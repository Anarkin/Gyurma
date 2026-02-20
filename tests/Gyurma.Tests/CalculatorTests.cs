using Gyurma;
using Gyurma.Mocks;
using Xunit;

[assembly: Gyurma(typeof(ICalculator))]
[assembly: Gyurma(typeof(ICalculator))] // duplicate to verify deduplication in generator
[assembly: Gyurma(typeof(ICalculator))]
[assembly: Gyurma(typeof(IInitProps))]

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

public interface IInitProps
{
    string Name { get; init; }
    int ReadOnly { get; }
}

public class CalculatorTests
{
    [Fact]
    public void Returns_works()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.Add(2, 3).Returns(5);
        Assert.Equal(5, calc.Add(2, 3));
    }

    [Fact]
    public void Throws_works()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.Add(2, 0).Throws<DivideByZeroException>();
        Assert.Throws<DivideByZeroException>(() => calc.Add(2, 0));
    }

    [Fact]
    public void No_setup_throws_NotImplementedException()
    {
        var calc = new ICalculatorGyurma();
        Assert.Throws<NotImplementedException>(() => calc.Add(1, 2));
    }

    [Fact]
    public void Void_method_setup_does_nothing()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.MemoryStore(42);
        calc.MemoryStore(42); // should not throw
    }

    [Fact]
    public void Void_method_throws()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.MemoryClear().Throws<InvalidOperationException>();
        Assert.Throws<InvalidOperationException>(() => calc.MemoryClear());
    }

    [Fact]
    public void Last_setup_wins()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.Add(1, 1).Returns(10);
        calc.Setup.Add(1, 1).Returns(2);
        Assert.Equal(2, calc.Add(1, 1));
    }

    [Fact]
    public void Setup_is_permanent()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.Add(1, 2).Returns(3);
        Assert.Equal(3, calc.Add(1, 2));
        Assert.Equal(3, calc.Add(1, 2));
        Assert.Equal(3, calc.Add(1, 2));
    }

    [Fact]
    public void Different_args_different_results()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.Add(1, 2).Returns(3);
        calc.Setup.Add(10, 20).Returns(30);
        Assert.Equal(3, calc.Add(1, 2));
        Assert.Equal(30, calc.Add(10, 20));
    }

    [Fact]
    public void No_setup_for_specific_args_throws()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.Add(1, 2).Returns(3);
        Assert.Throws<NotImplementedException>(() => calc.Add(5, 6));
    }

    [Fact]
    public void Reference_type_args_use_reference_equality()
    {
        var calc = new ICalculatorGyurma();
        var obj = new object();
        calc.Setup.Format(obj).Returns("hello");
        Assert.Equal("hello", calc.Format(obj));
    }

    [Fact]
    public void Different_reference_throws()
    {
        var calc = new ICalculatorGyurma();
        var obj1 = new object();
        var obj2 = new object();
        calc.Setup.Format(obj1).Returns("hello");
        Assert.Throws<NotImplementedException>(() => calc.Format(obj2));
    }

    [Fact]
    public void Property_getter_returns_setup_value()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.LastResult.Returns(42.0);
        Assert.Equal(42.0, calc.LastResult);
    }

    [Fact]
    public void Property_getter_setter_returns_setup_value()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.Mode.Returns("DEG");
        Assert.Equal("DEG", calc.Mode);
    }

    [Fact]
    public void Property_getter_no_setup_throws()
    {
        var calc = new ICalculatorGyurma();
        Assert.Throws<NotImplementedException>(() => calc.LastResult);
    }

    [Fact]
    public void Property_getter_throws_with_setup()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.LastResult.Throws<InvalidOperationException>();
        Assert.Throws<InvalidOperationException>(() => calc.LastResult);
    }

    [Fact]
    public void Property_setter_is_noop()
    {
        var calc = new ICalculatorGyurma();
        calc.Mode = "RAD";
        Assert.Throws<NotImplementedException>(() => calc.Mode);
    }

    [Fact]
    public void Indexer_getter_returns_setup_value()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup[0].Returns(3.14);
        Assert.Equal(3.14, calc[0]);
    }

    [Fact]
    public void Indexer_different_args_return_different_results()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup[0].Returns(1.0);
        calc.Setup[1].Returns(2.0);
        Assert.Equal(1.0, calc[0]);
        Assert.Equal(2.0, calc[1]);
    }

    [Fact]
    public void Indexer_getter_no_setup_throws()
    {
        var calc = new ICalculatorGyurma();
        Assert.Throws<NotImplementedException>(() => calc[0]);
    }

    [Fact]
    public void Indexer_getter_throws_with_setup()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup[0].Throws<InvalidOperationException>();
        Assert.Throws<InvalidOperationException>(() => calc[0]);
    }

    [Fact]
    public void Indexer_setter_is_noop()
    {
        var calc = new ICalculatorGyurma();
        calc["x"] = "value";
        Assert.Throws<NotImplementedException>(() => calc["x"]);
    }

    [Fact]
    public void Mock_is_instance_of_interface()
    {
        var calc = new ICalculatorGyurma();
        Assert.IsAssignableFrom<ICalculator>(calc);
    }

    [Fact]
    public void Void_method_no_setup_throws()
    {
        var calc = new ICalculatorGyurma();
        Assert.Throws<NotImplementedException>(() => calc.MemoryStore(42));
    }

    [Fact]
    public void Void_method_parameterless_no_setup_throws()
    {
        var calc = new ICalculatorGyurma();
        Assert.Throws<NotImplementedException>(() => calc.MemoryClear());
    }

    [Fact]
    public void Property_setup_is_permanent()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.LastResult.Returns(99.0);
        Assert.Equal(99.0, calc.LastResult);
        Assert.Equal(99.0, calc.LastResult);
        Assert.Equal(99.0, calc.LastResult);
    }

    [Fact]
    public void Property_last_setup_wins()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup.LastResult.Returns(10.0);
        calc.Setup.LastResult.Returns(20.0);
        Assert.Equal(20.0, calc.LastResult);
    }

    [Fact]
    public void Indexer_setup_is_permanent()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup[0].Returns(7.0);
        Assert.Equal(7.0, calc[0]);
        Assert.Equal(7.0, calc[0]);
        Assert.Equal(7.0, calc[0]);
    }

    [Fact]
    public void Indexer_last_setup_wins()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup[0].Returns(5.0);
        calc.Setup[0].Returns(6.0);
        Assert.Equal(6.0, calc[0]);
    }

    [Fact]
    public void Indexer_string_key_different_keys_different_results()
    {
        var calc = new ICalculatorGyurma();
        calc.Setup["a"].Returns("alpha");
        calc.Setup["b"].Returns("beta");
        Assert.Equal("alpha", calc["a"]);
        Assert.Equal("beta", calc["b"]);
    }

    // ==================== Init-Only Properties ====================

    [Fact]
    public void Init_property_getter_returns_setup_value()
    {
        var mock = new IInitPropsGyurma();
        mock.Setup.Name.Returns("Alice");
        Assert.Equal("Alice", mock.Name);
    }

    [Fact]
    public void Init_property_setter_is_noop()
    {
        var mock = new IInitPropsGyurma() { Name = "ignored" };
        Assert.Throws<NotImplementedException>(() => mock.Name);
    }

    [Fact]
    public void Init_property_getter_no_setup_throws()
    {
        var mock = new IInitPropsGyurma();
        Assert.Throws<NotImplementedException>(() => mock.Name);
    }
}
