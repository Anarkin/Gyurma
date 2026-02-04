# Gyurma

Gyurma is a lightweight .NET source generator-based mocking library. It generates mock implementations of interfaces and abstract classes at compile time.

## Installation

Add the `Gyurma` and `Gyurma.Generators` packages to your test project.

## Usage

### 1. Define the mock

Use the `[assembly: Gyurma(typeof(TypeToMock))]` attribute to instruct the generator to create a mock for `TypeToMock`.

```csharp
using Gyurma;

[assembly: Gyurma(typeof(ICalculator))]

public interface ICalculator
{
    int Add(int a, int b);
}
```

### 2. Create the mock instance

The generator creates a class named `{TypeName}Gyurma`. For `ICalculator`, it will be `ICalculatorGyurma`.

```csharp
var mock = new ICalculatorGyurma();
```

### 3. Setup behavior

Use the `Setup` property to configure the mock's behavior.

#### Return a value

```csharp
mock.Setup.Add(2, 3).Returns(5);
```

#### Return a value using a provider

```csharp
int counter = 0;
mock.Setup.Add(2, 3).Returns(() => ++counter);
```

#### Throw an exception

```csharp
mock.Setup.Add(2, 0).Throws<DivideByZeroException>();
```

Or with a specific exception instance:

```csharp
mock.Setup.Add(2, 0).Throws(new DivideByZeroException("Cannot divide by zero"));
```

### 4. Behavior

The mock will throw `NotImplementedException` if a method is called with arguments that have not been explicitly set up.

### Abstract Classes

Gyurma also supports abstract classes.

```csharp
[assembly: Gyurma(typeof(AbstractShape))]

public abstract class AbstractShape
{
    public abstract double Area();
}

// ...
var mock = new AbstractShapeGyurma();
mock.Setup.Area().Returns(42.0);
```

## Features

*   **Source Generated**: Mocks are generated at compile time, providing better performance and easier debugging.
*   **Strongly Typed**: Setup uses the actual methods of the interface/class, ensuring type safety.
*   **Method Overloads**: Supports method overloads.
*   **Properties and Indexers**: Supports mocking properties and indexers.
*   **Argument Matching**: Matches arguments by value (using `Equals`).
