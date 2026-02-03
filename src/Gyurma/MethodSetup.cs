namespace Gyurma;

public class MethodSetup<T> : IMethodSetup<T>
{
    private readonly Action<Func<T>> _register;

    public MethodSetup(Action<Func<T>> register)
    {
        _register = register;
    }

    public void Returns(T value) => _register(() => value);

    public void Throws<TException>() where TException : Exception, new() => _register(() => throw new TException());
}
