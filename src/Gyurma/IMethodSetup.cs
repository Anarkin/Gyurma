namespace Gyurma;

public interface IMethodSetup<in T>
{
    void Returns(T value);
    void Throws<TException>() where TException : Exception, new();
}
