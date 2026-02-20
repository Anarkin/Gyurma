using Gyurma;
using Gyurma.Mocks;
using Xunit;

[assembly: Gyurma(typeof(ICallCountTarget))]

public interface ICallCountTarget
{
    double Add(double a, double b);
    void MemoryStore(double value);
    void MemoryClear();
    double LastResult { get; }
    string Mode { get; set; }
    double this[int slot] { get; }
    string this[string register] { get; set; }
}

public class CallCountTests
{
    // ==================== Methods with args ====================

    [Fact]
    public void Method_call_count_with_args()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup.Add(2, 3).Returns(5);

        mock.Add(2, 3);
        mock.Add(2, 3);
        mock.Add(2, 3);

        Assert.Equal(3, mock.CallCounts.Add(2, 3));
    }

    [Fact]
    public void Uncalled_method_returns_zero()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup.Add(2, 3).Returns(5);

        Assert.Equal(0, mock.CallCounts.Add(2, 3));
        Assert.Equal(0, mock.CallCounts.Add(9, 9));
    }

    [Fact]
    public void Different_args_tracked_separately()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup.Add(1, 2).Returns(3);
        mock.Setup.Add(10, 20).Returns(30);

        mock.Add(1, 2);
        mock.Add(1, 2);
        mock.Add(10, 20);

        Assert.Equal(2, mock.CallCounts.Add(1, 2));
        Assert.Equal(1, mock.CallCounts.Add(10, 20));
        Assert.Equal(0, mock.CallCounts.Add(5, 5));
    }

    [Fact]
    public void Calls_through_NotImplementedException_still_counted()
    {
        var mock = new ICallCountTargetGyurma();

        Assert.Throws<NotImplementedException>(() => mock.Add(1, 1));
        Assert.Throws<NotImplementedException>(() => mock.Add(1, 1));

        Assert.Equal(2, mock.CallCounts.Add(1, 1));
    }

    // ==================== Void methods ====================

    [Fact]
    public void Void_method_with_args_call_count()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup.MemoryStore(42);

        mock.MemoryStore(42);
        mock.MemoryStore(42);

        Assert.Equal(2, mock.CallCounts.MemoryStore(42));
        Assert.Equal(0, mock.CallCounts.MemoryStore(99));
    }

    [Fact]
    public void Void_parameterless_method_call_count()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup.MemoryClear();

        mock.MemoryClear();
        mock.MemoryClear();
        mock.MemoryClear();

        Assert.Equal(3, mock.CallCounts.MemoryClear());
    }

    [Fact]
    public void Void_parameterless_no_setup_still_counted()
    {
        var mock = new ICallCountTargetGyurma();

        Assert.Throws<NotImplementedException>(() => mock.MemoryClear());

        Assert.Equal(1, mock.CallCounts.MemoryClear());
    }

    // ==================== Properties ====================

    [Fact]
    public void Property_getter_call_count()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup.LastResult.Returns(42.0);

        _ = mock.LastResult;
        _ = mock.LastResult;

        Assert.Equal(2, mock.CallCounts.LastResult);
    }

    [Fact]
    public void Property_getter_no_setup_still_counted()
    {
        var mock = new ICallCountTargetGyurma();

        Assert.Throws<NotImplementedException>(() => mock.LastResult);

        Assert.Equal(1, mock.CallCounts.LastResult);
    }

    [Fact]
    public void Property_uncalled_returns_zero()
    {
        var mock = new ICallCountTargetGyurma();

        Assert.Equal(0, mock.CallCounts.LastResult);
        Assert.Equal(0, mock.CallCounts.Mode);
    }

    [Fact]
    public void Property_with_setter_only_counts_getter()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup.Mode.Returns("DEG");

        mock.Mode = "RAD"; // setter - not counted
        _ = mock.Mode;     // getter - counted

        Assert.Equal(1, mock.CallCounts.Mode);
    }

    // ==================== Indexers ====================

    [Fact]
    public void Indexer_getter_call_count()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup[0].Returns(3.14);

        _ = mock[0];
        _ = mock[0];

        Assert.Equal(2, mock.CallCounts[0]);
    }

    [Fact]
    public void Indexer_different_keys_tracked_separately()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup[0].Returns(1.0);
        mock.Setup[1].Returns(2.0);

        _ = mock[0];
        _ = mock[0];
        _ = mock[1];

        Assert.Equal(2, mock.CallCounts[0]);
        Assert.Equal(1, mock.CallCounts[1]);
        Assert.Equal(0, mock.CallCounts[99]);
    }

    [Fact]
    public void Indexer_no_setup_still_counted()
    {
        var mock = new ICallCountTargetGyurma();

        Assert.Throws<NotImplementedException>(() => mock[0]);

        Assert.Equal(1, mock.CallCounts[0]);
    }

    [Fact]
    public void String_indexer_setter_not_counted()
    {
        var mock = new ICallCountTargetGyurma();
        mock.Setup["x"].Returns("value");

        mock["y"] = "ignored"; // setter - not counted
        _ = mock["x"];         // getter - counted

        Assert.Equal(1, mock.CallCounts["x"]);
        Assert.Equal(0, mock.CallCounts["y"]);
    }

    // ==================== Generics ====================

    [Fact]
    public void Generic_method_call_counts_separate_by_type_args()
    {
        var serializer = new ISerializerGyurma();
        var user = new User { Id = 1 };
        var order = new Order { OrderId = 1 };

        serializer.Setup.Deserialize<User>("{}").Returns(user);
        serializer.Setup.Deserialize<Order>("{}").Returns(order);

        serializer.Deserialize<User>("{}");
        serializer.Deserialize<User>("{}");
        serializer.Deserialize<Order>("{}");

        Assert.Equal(2, serializer.CallCounts.Deserialize<User>("{}"));
        Assert.Equal(1, serializer.CallCounts.Deserialize<Order>("{}"));
        Assert.Equal(0, serializer.CallCounts.Deserialize<User>("other"));
    }

    [Fact]
    public void Generic_type_call_counts()
    {
        var repo = new IRepositoryGyurma<User>();
        var user = new User { Id = 1, Name = "Alice" };
        repo.Setup.GetById(1).Returns(user);

        repo.GetById(1);
        repo.GetById(1);
        repo.GetById(1);

        Assert.Equal(3, repo.CallCounts.GetById(1));
        Assert.Equal(0, repo.CallCounts.GetById(2));
    }

    [Fact]
    public void Generic_indexer_call_counts()
    {
        var indexer = new IGenericIndexerGyurma<string, int>();
        indexer.Setup["a"].Returns(1);
        indexer.Setup["b"].Returns(2);

        _ = indexer["a"];
        _ = indexer["a"];
        _ = indexer["b"];

        Assert.Equal(2, indexer.CallCounts["a"]);
        Assert.Equal(1, indexer.CallCounts["b"]);
        Assert.Equal(0, indexer.CallCounts["c"]);
    }

    [Fact]
    public void Multi_type_param_generic_method_call_counts()
    {
        var serializer = new ISerializerGyurma();
        serializer.Setup.Convert<int, string>(42).Returns("42");
        serializer.Setup.Convert<string, int>("42").Returns(42);

        serializer.Convert<int, string>(42);
        serializer.Convert<int, string>(42);
        serializer.Convert<string, int>("42");

        Assert.Equal(2, serializer.CallCounts.Convert<int, string>(42));
        Assert.Equal(1, serializer.CallCounts.Convert<string, int>("42"));
    }
}
