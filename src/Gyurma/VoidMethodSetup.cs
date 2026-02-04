namespace Gyurma;

public class VoidMethodSetup : IVoidMethodSetup
{
    private readonly Action<Action> _register;

    public VoidMethodSetup(Action<Action> register)
    {
        _register = register;
        _register(() => { });
    }

    public void Throws<TException>() where TException : Exception, new() => _register(() => throw new TException());

    public void Throws(Exception exception) => _register(() => throw exception);
}
