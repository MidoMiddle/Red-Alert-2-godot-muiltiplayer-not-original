using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Rts.Analyzers;

/// <summary>
/// Reports code that breaks simulation determinism and is not caught by the banned-API list:
/// RTS0001 — any floating-point value (float, double, decimal), including keywords and literals;
/// RTS0002 — enumeration of Dictionary / HashSet, whose order is not guaranteed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
{
    public const string FloatingPointId = "RTS0001";
    public const string UnorderedEnumerationId = "RTS0002";

    private const string Category = "Determinism";

    private static readonly DiagnosticDescriptor FloatingPointRule = new(
        FloatingPointId,
        "Floating point in simulation code",
        "'{0}' is a floating-point type; floating point is not deterministic across CPUs, use Fixed or integers",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnorderedEnumerationRule = new(
        UnorderedEnumerationId,
        "Unordered enumeration in simulation code",
        "Enumerating '{0}' has no guaranteed order; use List, sort by id, or SortedDictionary/SortedSet",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(FloatingPointRule, UnorderedEnumerationRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzePredefinedType, SyntaxKind.PredefinedType);
        context.RegisterOperationAction(AnalyzeLiteral, OperationKind.Literal);
        context.RegisterOperationAction(AnalyzeForEach, OperationKind.Loop);
        context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
    }

    // float x; double M(); decimal[] a; (float)y — every spelled-out floating-point keyword.
    private static void AnalyzePredefinedType(SyntaxNodeAnalysisContext context)
    {
        var node = (PredefinedTypeSyntax)context.Node;
        var kind = node.Keyword.Kind();
        if (kind is SyntaxKind.FloatKeyword or SyntaxKind.DoubleKeyword or SyntaxKind.DecimalKeyword)
        {
            context.ReportDiagnostic(Diagnostic.Create(FloatingPointRule, node.GetLocation(), node.Keyword.Text));
        }
    }

    // var x = 1.5; — a floating-point literal without any keyword in sight.
    private static void AnalyzeLiteral(OperationAnalysisContext context)
    {
        var type = context.Operation.Type;
        if (type is not null && IsFloatingPoint(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                FloatingPointRule, context.Operation.Syntax.GetLocation(), type.ToDisplayString()));
        }
    }

    // foreach (var kv in dictionary) — uses the GetEnumerator pattern, so no conversion is involved.
    private static void AnalyzeForEach(OperationAnalysisContext context)
    {
        if (context.Operation is not IForEachLoopOperation loop)
        {
            return;
        }

        var collectionType = UnwrapConversion(loop.Collection).Type;
        if (collectionType is not null && IsUnorderedCollection(collectionType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnorderedEnumerationRule, loop.Collection.Syntax.GetLocation(), collectionType.ToDisplayString()));
        }
    }

    // dictionary.Select(...), new List<T>(hashSet) — the collection is passed on as IEnumerable.
    private static void AnalyzeConversion(OperationAnalysisContext context)
    {
        var conversion = (IConversionOperation)context.Operation;
        var sourceType = conversion.Operand.Type;
        var targetType = conversion.Type;
        if (sourceType is null || targetType is null || !IsUnorderedCollection(sourceType))
        {
            return;
        }

        if (IsEnumerableInterface(targetType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnorderedEnumerationRule, conversion.Operand.Syntax.GetLocation(), sourceType.ToDisplayString()));
        }
    }

    private static IOperation UnwrapConversion(IOperation operation)
    {
        while (operation is IConversionOperation conversion && conversion.IsImplicit)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool IsFloatingPoint(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;

    private static bool IsUnorderedCollection(ITypeSymbol type)
    {
        var name = type.OriginalDefinition.ToDisplayString();
        return name is "System.Collections.Generic.Dictionary<TKey, TValue>"
            or "System.Collections.Generic.Dictionary<TKey, TValue>.KeyCollection"
            or "System.Collections.Generic.Dictionary<TKey, TValue>.ValueCollection"
            or "System.Collections.Generic.HashSet<T>";
    }

    private static bool IsEnumerableInterface(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T
            or SpecialType.System_Collections_IEnumerable;
}
