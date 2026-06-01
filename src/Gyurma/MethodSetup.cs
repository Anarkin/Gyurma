namespace Gyurma;

public class MethodSetup<T> : IMethodSetup<T>
{
    private readonly Action<Func<T>> _register;

    public MethodSetup(Action<Func<T>> register)
    {
        this._register = register;
    }

    public void Returns(T value)
    {
        this._register(() => value);
    }

    public void Throws<TException>() where TException : Exception, new()
    {
        this._register(() => throw new TException());
    }
}
