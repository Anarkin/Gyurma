namespace Gyurma;

public interface IVoidMethodSetup
{
    void Throws<TException>() where TException : Exception, new();
}
