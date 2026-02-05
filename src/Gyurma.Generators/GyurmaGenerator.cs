using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Gyurma.Generators;

[Generator]
public class GyurmaGenerator : IIncrementalGenerator
{
    private static readonly Regex InvalidIdentifierChars = new(@"[^a-zA-Z0-9_]", RegexOptions.Compiled);

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

        var members = type.GetMembers();

        var methods = isInterface
            ? members.OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToList()
            : members.OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && (m.IsAbstract || m.IsVirtual)).ToList();

        var properties = isInterface
            ? members.OfType<IPropertySymbol>().Where(p => !p.IsIndexer).ToList()
            : members.OfType<IPropertySymbol>().Where(p => !p.IsIndexer && (p.IsAbstract || p.IsVirtual)).ToList();

        var indexers = isInterface
            ? members.OfType<IPropertySymbol>().Where(p => p.IsIndexer).ToList()
            : members.OfType<IPropertySymbol>().Where(p => p.IsIndexer && (p.IsAbstract || p.IsVirtual)).ToList();

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

            if (m.Parameters.Length == 0)
            {
                sb.AppendLine($"    private {delegateType}? {MethodFieldName(m)};");
            }
            else
            {
                var keyType = TupleType(m.Parameters);
                sb.AppendLine($"    private readonly Dictionary<{keyType}, {delegateType}> {MethodFieldName(m)} = new();");
            }
        }

        // Fields for each property
        foreach (var p in properties)
        {
            sb.AppendLine($"    private Func<{FullTypeName(p.Type)}>? {PropertyFieldName(p)};");
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

            var modifier = isClass ? "public override" : "public";
            sb.AppendLine($"    {modifier} {returnType} {m.Name}({paramList})");
            sb.AppendLine("    {");

            if (m.Parameters.Length == 0)
            {
                sb.AppendLine($"        if ({MethodFieldName(m)} == null)");
                sb.AppendLine($"            throw new NotImplementedException();");
                if (isVoid)
                    sb.AppendLine($"        {MethodFieldName(m)}();");
                else
                    sb.AppendLine($"        return {MethodFieldName(m)}();");
            }
            else
            {
                var keyExpr = TupleExpr(m.Parameters);
                sb.AppendLine($"        if (!{MethodFieldName(m)}.TryGetValue({keyExpr}, out var impl))");
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
                sb.AppendLine($"            if ({PropertyFieldName(p)} == null)");
                sb.AppendLine($"                throw new NotImplementedException();");
                sb.AppendLine($"            return {PropertyFieldName(p)}();");
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
                sb.AppendLine($"            if ({PropertyFieldName(p)} == null)");
                sb.AppendLine($"                throw new NotImplementedException();");
                sb.AppendLine($"            return {PropertyFieldName(p)}();");
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

            sb.AppendLine($"        public {setupReturn} {m.Name}({paramList})");
            sb.AppendLine("        {");

            if (isVoid)
            {
                if (m.Parameters.Length == 0)
                {
                    sb.AppendLine($"            return new VoidMethodSetup(__impl => _mock.{MethodFieldName(m)} = __impl);");
                }
                else
                {
                    var keyExpr = TupleExpr(m.Parameters);
                    sb.AppendLine($"            return new VoidMethodSetup(__impl => _mock.{MethodFieldName(m)}[{keyExpr}] = __impl);");
                }
            }
            else
            {
                if (m.Parameters.Length == 0)
                {
                    sb.AppendLine($"            return new MethodSetup<{FullTypeName(m.ReturnType)}>(__impl => _mock.{MethodFieldName(m)} = __impl);");
                }
                else
                {
                    var keyExpr = TupleExpr(m.Parameters);
                    sb.AppendLine($"            return new MethodSetup<{FullTypeName(m.ReturnType)}>(__impl => _mock.{MethodFieldName(m)}[{keyExpr}] = __impl);");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        foreach (var p in properties)
        {
            var setupReturn = $"IMethodSetup<{FullTypeName(p.Type)}>";
            sb.AppendLine($"        public {setupReturn} {p.Name} =>");
            sb.AppendLine($"            new MethodSetup<{FullTypeName(p.Type)}>(f => _mock.{PropertyFieldName(p)} = f);");
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
            ? FullTypeName(parameters[0].Type)
            : $"({string.Join(", ", parameters.Select(p => FullTypeName(p.Type)))})";

    // Converts type name to valid C# identifier by replacing invalid chars with underscores
    internal static string SanitizeForIdentifier(string name) =>
        InvalidIdentifierChars.Replace(name, "_");

    // Generates a unique signature string for a method based on its parameter types
    // Returns empty string for parameterless methods, otherwise "_Type1_Type2_..."
    internal static string GetMethodSignature(IMethodSymbol method) =>
        method.Parameters.Length == 0
            ? ""
            : "_" + string.Join("_", method.Parameters.Select(p => SanitizeForIdentifier(FullTypeName(p.Type))));

    // Generates the internal field name for storing a method's setup delegate
    private static string MethodFieldName(IMethodSymbol method) =>
        $"_setup_{method.Name}{GetMethodSignature(method)}";

    // Properties cannot be overloaded, so no signature needed - just the name
    private static string PropertyFieldName(IPropertySymbol property) =>
        $"_setup_{property.Name}";

    // Generates a unique signature string for an indexer based on its parameter types
    internal static string GetIndexerSignature(IPropertySymbol indexer) =>
        string.Join("_", indexer.Parameters.Select(p => SanitizeForIdentifier(FullTypeName(p.Type))));

    private static string IndexerFieldName(IPropertySymbol indexer) =>
        $"_setup_Indexer_{GetIndexerSignature(indexer)}";

    private static string TupleExpr(ImmutableArray<IParameterSymbol> parameters) =>
        parameters.Length == 1
            ? parameters[0].Name
            : $"({string.Join(", ", parameters.Select(p => p.Name))})";
}
