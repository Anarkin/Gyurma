# CONCEPT.md

"gyurma", a mocking framework project using source generators

## public api

```cs
[assembly: Gyurma(typeof(SomeClass1))]
[assembly: Gyurma(typeof(SomeClass2))]

using Gyurma.Mocks;
```
it is recommended to add the assembly-level attribute locally, right next to each test setup; meaning it might be used on the same type multiple times

and then it generates classes such as `SomeClass1Gyurma` and `SomeClass2Gyurma`, so, following the pattern where it adds `Gyurma` to the original class' name as a postfix
- the generated classes go under the `Gyurma.Mocks` namespace

then, `SomeClass1Gyurma` implements `SomeClass1`
- usually `SomeClass1` is an `interface` or `abstract class`, which are explicitly supported (but can be any type in theory, having parameterless constructors)

then, `SomeClass1Gyurma` has the following members:
- `Setup`, which has the same members as `SomeClass1` but with different return types. each member here returns a strongly typed setup object with additional strongly typed methods:
  - `Returns`
  - `Throws<>`

## sample usage

imagine you have the following - production - types

```cs
public interface ICalculator
{
    double Add(double a, double b);
    void MemoryStore(double value);
    void MemoryClear();
    string Format(object value);
    double LastResult { get; }
    string Mode { get; set; }
    double this[int slot] { get; }
    string this[string register] { get; set; }
}

public abstract class AbstractShape
{
    public abstract double Area();
    public abstract double Perimeter(double scale);
    public virtual void Validate() { }
}
```

the testing and mocking side goes as:

```cs
[assembly: Gyurma(typeof(ICalculator))]

using Gyurma.Mocks;

public class ICalculatorTest
{
    public void Main()
    {
        var calculator = new ICalculatorGyurma();
        // the `calculator` instance is available at this point (source generated)
        // ...
    }
}
```

## supported members

### methods

public api:
- Returns() (for non-void methods)
- Throws<>

note: void method setups return `IVoidMethodSetup` which only has `Throws<>()` since void methods cannot return values

considerations on methods:
- when a method is called without a setup taking place first, it throws a `NotImplementedException`
- `void` method setups automatically register a no-op (no need to chain any method like `.Returns()`)
- setting up the same method multiple times with the same arguments: the last setup is effective only
- setting up the same method multiple times with different arguments: works separately
- a setup (the last one of same arguments) is permanent, i.e. it returns the same upon multiple calls
- there is no way to verify that a method was called

considerations on arguments:
- strict equality; no argument matchers exist
- reference types are compared by references


examples, emitting some boilerplate context:

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup.Add(2, 3).Returns(5);
calculator.Setup.Add(2, 10).Returns(12);

Assert.Equal(5, calculator.Add(2, 3));
Assert.Equal(12, calculator.Add(2, 10));
```

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup.Add(2, 0).Throws<DivideByZeroException>();

Assert.Throws<DivideByZeroException>(() => calculator.Add(2, 0));
```

```cs
var calculator = new ICalculatorGyurma();

Assert.Throws<NotImplementedException>(() => calculator.Add(2, 2));
```

### properties

conceptually, properties should be handled the same as other members already described, having the same or similar public apis, and following the same rules, for example,
- when a getter is called without a setup taking place first, it throws a `NotImplementedException`
- when a setter is called, consistently with a `void` method setup, nothing happens

examples, emitting some boilerplate context:

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup.LastResult.Returns(42.0);

Assert.Equal(42.0, calculator.LastResult);
```

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup.Mode.Returns("DEG");

Assert.Equal("DEG", calculator.Mode);
```

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup.LastResult.Throws<InvalidOperationException>();

Assert.Throws<InvalidOperationException>(() => calculator.LastResult);
```

```cs
var calculator = new ICalculatorGyurma();

calculator.Mode = "RAD";

// getter still has no setup:
Assert.Throws<NotImplementedException>(() => calculator.Mode);
```

### indexers

conceptually, indexers should be handled the same as other members already described, having the same or similar public apis, and following the same rules, for example,
- when an indexer getter is called without a setup taking place first, it throws a `NotImplementedException`
- when an indexer setter is called, consistently with a `void` method setup, nothing happens

examples, emitting some boilerplate context:

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup[0].Returns(3.14);

Assert.Equal(3.14, calculator[0]);
```

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup[0].Returns(1.0);
calculator.Setup[1].Returns(2.0);

Assert.Equal(1.0, calculator[0]);
Assert.Equal(2.0, calculator[1]);
```

```cs
var calculator = new ICalculatorGyurma();

calculator.Setup[0].Throws<InvalidOperationException>();

Assert.Throws<InvalidOperationException>(() => calculator[0]);
```

```cs
var calculator = new ICalculatorGyurma();

Assert.Throws<NotImplementedException>(() => calculator[0]);
```

```cs
var calculator = new ICalculatorGyurma();

calculator["x"] = "value";

// getter still has no setup:
Assert.Throws<NotImplementedException>(() => calculator["x"]);
```

## generics

having the following production type:

```cs
public interface IGenericType<TClass1, TClass2>
{
    TClass1 Filter(TClass1 value);
    TClass2 Map(TClass1 value);

    TClass1 this[int index] { get; set; }
    TClass2 this[string index] { get; set; }

    TMethod2 Select<TMethod1, TMethod2>(TMethod1 value);
}
```

and the following considerations:
- constraints are preserved in the generated mock, meaning compile-time errors occur when violated
- different type arguments are treated as separate setups, similar to how different argument values work, meaning that setting up `Select<int, string>` does not affect `Select<bool, double>`, and vice versa
- nested generics are supported

the usage goes as:

```cs
[assembly: Gyurma(typeof(IGenericType<,>))] // open

var genericType = new IGenericTypeGyurma<string, int>();
genericType.Setup.Filter("input").Returns("npt");
genericType.Setup.Map("hello").Returns(42);

genericType.Setup[0].Returns("first");
genericType.Setup["key"].Returns(42);

genericType.Setup.Select<bool, double>(true).Returns(1.0);
genericType.Setup.Select<int, string>(1).Returns("one");

```
