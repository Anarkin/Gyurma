using Gyurma;
using Gyurma.Mocks;
using Xunit;

[assembly: Gyurma(typeof(Gyurma.Tests.IOverloadedService))]

namespace Gyurma.Tests;

public interface IOverloadedService
{
    // Parameterless vs parameterized
    void Process();
    void Process(int count);

    // Same param count, different types
    void Handle(int value);
    void Handle(string value);
    void Handle(double value);

    // Different parameter order
    void Transform(int a, string b);
    void Transform(string a, int b);

    // Return type overloads
    int Compute(int x);
    int Compute(int x, int y);
}

public class OverloadTests
{
    [Fact]
    public void Parameterless_and_parameterized_overloads_work()
    {
        var svc = new IOverloadedServiceGyurma();
        svc.Setup.Process();
        svc.Setup.Process(5);

        svc.Process();    // no-op
        svc.Process(5);   // no-op
    }

    [Fact]
    public void Same_param_count_different_types_work()
    {
        var svc = new IOverloadedServiceGyurma();
        svc.Setup.Handle(42);
        svc.Setup.Handle("test");
        svc.Setup.Handle(3.14);

        svc.Handle(42);
        svc.Handle("test");
        svc.Handle(3.14);
    }

    [Fact]
    public void Different_param_order_works()
    {
        var svc = new IOverloadedServiceGyurma();
        svc.Setup.Transform(1, "a");
        svc.Setup.Transform("b", 2);

        svc.Transform(1, "a");
        svc.Transform("b", 2);
    }

    [Fact]
    public void Return_type_overloads_work()
    {
        var svc = new IOverloadedServiceGyurma();
        svc.Setup.Compute(5).Returns(10);
        svc.Setup.Compute(2, 3).Returns(6);

        Assert.Equal(10, svc.Compute(5));
        Assert.Equal(6, svc.Compute(2, 3));
    }
}
