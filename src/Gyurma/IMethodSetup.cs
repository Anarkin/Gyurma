namespace Gyurma;

public interface IMethodSetup<in T>
{
    void Returns(T value);
    void Returns(Func<T> valueProvider);
    void Throws<TException>() where TException : Exception, new();
    void Throws(Exception exception);
}
