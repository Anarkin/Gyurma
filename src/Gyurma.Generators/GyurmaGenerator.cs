using System.Collections.Generic;
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
                    // For open generic types like typeof(IRepository<>), use GetSymbolInfo instead of GetTypeInfo
                    var symbolInfo = ctx.SemanticModel.GetSymbolInfo(typeOf.Type);
                    if (symbolInfo.Symbol is INamedTypeSymbol namedType)
                    {
                        // Verify the attribute is actually GyurmaAttribute
                        var attrSymbol = ctx.SemanticModel.GetSymbolInfo(attr).Symbol;
                        if (attrSymbol?.ContainingType?.ToDisplayString() == "Gyurma.GyurmaAttribute")
                        {
                            // For open generic types, get the original definition
                            return namedType.IsUnboundGenericType ? namedType.OriginalDefinition : namedType;
                        }
                    }
                    // Fallback for non-generic types
                    var typeInfo = ctx.SemanticModel.GetTypeInfo(typeOf.Type);
                    if (typeInfo.Type is INamedTypeSymbol namedType2)
                    {
                        var attrSymbol = ctx.SemanticModel.GetSymbolInfo(attr).Symbol;
                        if (attrSymbol?.ContainingType?.ToDisplayString() == "Gyurma.GyurmaAttribute")
                        {
                            return namedType2;
                        }
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
            // For generic types, include arity in filename to avoid conflicts
            var fileName = type.TypeParameters.Length > 0
                ? $"{type.Name}Gyurma_{type.TypeParameters.Length}.g.cs"
                : $"{type.Name}Gyurma.g.cs";
            ctx.AddSource(fileName, source);
        }
    }

    private static string GenerateMock(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        var name = type.Name;
        var mockName = $"{name}Gyurma";
        var isInterface = type.TypeKind == TypeKind.Interface;
        var isClass = type.TypeKind == TypeKind.Class;

        // Handle generic type parameters
        var typeParamList = GetTypeParameterList(type.TypeParameters);
        var typeConstraints = BuildConstraintClauses(type.TypeParameters);
        var baseType = GetFullyQualifiedNameWithTypeParams(type);

        // type.GetMembers() only returns directly declared members, so we must also gather:
        // - For interfaces: members from all base interfaces
        // - For classes: inherited abstract members from the base type chain
        var allMembers = isInterface
            ? type.GetMembers().Concat(type.AllInterfaces.SelectMany(i => i.GetMembers()))
            : type.GetMembers().Concat(GetInheritedAbstractMembers(type));

        var methods = isInterface
            ? allMembers.OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary)
                .GroupBy(m => MethodFieldName(m)).Select(g => g.First()).ToList()
            : allMembers.OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && (m.IsAbstract || m.IsVirtual)).ToList();

        var properties = isInterface
            ? allMembers.OfType<IPropertySymbol>().Where(p => !p.IsIndexer)
                .GroupBy(p => PropertyFieldName(p)).Select(g => g.FirstOrDefault(p => p.SetMethod != null) ?? g.First()).ToList()
            : allMembers.OfType<IPropertySymbol>().Where(p => !p.IsIndexer && (p.IsAbstract || p.IsVirtual)).ToList();

        var indexers = isInterface
            ? allMembers.OfType<IPropertySymbol>().Where(p => p.IsIndexer)
                .GroupBy(p => IndexerFieldName(p)).Select(g => g.FirstOrDefault(p => p.SetMethod != null) ?? g.First()).ToList()
            : allMembers.OfType<IPropertySymbol>().Where(p => p.IsIndexer && (p.IsAbstract || p.IsVirtual)).ToList();

        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Gyurma;");
        sb.AppendLine();
        sb.AppendLine("namespace Gyurma.Mocks;");
        sb.AppendLine();
        sb.AppendLine($"public class {mockName}{typeParamList} : {baseType}{typeConstraints}");
        sb.AppendLine("{");

        // Fields for each method
        foreach (var m in methods)
        {
            var isVoid = m.ReturnType.SpecialType == SpecialType.System_Void;
            var isGenericMethod = m.TypeParameters.Length > 0;

            // For generic methods, always use object return type and include Type[] in the key
            var delegateType = isVoid ? "Action" : (isGenericMethod ? "Func<object?>" : $"Func<{FullTypeName(m.ReturnType)}>");

            if (m.Parameters.Length == 0 && !isGenericMethod)
            {
                sb.AppendLine($"    private {delegateType}? {MethodFieldName(m)};");
                sb.AppendLine($"    private int {MethodCallCountFieldName(m)};");
            }
            else
            {
                var keyType = GetMethodKeyType(m);
                sb.AppendLine($"    private readonly Dictionary<{keyType}, {delegateType}> {MethodFieldName(m)} = new();");
                sb.AppendLine($"    private readonly Dictionary<{keyType}, int> {MethodCallCountFieldName(m)} = new();");
            }
        }

        // Fields for each property
        foreach (var p in properties)
        {
            sb.AppendLine($"    private Func<{FullTypeName(p.Type)}>? {PropertyFieldName(p)};");
            sb.AppendLine($"    private int {PropertyCallCountFieldName(p)};");
        }

        // Fields for each indexer
        foreach (var idx in indexers)
        {
            var fieldName = IndexerFieldName(idx);
            var keyType = TupleType(idx.Parameters);
            sb.AppendLine($"    private readonly Dictionary<{keyType}, Func<{FullTypeName(idx.Type)}>> {fieldName} = new();");
            sb.AppendLine($"    private readonly Dictionary<{keyType}, int> {IndexerCallCountFieldName(idx)} = new();");
        }

        sb.AppendLine();

        // Setup and CallCounts properties
        sb.AppendLine($"    public {mockName}Setup Setup {{ get; }}");
        sb.AppendLine($"    public {mockName}CallCounts CallCounts {{ get; }}");
        sb.AppendLine();

        // Constructor
        var baseCall = isClass ? " : base()" : "";
        sb.AppendLine($"    public {mockName}(){baseCall}");
        sb.AppendLine("    {");
        sb.AppendLine("        Setup = new(this);");
        sb.AppendLine("        CallCounts = new(this);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Interface implementation
        foreach (var m in methods)
        {
            var isVoid = m.ReturnType.SpecialType == SpecialType.System_Void;
            var isGenericMethod = m.TypeParameters.Length > 0;
            var methodTypeParamList = GetTypeParameterList(m.TypeParameters);
            var methodConstraints = BuildConstraintClauses(m.TypeParameters);
            var returnType = isVoid ? "void" : FullTypeName(m.ReturnType);
            var paramList = string.Join(", ", m.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));

            var modifier = isClass ? "public override" : "public";
            sb.AppendLine($"    {modifier} {returnType} {m.Name}{methodTypeParamList}({paramList}){methodConstraints}");
            sb.AppendLine("    {");

            if (m.Parameters.Length == 0 && !isGenericMethod)
            {
                sb.AppendLine($"        {MethodCallCountFieldName(m)}++;");
                sb.AppendLine($"        if ({MethodFieldName(m)} == null)");
                sb.AppendLine($"            throw new NotImplementedException();");
                if (isVoid)
                {
                    sb.AppendLine($"        {MethodFieldName(m)}();");
                }
                else
                {
                    sb.AppendLine($"        return {MethodFieldName(m)}();");
                }
            }
            else
            {
                var keyExpr = GetMethodKeyExpr(m);
                sb.AppendLine($"        var __key = {keyExpr};");
                sb.AppendLine($"        {MethodCallCountFieldName(m)}[__key] = {MethodCallCountFieldName(m)}.TryGetValue(__key, out var __cc) ? __cc + 1 : 1;");
                sb.AppendLine($"        if (!{MethodFieldName(m)}.TryGetValue(__key, out var impl))");
                sb.AppendLine($"            throw new NotImplementedException();");
                if (isVoid)
                {
                    sb.AppendLine($"        impl();");
                }
                else if (isGenericMethod)
                {
                    sb.AppendLine($"        return ({returnType})impl()!;");
                }
                else
                {
                    sb.AppendLine($"        return impl();");
                }
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
                var setter = p.SetMethod.IsInitOnly ? "init" : "set";
                sb.AppendLine($"    {modifier} {returnType} {p.Name}");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            {PropertyCallCountFieldName(p)}++;");
                sb.AppendLine($"            if ({PropertyFieldName(p)} == null)");
                sb.AppendLine($"                throw new NotImplementedException();");
                sb.AppendLine($"            return {PropertyFieldName(p)}();");
                sb.AppendLine("        }");
                sb.AppendLine($"        {setter} {{ }}");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    {modifier} {returnType} {p.Name}");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            {PropertyCallCountFieldName(p)}++;");
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

            var ccFieldName = IndexerCallCountFieldName(idx);
            sb.AppendLine($"    {modifier} {returnType} this[{paramList}]");
            sb.AppendLine("    {");
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine($"            {ccFieldName}[{keyExpr}] = {ccFieldName}.TryGetValue({keyExpr}, out var __cc) ? __cc + 1 : 1;");
            sb.AppendLine($"            if (!{fieldName}.TryGetValue({keyExpr}, out var impl))");
            sb.AppendLine($"                throw new NotImplementedException();");
            sb.AppendLine($"            return impl();");
            sb.AppendLine("        }");
            if (idx.SetMethod != null)
            {
                var setter = idx.SetMethod.IsInitOnly ? "init" : "set";
                sb.AppendLine($"        {setter} {{ }}");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Setup class - nested class inherits type parameters from outer class, no need to redeclare
        sb.AppendLine($"    public class {mockName}Setup");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {mockName}{typeParamList} _mock;");
        sb.AppendLine();
        sb.AppendLine($"        public {mockName}Setup({mockName}{typeParamList} mock)");
        sb.AppendLine("        {");
        sb.AppendLine("            _mock = mock;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var m in methods)
        {
            var isVoid = m.ReturnType.SpecialType == SpecialType.System_Void;
            var isGenericMethod = m.TypeParameters.Length > 0;
            var methodTypeParamList = GetTypeParameterList(m.TypeParameters);
            var methodConstraints = BuildConstraintClauses(m.TypeParameters);
            var setupReturn = isVoid ? "IVoidMethodSetup" : $"IMethodSetup<{FullTypeName(m.ReturnType)}>";
            var paramList = string.Join(", ", m.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));

            sb.AppendLine($"        public {setupReturn} {m.Name}{methodTypeParamList}({paramList}){methodConstraints}");
            sb.AppendLine("        {");

            if (isVoid)
            {
                if (m.Parameters.Length == 0 && !isGenericMethod)
                {
                    sb.AppendLine($"            return new VoidMethodSetup(__impl => _mock.{MethodFieldName(m)} = __impl);");
                }
                else
                {
                    var keyExpr = GetMethodKeyExpr(m);
                    sb.AppendLine($"            return new VoidMethodSetup(__impl => _mock.{MethodFieldName(m)}[{keyExpr}] = __impl);");
                }
            }
            else
            {
                if (m.Parameters.Length == 0 && !isGenericMethod)
                {
                    sb.AppendLine($"            return new MethodSetup<{FullTypeName(m.ReturnType)}>(__impl => _mock.{MethodFieldName(m)} = __impl);");
                }
                else if (isGenericMethod)
                {
                    var keyExpr = GetMethodKeyExpr(m);
                    sb.AppendLine($"            return new MethodSetup<{FullTypeName(m.ReturnType)}>(__impl => _mock.{MethodFieldName(m)}[{keyExpr}] = () => __impl());");
                }
                else
                {
                    var keyExpr = GetMethodKeyExpr(m);
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
        sb.AppendLine();

        // CallCounts class
        sb.AppendLine($"    public class {mockName}CallCounts");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {mockName}{typeParamList} _mock;");
        sb.AppendLine();
        sb.AppendLine($"        public {mockName}CallCounts({mockName}{typeParamList} mock)");
        sb.AppendLine("        {");
        sb.AppendLine("            _mock = mock;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var m in methods)
        {
            var isGenericMethod = m.TypeParameters.Length > 0;
            var methodTypeParamList = GetTypeParameterList(m.TypeParameters);
            var methodConstraints = BuildConstraintClauses(m.TypeParameters);
            var paramList = string.Join(", ", m.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));

            sb.AppendLine($"        public int {m.Name}{methodTypeParamList}({paramList}){methodConstraints}");
            sb.AppendLine("        {");

            if (m.Parameters.Length == 0 && !isGenericMethod)
            {
                sb.AppendLine($"            return _mock.{MethodCallCountFieldName(m)};");
            }
            else
            {
                var keyExpr = GetMethodKeyExpr(m);
                sb.AppendLine($"            return _mock.{MethodCallCountFieldName(m)}.TryGetValue({keyExpr}, out var c) ? c : 0;");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        foreach (var p in properties)
        {
            sb.AppendLine($"        public int {p.Name} => _mock.{PropertyCallCountFieldName(p)};");
            sb.AppendLine();
        }

        foreach (var idx in indexers)
        {
            var paramList = string.Join(", ", idx.Parameters.Select(p => $"{FullTypeName(p.Type)} {p.Name}"));
            var keyExpr = TupleExpr(idx.Parameters);

            sb.AppendLine($"        public int this[{paramList}] => _mock.{IndexerCallCountFieldName(idx)}.TryGetValue({keyExpr}, out var c) ? c : 0;");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string FullTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string TupleType(ImmutableArray<IParameterSymbol> parameters)
    {
        return parameters.Length == 1
            ? FullTypeName(parameters[0].Type)
            : $"({string.Join(", ", parameters.Select(p => FullTypeName(p.Type)))})";
    }

    // Converts type name to valid C# identifier by replacing invalid chars with underscores
    internal static string SanitizeForIdentifier(string name)
    {
        return InvalidIdentifierChars.Replace(name, "_");
    }

    // Generates a unique signature string for a method based on its parameter types
    // Returns empty string for parameterless methods, otherwise "_Type1_Type2_..."
    internal static string GetMethodSignature(IMethodSymbol method)
    {
        return method.Parameters.Length == 0
            ? ""
            : "_" + string.Join("_", method.Parameters.Select(p => SanitizeForIdentifier(FullTypeName(p.Type))));
    }

    // Generates the internal field name for storing a method's setup delegate
    private static string MethodFieldName(IMethodSymbol method)
    {
        return $"_setup_{method.Name}{GetMethodTypeArity(method)}{GetMethodSignature(method)}";
    }

    // Properties cannot be overloaded, so no signature needed - just the name
    private static string PropertyFieldName(IPropertySymbol property)
    {
        return $"_setup_{property.Name}";
    }

    // Generates a unique signature string for an indexer based on its parameter types
    internal static string GetIndexerSignature(IPropertySymbol indexer)
    {
        return string.Join("_", indexer.Parameters.Select(p => SanitizeForIdentifier(FullTypeName(p.Type))));
    }

    private static string IndexerFieldName(IPropertySymbol indexer)
    {
        return $"_setup_Indexer_{GetIndexerSignature(indexer)}";
    }

    // Call count field name helpers (mirror setup field names with _callCount_ prefix)
    private static string MethodCallCountFieldName(IMethodSymbol method)
    {
        return $"_callCount_{method.Name}{GetMethodTypeArity(method)}{GetMethodSignature(method)}";
    }

    private static string PropertyCallCountFieldName(IPropertySymbol property)
    {
        return $"_callCount_{property.Name}";
    }

    private static string IndexerCallCountFieldName(IPropertySymbol indexer)
    {
        return $"_callCount_Indexer_{GetIndexerSignature(indexer)}";
    }

    private static string TupleExpr(ImmutableArray<IParameterSymbol> parameters)
    {
        return parameters.Length == 1
            ? parameters[0].Name
            : $"({string.Join(", ", parameters.Select(p => p.Name))})";
    }

    // Get type parameter list: "" or "<T>" or "<TKey, TValue>"
    private static string GetTypeParameterList(ImmutableArray<ITypeParameterSymbol> typeParams)
    {
        if (typeParams.Length == 0)
        {
            return "";
        }

        return "<" + string.Join(", ", typeParams.Select(tp => tp.Name)) + ">";
    }

    // Build constraint clauses: " where T : class, new()"
    private static string BuildConstraintClauses(ImmutableArray<ITypeParameterSymbol> typeParams)
    {
        var clauses = new List<string>();
        foreach (var tp in typeParams)
        {
            var constraints = new List<string>();
            if (tp.HasReferenceTypeConstraint)
            {
                constraints.Add("class");
            }

            if (tp.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }

            if (tp.HasUnmanagedTypeConstraint)
            {
                constraints.Add("unmanaged");
            }

            if (tp.HasNotNullConstraint)
            {
                constraints.Add("notnull");
            }

            foreach (var c in tp.ConstraintTypes)
            {
                constraints.Add(FullTypeName(c));
            }

            if (tp.HasConstructorConstraint)
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                clauses.Add($"where {tp.Name} : {string.Join(", ", constraints)}");
            }
        }
        return clauses.Count > 0 ? " " + string.Join(" ", clauses) : "";
    }

    // Get fully qualified name preserving type parameters (for base type declaration)
    private static string GetFullyQualifiedNameWithTypeParams(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        // Build the fully qualified name with type parameter names
        var ns = type.ContainingNamespace;
        string prefix;
        if (ns == null || ns.IsGlobalNamespace)
        {
            prefix = "global::";
        }
        else
        {
            prefix = $"global::{ns.ToDisplayString()}.";
        }

        var typeParams = string.Join(", ", type.TypeParameters.Select(tp => tp.Name));
        return $"{prefix}{type.Name}<{typeParams}>";
    }

    // Get the dictionary key type for a method (includes Type[] for generic methods)
    private static string GetMethodKeyType(IMethodSymbol method)
    {
        var parts = new List<string>();

        // Add Type for each type parameter
        for (var i = 0; i < method.TypeParameters.Length; i++)
        {
            parts.Add("Type");
        }

        // Add parameter types - convert method type parameters to object
        foreach (var p in method.Parameters)
        {
            parts.Add(GetParameterTypeForKey(p.Type, method.TypeParameters));
        }

        return parts.Count == 1 ? parts[0] : $"({string.Join(", ", parts)})";
    }

    // Get the type name for a parameter in the key type - method type parameters become object
    private static string GetParameterTypeForKey(ITypeSymbol type, ImmutableArray<ITypeParameterSymbol> methodTypeParams)
    {
        // If the type is a method type parameter, use object
        if (type is ITypeParameterSymbol tp && methodTypeParams.Contains(tp, SymbolEqualityComparer.Default))
        {
            return "object";
        }

        return FullTypeName(type);
    }

    // Get the key expression for a method call (includes typeof(T) for generic methods)
    private static string GetMethodKeyExpr(IMethodSymbol method)
    {
        var parts = new List<string>();

        // Add typeof(T) for each type parameter
        foreach (var tp in method.TypeParameters)
        {
            parts.Add($"typeof({tp.Name})");
        }

        // Add parameter names
        foreach (var p in method.Parameters)
        {
            parts.Add(p.Name);
        }

        return parts.Count == 1 ? parts[0] : $"({string.Join(", ", parts)})";
    }

    // Get method type arity suffix for field names (to distinguish generic overloads)
    private static string GetMethodTypeArity(IMethodSymbol method)
    {
        return method.TypeParameters.Length > 0 ? $"_T{method.TypeParameters.Length}" : "";
    }

    // Walks the base type chain and collects abstract/virtual members that haven't been overridden
    private static IEnumerable<ISymbol> GetInheritedAbstractMembers(INamedTypeSymbol type)
    {
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        // Collect what the target type itself overrides
        foreach (var m in type.GetMembers())
        {
            if (m is IMethodSymbol ms && ms.OverriddenMethod != null)
            {
                overridden.Add(ms.OverriddenMethod);
            }
            else if (m is IPropertySymbol ps && ps.OverriddenProperty != null)
            {
                overridden.Add(ps.OverriddenProperty);
            }
        }

        var current = type.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IMethodSymbol m && m.MethodKind == MethodKind.Ordinary)
                {
                    if ((m.IsAbstract || m.IsVirtual) && !overridden.Contains(m))
                    {
                        yield return m;
                    }

                    if (m.OverriddenMethod != null)
                    {
                        overridden.Add(m.OverriddenMethod);
                    }
                }
                else if (member is IPropertySymbol p)
                {
                    if ((p.IsAbstract || p.IsVirtual) && !overridden.Contains(p))
                    {
                        yield return p;
                    }

                    if (p.OverriddenProperty != null)
                    {
                        overridden.Add(p.OverriddenProperty);
                    }
                }
            }
            current = current.BaseType;
        }
    }
}
