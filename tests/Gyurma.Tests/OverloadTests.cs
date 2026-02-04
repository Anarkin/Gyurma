using Gyurma;
using Gyurma.Mocks;
using Xunit;

[assembly: Gyurma(typeof(IOverloaded))]

public interface IOverloaded
{
    int DoSomething();
    int DoSomething(int a);
}

public class OverloadTests
{
    [Fact]
    public void Overloads_work_independently()
    {
        var mock = new IOverloadedGyurma();
        mock.Setup.DoSomething().Returns(1);
        mock.Setup.DoSomething(10).Returns(2);

        Assert.Equal(1, mock.DoSomething());
        Assert.Equal(2, mock.DoSomething(10));
    }
}
