using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Gyurma.Generators;
using Xunit;

namespace Gyurma.Tests;

public class SignatureTests
{
    private static IMethodSymbol GetMethodSymbol(string methodName, string source)
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var type = compilation.GlobalNamespace.GetTypeMembers().First();
        return type.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == methodName);
    }

    [Fact]
    public void GetMethodSignature_Parameterless_ReturnsEmpty()
    {
        var method = GetMethodSymbol("Foo", "public interface ITest { void Foo(); }");
        Assert.Equal("", GyurmaGenerator.GetMethodSignature(method));
    }

    [Fact]
    public void GetMethodSignature_SingleIntParam_ReturnsSignature()
    {
        var method = GetMethodSymbol("Foo", "public interface ITest { void Foo(int x); }");
        // Without full .NET references, types appear as keywords (int, string)
        Assert.Equal("_int", GyurmaGenerator.GetMethodSignature(method));
    }

    [Fact]
    public void GetMethodSignature_SingleStringParam_ReturnsSignature()
    {
        var method = GetMethodSymbol("Foo", "public interface ITest { void Foo(string x); }");
        Assert.Equal("_string", GyurmaGenerator.GetMethodSignature(method));
    }

    [Fact]
    public void GetMethodSignature_TwoParams_CombinesTypes()
    {
        var method = GetMethodSymbol("Foo", "public interface ITest { void Foo(int a, string b); }");
        Assert.Equal("_int_string", GyurmaGenerator.GetMethodSignature(method));
    }

    [Fact]
    public void GetMethodSignature_SameParamsDifferentOrder_ProducesDifferentSignatures()
    {
        var method1 = GetMethodSymbol("Foo", "public interface ITest { void Foo(int a, string b); }");
        var method2 = GetMethodSymbol("Foo", "public interface ITest { void Foo(string a, int b); }");

        var sig1 = GyurmaGenerator.GetMethodSignature(method1);
        var sig2 = GyurmaGenerator.GetMethodSignature(method2);

        Assert.NotEqual(sig1, sig2);
    }

    [Theory]
    [InlineData("global::System.Int32", "global__System_Int32")]
    [InlineData("global::System.String", "global__System_String")]
    [InlineData("List<int>", "List_int_")]
    [InlineData("MyType", "MyType")]
    [InlineData("A.B.C", "A_B_C")]
    public void SanitizeForIdentifier_ReplacesInvalidChars(string input, string expected)
    {
        Assert.Equal(expected, GyurmaGenerator.SanitizeForIdentifier(input));
    }
}
