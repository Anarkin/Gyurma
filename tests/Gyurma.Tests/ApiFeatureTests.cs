using Gyurma;
using Gyurma.Mocks;
using Xunit;

[assembly: Gyurma(typeof(IApiFeatures))]

public interface IApiFeatures
{
    int GetValue();
    void DoSomething();
}

public class ApiFeatureTests
{
    [Fact]
    public void Returns_Func_works()
    {
        var mock = new IApiFeaturesGyurma();
        int counter = 0;
        mock.Setup.GetValue().Returns(() => ++counter);

        Assert.Equal(1, mock.GetValue());
        Assert.Equal(2, mock.GetValue());
    }

    [Fact]
    public void Throws_Exception_instance_works()
    {
        var mock = new IApiFeaturesGyurma();
        var ex = new InvalidOperationException("Custom message");
        mock.Setup.GetValue().Throws(ex);

        var thrown = Assert.Throws<InvalidOperationException>(() => mock.GetValue());
        Assert.Same(ex, thrown);
    }

    [Fact]
    public void Void_Throws_Exception_instance_works()
    {
        var mock = new IApiFeaturesGyurma();
        var ex = new InvalidOperationException("Custom message void");
        mock.Setup.DoSomething().Throws(ex);

        var thrown = Assert.Throws<InvalidOperationException>(() => mock.DoSomething());
        Assert.Same(ex, thrown);
    }
}
