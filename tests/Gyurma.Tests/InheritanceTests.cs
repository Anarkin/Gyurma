using Gyurma;
using Gyurma.Mocks;
using Xunit;

// Simple interface inheritance
[assembly: Gyurma(typeof(IChild))]
// Multi-level inheritance
[assembly: Gyurma(typeof(IGrandchild))]
// Diamond inheritance
[assembly: Gyurma(typeof(IDiamond))]
// Generic interface inheritance
[assembly: Gyurma(typeof(IGenericChild<>))]
// Non-generic inheriting concrete generic
[assembly: Gyurma(typeof(IConcreteChild))]
// IDisposable inheritance
[assembly: Gyurma(typeof(IDisposableService))]
// Property accessor broadening
[assembly: Gyurma(typeof(IWritable))]

public interface IBase
{
    int Foo(int x);
    void Bar();
    double Value { get; }
    int this[int index] { get; }
}

public interface IChild : IBase
{
    string Baz(string s);
}

public interface IMid : IBase
{
    bool Flag { get; set; }
}

public interface IGrandchild : IMid
{
    string Name { get; }
}

public interface IDiamondLeft : IBase
{
    void Left();
}

public interface IDiamondRight : IBase
{
    void Right();
}

public interface IDiamond : IDiamondLeft, IDiamondRight
{
    void Center();
}

// Generic interface inheritance
public interface IGenericBase<T>
{
    T Get(int id);
    void Set(T value);
}

public interface IGenericChild<T> : IGenericBase<T>
{
    T Transform(T input);
}

// Non-generic inheriting concrete generic
public interface IConcreteChild : IGenericBase<string>
{
    void Extra();
}

// IDisposable inheritance
public interface IDisposableService : IDisposable
{
    string GetData();
}

// Property accessor broadening
public interface IReadable
{
    string Label { get; }
}

public interface IWritable : IReadable
{
    new string Label { get; set; }
}

public class InheritanceTests
{
    // ==================== Simple Inheritance ====================

    [Fact]
    public void Child_method_works()
    {
        var mock = new IChildGyurma();
        mock.Setup.Baz("hi").Returns("HI");
        Assert.Equal("HI", mock.Baz("hi"));
    }

    [Fact]
    public void Inherited_method_works()
    {
        var mock = new IChildGyurma();
        mock.Setup.Foo(1).Returns(10);
        Assert.Equal(10, mock.Foo(1));
    }

    [Fact]
    public void Inherited_void_method_works()
    {
        var mock = new IChildGyurma();
        mock.Setup.Bar();
        mock.Bar(); // should not throw
    }

    [Fact]
    public void Inherited_property_works()
    {
        var mock = new IChildGyurma();
        mock.Setup.Value.Returns(3.14);
        Assert.Equal(3.14, mock.Value);
    }

    [Fact]
    public void Inherited_indexer_works()
    {
        var mock = new IChildGyurma();
        mock.Setup[0].Returns(42);
        Assert.Equal(42, mock[0]);
    }

    [Fact]
    public void Inherited_method_no_setup_throws()
    {
        var mock = new IChildGyurma();
        Assert.Throws<NotImplementedException>(() => mock.Foo(1));
    }

    [Fact]
    public void Mock_is_assignable_to_child_and_base()
    {
        var mock = new IChildGyurma();
        Assert.IsAssignableFrom<IChild>(mock);
        Assert.IsAssignableFrom<IBase>(mock);
    }

    // ==================== Call Counts on Inherited Members ====================

    [Fact]
    public void Inherited_method_call_count()
    {
        var mock = new IChildGyurma();
        mock.Setup.Foo(1).Returns(10);

        mock.Foo(1);
        mock.Foo(1);

        Assert.Equal(2, mock.CallCounts.Foo(1));
    }

    [Fact]
    public void Inherited_property_call_count()
    {
        var mock = new IChildGyurma();
        mock.Setup.Value.Returns(1.0);

        _ = mock.Value;

        Assert.Equal(1, mock.CallCounts.Value);
    }

    [Fact]
    public void Inherited_indexer_call_count()
    {
        var mock = new IChildGyurma();
        mock.Setup[0].Returns(1);

        _ = mock[0];
        _ = mock[0];

        Assert.Equal(2, mock.CallCounts[0]);
    }

    // ==================== Multi-Level Inheritance ====================

    [Fact]
    public void Grandchild_own_member_works()
    {
        var mock = new IGrandchildGyurma();
        mock.Setup.Name.Returns("Alice");
        Assert.Equal("Alice", mock.Name);
    }

    [Fact]
    public void Grandchild_mid_member_works()
    {
        var mock = new IGrandchildGyurma();
        mock.Setup.Flag.Returns(true);
        Assert.True(mock.Flag);
    }

    [Fact]
    public void Grandchild_base_member_works()
    {
        var mock = new IGrandchildGyurma();
        mock.Setup.Foo(5).Returns(50);
        Assert.Equal(50, mock.Foo(5));
    }

    [Fact]
    public void Grandchild_assignable_to_all_ancestors()
    {
        var mock = new IGrandchildGyurma();
        Assert.IsAssignableFrom<IGrandchild>(mock);
        Assert.IsAssignableFrom<IMid>(mock);
        Assert.IsAssignableFrom<IBase>(mock);
    }

    // ==================== Diamond Inheritance ====================

    [Fact]
    public void Diamond_own_member_works()
    {
        var mock = new IDiamondGyurma();
        mock.Setup.Center();
        mock.Center();
    }

    [Fact]
    public void Diamond_left_member_works()
    {
        var mock = new IDiamondGyurma();
        mock.Setup.Left();
        mock.Left();
    }

    [Fact]
    public void Diamond_right_member_works()
    {
        var mock = new IDiamondGyurma();
        mock.Setup.Right();
        mock.Right();
    }

    [Fact]
    public void Diamond_base_member_works()
    {
        var mock = new IDiamondGyurma();
        mock.Setup.Foo(1).Returns(100);
        Assert.Equal(100, mock.Foo(1));
    }

    [Fact]
    public void Diamond_base_member_not_duplicated()
    {
        // Verifies IBase members appear only once despite diamond
        var mock = new IDiamondGyurma();
        mock.Setup.Value.Returns(9.9);
        Assert.Equal(9.9, mock.Value);
    }

    [Fact]
    public void Diamond_assignable_to_all_ancestors()
    {
        var mock = new IDiamondGyurma();
        Assert.IsAssignableFrom<IDiamond>(mock);
        Assert.IsAssignableFrom<IDiamondLeft>(mock);
        Assert.IsAssignableFrom<IDiamondRight>(mock);
        Assert.IsAssignableFrom<IBase>(mock);
    }

    // ==================== Generic Interface Inheritance ====================

    [Fact]
    public void Generic_child_own_member_works()
    {
        var mock = new IGenericChildGyurma<string>();
        mock.Setup.Transform("hello").Returns("HELLO");
        Assert.Equal("HELLO", mock.Transform("hello"));
    }

    [Fact]
    public void Generic_child_inherited_method_works()
    {
        var mock = new IGenericChildGyurma<string>();
        mock.Setup.Get(1).Returns("first");
        Assert.Equal("first", mock.Get(1));
    }

    [Fact]
    public void Generic_child_inherited_void_method_works()
    {
        var mock = new IGenericChildGyurma<int>();
        mock.Setup.Set(42);
        mock.Set(42); // should not throw
    }

    [Fact]
    public void Generic_child_no_setup_throws()
    {
        var mock = new IGenericChildGyurma<int>();
        Assert.Throws<NotImplementedException>(() => mock.Get(1));
    }

    [Fact]
    public void Generic_child_assignable_to_base()
    {
        var mock = new IGenericChildGyurma<string>();
        Assert.IsAssignableFrom<IGenericChild<string>>(mock);
        Assert.IsAssignableFrom<IGenericBase<string>>(mock);
    }

    // ==================== Non-Generic Inheriting Concrete Generic ====================

    [Fact]
    public void Concrete_child_own_member_works()
    {
        var mock = new IConcreteChildGyurma();
        mock.Setup.Extra();
        mock.Extra();
    }

    [Fact]
    public void Concrete_child_inherited_generic_method_works()
    {
        var mock = new IConcreteChildGyurma();
        mock.Setup.Get(1).Returns("result");
        Assert.Equal("result", mock.Get(1));
    }

    [Fact]
    public void Concrete_child_inherited_generic_void_works()
    {
        var mock = new IConcreteChildGyurma();
        mock.Setup.Set("value");
        mock.Set("value");
    }

    [Fact]
    public void Concrete_child_assignable_to_generic_base()
    {
        var mock = new IConcreteChildGyurma();
        Assert.IsAssignableFrom<IConcreteChild>(mock);
        Assert.IsAssignableFrom<IGenericBase<string>>(mock);
    }

    // ==================== IDisposable Inheritance ====================

    [Fact]
    public void Disposable_service_own_member_works()
    {
        var mock = new IDisposableServiceGyurma();
        mock.Setup.GetData().Returns("data");
        Assert.Equal("data", mock.GetData());
    }

    [Fact]
    public void Disposable_service_inherited_dispose_works()
    {
        var mock = new IDisposableServiceGyurma();
        mock.Setup.Dispose();
        mock.Dispose(); // should not throw
    }

    [Fact]
    public void Disposable_service_assignable()
    {
        var mock = new IDisposableServiceGyurma();
        Assert.IsAssignableFrom<IDisposableService>(mock);
        Assert.IsAssignableFrom<IDisposable>(mock);
    }

    // ==================== Property Accessor Broadening ====================

    [Fact]
    public void Broadened_property_getter_works()
    {
        var mock = new IWritableGyurma();
        mock.Setup.Label.Returns("hello");
        Assert.Equal("hello", mock.Label);
    }

    [Fact]
    public void Broadened_property_setter_is_noop()
    {
        var mock = new IWritableGyurma();
        mock.Label = "value";
        Assert.Throws<NotImplementedException>(() => mock.Label);
    }

    [Fact]
    public void Broadened_property_assignable_to_both()
    {
        var mock = new IWritableGyurma();
        Assert.IsAssignableFrom<IWritable>(mock);
        Assert.IsAssignableFrom<IReadable>(mock);
    }
}
