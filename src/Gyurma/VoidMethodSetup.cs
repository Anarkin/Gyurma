namespace Gyurma;

public class VoidMethodSetup : IVoidMethodSetup
{
    private readonly Action<Action> _register;

    public VoidMethodSetup(Action<Action> register)
    {
        this._register = register;
        this._register(() => { });
    }

    public void Throws<TException>() where TException : Exception, new()
    {
        this._register(() => throw new TException());
    }
}
