using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Gyurma.Generators;

[Generator]
public class GyurmaGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var attributes = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: (node, _) => node is AttributeSyntax attr &&
                                    attr.Parent is AttributeListSyntax list &&
                                    list.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) == true,
            transform: (ctx, _) =>
            {
                var attr = (AttributeSyntax)ctx.Node;
                if (attr.ArgumentList?.Arguments.Count == 1 &&
                    attr.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax typeOf)
                {
                    var typeInfo = ctx.SemanticModel.GetTypeInfo(typeOf.Type);
                    if (typeInfo.Type is INamedTypeSymbol namedType)
                    {
                        // Verify the attribute is actually GyurmaAttribute
                        var symbol = ctx.SemanticModel.GetSymbolInfo(attr).Symbol;
                        if (symbol?.ContainingType?.ToDisplayString() == "Gyurma.GyurmaAttribute")
                            return namedType;
                    }
                }
                return null;
            })
            .Where(t => t != null)
            .Select((t, _) => t!);

        context.RegisterSourceOutput(attributes.Collect(), Generate);
    }

    private static void Generate(SourceProductionContext ctx, ImmutableArray<INamedTypeSymbol> types)
    {
        foreach (var type in types.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
        {
            if (type.TypeKind == TypeKind.Class && !type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility >= Accessibility.Protected))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("GYURMA001", "No parameterless constructor",
                        $"Type '{type.Name}' has no accessible parameterless constructor and cannot be mocked.",
                        "Gyurma", DiagnosticSeverity.Error, true),
                    Location.None));
                continue;
            }

            var source = GenerateMock(type);
            ctx.AddSource($"{type.Name}Gyurma.g.cs", source);
        }
    }

    private static string GenerateMock(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        var name = type.Name;
        var mockName = $"{name}Gyurma";
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isInterface = type.TypeKind == TypeKind.Interface;
        var isClass = type.TypeKind == TypeKind.Class;

        var methods = isInterface
            ? type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToList()
            : type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && (m.IsAbstract || m.IsVirtual)).ToList();

        var properties = isInterface
            ? type.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsIndexer).ToList()
            : type.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsIndexer && (p.IsAbstract || p.IsVirtual)).ToList();

        var indexers = isInterface
            ? type.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsIndexer).ToList()
            : type.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsIndexer && (p.IsAbstract || p.IsVirtual)).ToList();

        // Calculate unique field names for methods
        var methodFields = new Dictionary<IMethodSymbol, string>(SymbolEqualityComparer.Default);
        foreach (var group in methods.GroupBy(m => m.Name))
        {
            if (group.Count() == 1)
            {
                methodFields[group.First()] = $"_setup_{group.First().Name}";
            }
            else
            {
                int i = 0;
                foreach (var m in group)
                {
                    methodFields[m] = $"_setup_{m.Name}_{i++}";
                }
            }
        }

        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Gyurma;");
        sb.AppendLine();
        sb.AppendLine("namespace Gyurma.Mocks;");
        sb.AppendLine();
        sb.AppendLine($"public class {mockName} : {fullName}");
        sb.AppendLine("{");

        // Fields for each method
        foreach (var m in methods)
        {
            var isVoid = m.ReturnType.SpecialType == SpecialType.System_Void;
            var delegateType = isVoid ? "Action" : $"Func<{FullTypeName(m.ReturnType)}>";
            var fieldName = methodFields[m];

            if (m.Parameters.Length == 0)
            {
                sb.AppendLine($"    private {delegateType}? {fieldName};");
            }
            else
            {
                var keyType = TupleType(m.Parameters);
                sb.AppendLine($"    private readonly Dictionary<{keyType}, {delegateType}> {fieldName} = new();");
            }
        }

        // Fields for each property
        foreach (var p in properties)
        {
            sb.AppendLine($"    private Func<{FullTypeName(p.Type)}>? _setup_{p.Name};");
        }

        // Fields for each indexer
        foreach (var idx in indexers)
        {
            var fieldName = IndexerFieldName(idx);
            var keyType = TupleType(idx.Parameters);
            sb.AppendLine($"    private readonly Dictionary<{keyType}, Func<{FullTypeName(idx.Type)}>> {fieldName} = new();");
        }

        sb.AppendLine();

        // Setup property
        sb.AppendLine($"    public {mockName}Setup Setup {{ get; }}");
        sb.AppendLine();

        // Constructor
        var baseCall = isClass ? " : base()" : "";
        sb.AppendLine($"    public {mockName}(){baseCall}");
        sb.AppendLine("    {");
        sb.AppendLine("        Setup = new(this);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Interface implementation
        foreach (var m in methods)
        {
            var isVoid = m.ReturnType.SpecialType == SpecialType.System_Void;
            var returnType = isVoid ? "void" : FullTypeName(m.ReturnType);
            var paramList = string.Join(", ", m.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));
            var fieldName = methodFields[m];

            var modifier = isClass ? "public override" : "public";
            sb.AppendLine($"    {modifier} {returnType} {m.Name}({paramList})");
            sb.AppendLine("    {");

            if (m.Parameters.Length == 0)
            {
                sb.AppendLine($"        if ({fieldName} == null)");
                sb.AppendLine($"            throw new NotImplementedException();");
                if (isVoid)
                    sb.AppendLine($"        {fieldName}();");
                else
                    sb.AppendLine($"        return {fieldName}();");
            }
            else
            {
                var keyExpr = TupleExpr(m.Parameters);
                sb.AppendLine($"        if (!{fieldName}.TryGetValue({keyExpr}, out var impl))");
                sb.AppendLine($"            throw new NotImplementedException();");
                if (isVoid)
                    sb.AppendLine($"        impl();");
                else
                    sb.AppendLine($"        return impl();");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Property implementations
        foreach (var p in properties)
        {
            var returnType = FullTypeName(p.Type);
            var modifier = isClass ? "public override" : "public";

            if (p.SetMethod != null)
            {
                sb.AppendLine($"    {modifier} {returnType} {p.Name}");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (_setup_{p.Name} == null)");
                sb.AppendLine($"                throw new NotImplementedException();");
                sb.AppendLine($"            return _setup_{p.Name}();");
                sb.AppendLine("        }");
                sb.AppendLine($"        set {{ }}");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    {modifier} {returnType} {p.Name}");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (_setup_{p.Name} == null)");
                sb.AppendLine($"                throw new NotImplementedException();");
                sb.AppendLine($"            return _setup_{p.Name}();");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
            }

            sb.AppendLine();
        }

        // Indexer implementations
        foreach (var idx in indexers)
        {
            var returnType = FullTypeName(idx.Type);
            var modifier = isClass ? "public override" : "public";
            var paramList = string.Join(", ", idx.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));
            var fieldName = IndexerFieldName(idx);
            var keyExpr = TupleExpr(idx.Parameters);

            sb.AppendLine($"    {modifier} {returnType} this[{paramList}]");
            sb.AppendLine("    {");
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (!{fieldName}.TryGetValue({keyExpr}, out var impl))");
            sb.AppendLine($"                throw new NotImplementedException();");
            sb.AppendLine($"            return impl();");
            sb.AppendLine("        }");
            if (idx.SetMethod != null)
            {
                sb.AppendLine($"        set {{ }}");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Setup class
        sb.AppendLine($"    public class {mockName}Setup");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {mockName} _mock;");
        sb.AppendLine();
        sb.AppendLine($"        public {mockName}Setup({mockName} mock)");
        sb.AppendLine("        {");
        sb.AppendLine("            _mock = mock;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var m in methods)
        {
            var isVoid = m.ReturnType.SpecialType == SpecialType.System_Void;
            var setupReturn = isVoid ? "IVoidMethodSetup" : $"IMethodSetup<{FullTypeName(m.ReturnType)}>";
            var paramList = string.Join(", ", m.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));
            var fieldName = methodFields[m];

            sb.AppendLine($"        public {setupReturn} {m.Name}({paramList})");
            sb.AppendLine("        {");

            if (isVoid)
            {
                if (m.Parameters.Length == 0)
                {
                    sb.AppendLine($"            return new VoidMethodSetup(a => _mock.{fieldName} = a);");
                }
                else
                {
                    var keyExpr = TupleExpr(m.Parameters);
                    sb.AppendLine($"            return new VoidMethodSetup(a => _mock.{fieldName}[{keyExpr}] = a);");
                }
            }
            else
            {
                if (m.Parameters.Length == 0)
                {
                    sb.AppendLine($"            return new MethodSetup<{FullTypeName(m.ReturnType)}>(f => _mock.{fieldName} = f);");
                }
                else
                {
                    var keyExpr = TupleExpr(m.Parameters);
                    sb.AppendLine($"            return new MethodSetup<{FullTypeName(m.ReturnType)}>(f => _mock.{fieldName}[{keyExpr}] = f);");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        foreach (var p in properties)
        {
            var setupReturn = $"IMethodSetup<{FullTypeName(p.Type)}>";
            sb.AppendLine($"        public {setupReturn} {p.Name} =>");
            sb.AppendLine($"            new MethodSetup<{FullTypeName(p.Type)}>(f => _mock._setup_{p.Name} = f);");
            sb.AppendLine();
        }

        foreach (var idx in indexers)
        {
            var setupReturn = $"IMethodSetup<{FullTypeName(idx.Type)}>";
            var paramList = string.Join(", ", idx.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));
            var fieldName = IndexerFieldName(idx);
            var keyExpr = TupleExpr(idx.Parameters);

            sb.AppendLine($"        public {setupReturn} this[{paramList}] =>");
            sb.AppendLine($"            new MethodSetup<{FullTypeName(idx.Type)}>(f => _mock.{fieldName}[{keyExpr}] = f);");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string FullTypeName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string TupleType(ImmutableArray<IParameterSymbol> parameters) =>
        parameters.Length == 1
            ? $"({FullTypeName(parameters[0].Type)}, byte)"
            : $"({string.Join(", ", parameters.Select(p => FullTypeName(p.Type)))})";

    private static string IndexerFieldName(IPropertySymbol indexer) =>
        "_setup_Indexer_" + string.Join("_", indexer.Parameters.Select(p => p.Type.Name));

    private static string TupleExpr(ImmutableArray<IParameterSymbol> parameters) =>
        parameters.Length == 1
            ? $"({parameters[0].Name}, 0)"
            : $"({string.Join(", ", parameters.Select(p => p.Name))})";
}
