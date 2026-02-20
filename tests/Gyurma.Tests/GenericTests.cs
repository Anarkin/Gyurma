using Gyurma;
using Gyurma.Mocks;
using Xunit;

// Generic interface: single type parameter
[assembly: Gyurma(typeof(IRepository<>))]
// Generic interface: multiple type parameters
[assembly: Gyurma(typeof(ICache<,>))]
// Generic interface with constraints
[assembly: Gyurma(typeof(IFactory<>))]
// Non-generic interface with generic methods
[assembly: Gyurma(typeof(ISerializer))]
// Generic interface with generic methods (combined)
[assembly: Gyurma(typeof(IProcessor<>))]
// Generic interface with generic indexers
[assembly: Gyurma(typeof(IGenericIndexer<,>))]

public interface IRepository<T>
{
    T? GetById(int id);
    void Save(T entity);
    IEnumerable<T> GetAll();
}

public interface ICache<TKey, TValue>
{
    TValue? Get(TKey key);
    void Set(TKey key, TValue value);
    bool Remove(TKey key);
}

public interface IFactory<T> where T : class, new()
{
    T Create();
    T CreateWithName(string name);
}

public interface ISerializer
{
    T? Deserialize<T>(string json);
    string Serialize<T>(T value);
    void Process<T>(T item);
    TOut Convert<TIn, TOut>(TIn value);
}

public interface IProcessor<T>
{
    TOut Transform<TOut>(T input);
    void Apply<TContext>(T input, TContext context);
}

public interface IGenericIndexer<TKey, TValue>
{
    TValue this[TKey key] { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class Order
{
    public int OrderId { get; set; }
}

public class GenericTests
{
    // ==================== Generic Interface Basic ====================

    [Fact]
    public void Generic_interface_returns_setup_value()
    {
        var repo = new IRepositoryGyurma<User>();
        var user = new User { Id = 1, Name = "Alice" };
        repo.Setup.GetById(1).Returns(user);

        Assert.Same(user, repo.GetById(1));
    }

    [Fact]
    public void Generic_interface_void_method_auto_noop()
    {
        var repo = new IRepositoryGyurma<User>();
        var user = new User { Id = 1 };
        repo.Setup.Save(user);

        repo.Save(user); // should not throw
    }

    [Fact]
    public void Generic_interface_void_method_throws()
    {
        var repo = new IRepositoryGyurma<User>();
        var user = new User { Id = 1 };
        repo.Setup.Save(user).Throws<InvalidOperationException>();

        Assert.Throws<InvalidOperationException>(() => repo.Save(user));
    }

    [Fact]
    public void Generic_interface_no_setup_throws_NotImplementedException()
    {
        var repo = new IRepositoryGyurma<User>();

        Assert.Throws<NotImplementedException>(() => repo.GetById(1));
    }

    [Fact]
    public void Generic_interface_void_no_setup_throws()
    {
        var repo = new IRepositoryGyurma<User>();
        var user = new User { Id = 1 };

        Assert.Throws<NotImplementedException>(() => repo.Save(user));
    }

    [Fact]
    public void Generic_interface_collection_return()
    {
        var repo = new IRepositoryGyurma<User>();
        var users = new List<User> { new User { Id = 1 }, new User { Id = 2 } };
        repo.Setup.GetAll().Returns(users);

        Assert.Same(users, repo.GetAll());
    }

    // ==================== Multi-Type Parameter Interface ====================

    [Fact]
    public void Multi_type_param_interface_works()
    {
        var cache = new ICacheGyurma<string, User>();
        var user = new User { Id = 1 };
        cache.Setup.Get("key1").Returns(user);

        Assert.Same(user, cache.Get("key1"));
    }

    [Fact]
    public void Multi_type_param_interface_void_method()
    {
        var cache = new ICacheGyurma<string, int>();
        cache.Setup.Set("count", 42);

        cache.Set("count", 42); // should not throw
    }

    [Fact]
    public void Multi_type_param_interface_different_args()
    {
        var cache = new ICacheGyurma<int, string>();
        cache.Setup.Get(1).Returns("one");
        cache.Setup.Get(2).Returns("two");

        Assert.Equal("one", cache.Get(1));
        Assert.Equal("two", cache.Get(2));
    }

    [Fact]
    public void Multi_type_param_interface_returns_primitive()
    {
        var cache = new ICacheGyurma<string, User>();
        cache.Setup.Remove("key").Returns(true);

        Assert.True(cache.Remove("key"));
    }

    // ==================== Generic Interface With Constraints ====================

    [Fact]
    public void Constrained_generic_interface_works()
    {
        var factory = new IFactoryGyurma<User>();
        var user = new User { Id = 1 };
        factory.Setup.Create().Returns(user);

        Assert.Same(user, factory.Create());
    }

    [Fact]
    public void Constrained_generic_interface_with_params()
    {
        var factory = new IFactoryGyurma<User>();
        var user = new User { Id = 1, Name = "Bob" };
        factory.Setup.CreateWithName("Bob").Returns(user);

        Assert.Same(user, factory.CreateWithName("Bob"));
    }

    // ==================== Generic Methods ====================

    [Fact]
    public void Generic_method_returns_setup_value()
    {
        var serializer = new ISerializerGyurma();
        var user = new User { Id = 1 };
        serializer.Setup.Deserialize<User>("{}").Returns(user);

        Assert.Same(user, serializer.Deserialize<User>("{}"));
    }

    [Fact]
    public void Generic_method_different_type_args_separate()
    {
        var serializer = new ISerializerGyurma();
        var user = new User { Id = 1 };
        var order = new Order { OrderId = 100 };

        serializer.Setup.Deserialize<User>("{}").Returns(user);
        serializer.Setup.Deserialize<Order>("{}").Returns(order);

        Assert.Same(user, serializer.Deserialize<User>("{}"));
        Assert.Same(order, serializer.Deserialize<Order>("{}"));
    }

    [Fact]
    public void Generic_method_no_setup_throws()
    {
        var serializer = new ISerializerGyurma();

        Assert.Throws<NotImplementedException>(() => serializer.Deserialize<User>("{}"));
    }

    [Fact]
    public void Generic_method_returns_string()
    {
        var serializer = new ISerializerGyurma();
        var user = new User { Id = 1 };
        serializer.Setup.Serialize(user).Returns("{\"id\":1}");

        Assert.Equal("{\"id\":1}", serializer.Serialize(user));
    }

    [Fact]
    public void Void_generic_method_auto_noop()
    {
        var serializer = new ISerializerGyurma();
        serializer.Setup.Process("item");

        serializer.Process("item"); // should not throw
    }

    [Fact]
    public void Void_generic_method_throws()
    {
        var serializer = new ISerializerGyurma();
        serializer.Setup.Process(42).Throws<InvalidOperationException>();

        Assert.Throws<InvalidOperationException>(() => serializer.Process(42));
    }

    [Fact]
    public void Void_generic_method_no_setup_throws()
    {
        var serializer = new ISerializerGyurma();

        Assert.Throws<NotImplementedException>(() => serializer.Process("test"));
    }

    // ==================== Multi-Type Parameter Generic Methods ====================

    [Fact]
    public void Multi_type_param_generic_method_works()
    {
        var serializer = new ISerializerGyurma();
        serializer.Setup.Convert<int, string>(42).Returns("42");

        Assert.Equal("42", serializer.Convert<int, string>(42));
    }

    [Fact]
    public void Multi_type_param_generic_method_different_types()
    {
        var serializer = new ISerializerGyurma();
        serializer.Setup.Convert<int, string>(42).Returns("42");
        serializer.Setup.Convert<string, int>("42").Returns(42);

        Assert.Equal("42", serializer.Convert<int, string>(42));
        Assert.Equal(42, serializer.Convert<string, int>("42"));
    }

    // ==================== Combined: Generic Method on Generic Interface ====================

    [Fact]
    public void Generic_method_on_generic_interface_works()
    {
        var processor = new IProcessorGyurma<string>();
        processor.Setup.Transform<int>("42").Returns(42);
        processor.Setup.Transform<bool>("true").Returns(true);

        Assert.Equal(42, processor.Transform<int>("42"));
        Assert.True(processor.Transform<bool>("true"));
    }

    [Fact]
    public void Void_generic_method_on_generic_interface()
    {
        var processor = new IProcessorGyurma<string>();
        var context = new object();
        processor.Setup.Apply("input", context);

        processor.Apply("input", context); // should not throw
    }

    [Fact]
    public void Generic_method_on_generic_interface_no_setup_throws()
    {
        var processor = new IProcessorGyurma<string>();

        Assert.Throws<NotImplementedException>(() => processor.Transform<int>("test"));
    }

    // ==================== Generic Indexers ====================

    [Fact]
    public void Generic_indexer_returns_setup_value()
    {
        var indexer = new IGenericIndexerGyurma<string, int>();
        indexer.Setup["key"].Returns(42);

        Assert.Equal(42, indexer["key"]);
    }

    [Fact]
    public void Generic_indexer_different_keys()
    {
        var indexer = new IGenericIndexerGyurma<int, string>();
        indexer.Setup[0].Returns("zero");
        indexer.Setup[1].Returns("one");

        Assert.Equal("zero", indexer[0]);
        Assert.Equal("one", indexer[1]);
    }

    [Fact]
    public void Generic_indexer_setter_is_noop()
    {
        var indexer = new IGenericIndexerGyurma<string, int>();
        indexer["key"] = 42;

        Assert.Throws<NotImplementedException>(() => indexer["key"]);
    }

    [Fact]
    public void Generic_indexer_no_setup_throws()
    {
        var indexer = new IGenericIndexerGyurma<string, int>();

        Assert.Throws<NotImplementedException>(() => indexer["key"]);
    }

    // ==================== Mock Assignability ====================

    [Fact]
    public void Generic_mock_is_assignable_to_interface()
    {
        var repo = new IRepositoryGyurma<User>();
        Assert.IsAssignableFrom<IRepository<User>>(repo);
    }

    [Fact]
    public void Multi_type_param_mock_is_assignable_to_interface()
    {
        var cache = new ICacheGyurma<string, User>();
        Assert.IsAssignableFrom<ICache<string, User>>(cache);
    }

    [Fact]
    public void Constrained_mock_is_assignable_to_interface()
    {
        var factory = new IFactoryGyurma<User>();
        Assert.IsAssignableFrom<IFactory<User>>(factory);
    }

    // ==================== Setup Permanence ====================

    [Fact]
    public void Generic_interface_setup_is_permanent()
    {
        var repo = new IRepositoryGyurma<User>();
        var user = new User { Id = 1 };
        repo.Setup.GetById(1).Returns(user);

        Assert.Same(user, repo.GetById(1));
        Assert.Same(user, repo.GetById(1));
        Assert.Same(user, repo.GetById(1));
    }

    [Fact]
    public void Generic_method_setup_is_permanent()
    {
        var serializer = new ISerializerGyurma();
        var user = new User { Id = 1 };
        serializer.Setup.Deserialize<User>("{}").Returns(user);

        Assert.Same(user, serializer.Deserialize<User>("{}"));
        Assert.Same(user, serializer.Deserialize<User>("{}"));
        Assert.Same(user, serializer.Deserialize<User>("{}"));
    }

    [Fact]
    public void Generic_interface_last_setup_wins()
    {
        var repo = new IRepositoryGyurma<User>();
        var user1 = new User { Id = 1 };
        var user2 = new User { Id = 2 };

        repo.Setup.GetById(1).Returns(user1);
        repo.Setup.GetById(1).Returns(user2);

        Assert.Same(user2, repo.GetById(1));
    }

    [Fact]
    public void Generic_method_last_setup_wins()
    {
        var serializer = new ISerializerGyurma();
        var user1 = new User { Id = 1 };
        var user2 = new User { Id = 2 };

        serializer.Setup.Deserialize<User>("{}").Returns(user1);
        serializer.Setup.Deserialize<User>("{}").Returns(user2);

        Assert.Same(user2, serializer.Deserialize<User>("{}"));
    }
}
