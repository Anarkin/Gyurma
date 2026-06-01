using Gyurma;
using Gyurma.Mocks;
using Xunit;

[assembly: Gyurma(typeof(AbstractShape))]
[assembly: Gyurma(typeof(AbstractDerived))]
[assembly: Gyurma(typeof(AbstractPartialOverride))]

public abstract class AbstractShape
{
    public abstract double Area();
    public abstract double Perimeter(double scale);
    public virtual void Validate() { }
}

public abstract class AbstractDerived : AbstractShape
{
    public abstract string Name { get; }
}

public abstract class AbstractPartialOverride : AbstractShape
{
    public sealed override double Area()
    {
        return 0;
    }
    // Perimeter() and Validate() remain inherited from AbstractShape
}

public class AbstractClassTests
{
    [Fact]
    public void Abstract_method_returns()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Area().Returns(42.0);
        Assert.Equal(42.0, shape.Area());
    }

    [Fact]
    public void Abstract_method_with_args_returns()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Perimeter(2.0).Returns(10.0);
        Assert.Equal(10.0, shape.Perimeter(2.0));
    }

    [Fact]
    public void Abstract_method_throws()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Area().Throws<InvalidOperationException>();
        Assert.Throws<InvalidOperationException>(() => shape.Area());
    }

    [Fact]
    public void No_setup_throws_NotImplementedException()
    {
        var shape = new AbstractShapeGyurma();
        Assert.Throws<NotImplementedException>(() => shape.Area());
    }

    [Fact]
    public void Virtual_void_method_no_setup_throws()
    {
        var shape = new AbstractShapeGyurma();
        Assert.Throws<NotImplementedException>(() => shape.Validate());
    }

    [Fact]
    public void Mock_is_instance_of_abstract_class()
    {
        var shape = new AbstractShapeGyurma();
        Assert.IsAssignableFrom<AbstractShape>(shape);
    }

    [Fact]
    public void Setup_is_permanent()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Area().Returns(100.0);
        Assert.Equal(100.0, shape.Area());
        Assert.Equal(100.0, shape.Area());
        Assert.Equal(100.0, shape.Area());
    }

    [Fact]
    public void Last_setup_wins()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Area().Returns(10.0);
        shape.Setup.Area().Returns(20.0);
        Assert.Equal(20.0, shape.Area());
    }

    [Fact]
    public void Different_args_different_results()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Perimeter(1.0).Returns(4.0);
        shape.Setup.Perimeter(2.0).Returns(8.0);
        Assert.Equal(4.0, shape.Perimeter(1.0));
        Assert.Equal(8.0, shape.Perimeter(2.0));
    }

    [Fact]
    public void Virtual_void_method_setup_does_nothing()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Validate();
        shape.Validate(); // should not throw
    }

    [Fact]
    public void Abstract_method_with_args_throws()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Perimeter(1.0).Throws<ArgumentException>();
        Assert.Throws<ArgumentException>(() => shape.Perimeter(1.0));
    }

    [Fact]
    public void Abstract_method_no_setup_for_specific_args_throws()
    {
        var shape = new AbstractShapeGyurma();
        shape.Setup.Perimeter(1.0).Returns(4.0);
        Assert.Throws<NotImplementedException>(() => shape.Perimeter(2.0));
    }

    // ==================== Abstract Class Inheritance ====================

    [Fact]
    public void Derived_own_member_works()
    {
        var mock = new AbstractDerivedGyurma();
        mock.Setup.Name.Returns("test");
        Assert.Equal("test", mock.Name);
    }

    [Fact]
    public void Derived_inherited_abstract_method_works()
    {
        var mock = new AbstractDerivedGyurma();
        mock.Setup.Area().Returns(25.0);
        Assert.Equal(25.0, mock.Area());
    }

    [Fact]
    public void Derived_inherited_abstract_method_with_args_works()
    {
        var mock = new AbstractDerivedGyurma();
        mock.Setup.Perimeter(2.0).Returns(16.0);
        Assert.Equal(16.0, mock.Perimeter(2.0));
    }

    [Fact]
    public void Derived_inherited_virtual_method_works()
    {
        var mock = new AbstractDerivedGyurma();
        mock.Setup.Validate();
        mock.Validate(); // should not throw
    }

    [Fact]
    public void Derived_inherited_no_setup_throws()
    {
        var mock = new AbstractDerivedGyurma();
        Assert.Throws<NotImplementedException>(() => mock.Area());
    }

    [Fact]
    public void Derived_is_assignable_to_all_ancestors()
    {
        var mock = new AbstractDerivedGyurma();
        Assert.IsAssignableFrom<AbstractDerived>(mock);
        Assert.IsAssignableFrom<AbstractShape>(mock);
    }

    // ==================== Partial Override (Sealed) ====================

    [Fact]
    public void Partial_override_sealed_method_uses_base_impl()
    {
        var mock = new AbstractPartialOverrideGyurma();
        // Area() is sealed in AbstractPartialOverride, so it uses the concrete implementation
        Assert.Equal(0, mock.Area());
    }

    [Fact]
    public void Partial_override_inherited_abstract_method_works()
    {
        var mock = new AbstractPartialOverrideGyurma();
        mock.Setup.Perimeter(1.0).Returns(4.0);
        Assert.Equal(4.0, mock.Perimeter(1.0));
    }

    [Fact]
    public void Partial_override_inherited_virtual_method_works()
    {
        var mock = new AbstractPartialOverrideGyurma();
        mock.Setup.Validate();
        mock.Validate(); // should not throw
    }

    [Fact]
    public void Partial_override_inherited_no_setup_throws()
    {
        var mock = new AbstractPartialOverrideGyurma();
        Assert.Throws<NotImplementedException>(() => mock.Perimeter(1.0));
    }
}
