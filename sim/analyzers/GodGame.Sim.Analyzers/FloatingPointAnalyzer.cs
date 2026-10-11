using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace GodGame.Sim.Analyzers;

/// <summary>
/// GG0001: no floating point anywhere in the simulation. The banned-API analyzer catches members such as
/// MathF.Sqrt, but not the types themselves (`float x = 1f;` compiles cleanly under it), so this analyzer
/// reports every float, double or decimal type, literal and value.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FloatingPointAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "GG0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Floating point in the simulation",
        messageFormat: "'{0}' is floating point; the simulation uses Fix or integers (GodGame.Sim.Math)",
        category: "Determinism",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Floating-point results can differ between runtimes, compilers and CPUs, which breaks "
            + "lockstep determinism. Use Fix for positions and velocities and integers in natural units for fields."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics
        );
        context.EnableConcurrentExecution();

        // Types written in source: float, System.Double, Single, and `var` when it stands for one of them.
        context.RegisterSyntaxNodeAction(
            AnalyzeTypeName,
            SyntaxKind.PredefinedType,
            SyntaxKind.IdentifierName
        );

        // Literals: 1f, 1.5, 2m.
        context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.NumericLiteralExpression);

        // Values from APIs and conversions with no floating-point type in sight: Math.Sqrt(4), ints passed as doubles.
        context.RegisterOperationAction(
            AnalyzeOperation,
            OperationKind.Invocation,
            OperationKind.PropertyReference,
            OperationKind.FieldReference,
            OperationKind.Conversion
        );
    }

    private static void AnalyzeTypeName(SyntaxNodeAnalysisContext context)
    {
        var type =
            context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol
            as ITypeSymbol;
        if (IsFloatingPoint(type))
        {
            Report(context.ReportDiagnostic, context.Node.GetLocation(), context.Node.ToString());
        }
    }

    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var type = context.SemanticModel.GetTypeInfo(literal, context.CancellationToken).Type;
        if (IsFloatingPoint(type))
        {
            Report(context.ReportDiagnostic, literal.GetLocation(), literal.Token.Text);
        }
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        var operation = context.Operation;
        if (operation is IConversionOperation { IsImplicit: false })
        {
            return; // an explicit cast names its type, which AnalyzeTypeName already reports
        }
        if (IsFloatingPoint(operation.Type))
        {
            Report(
                context.ReportDiagnostic,
                operation.Syntax.GetLocation(),
                operation.Syntax.ToString()
            );
        }
    }

    private static bool IsFloatingPoint(ITypeSymbol? type)
    {
        if (
            type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            } nullable
        )
        {
            type = nullable.TypeArguments[0];
        }
        return type?.SpecialType
            is SpecialType.System_Single
                or SpecialType.System_Double
                or SpecialType.System_Decimal;
    }

    private static void Report(System.Action<Diagnostic> report, Location location, string text) =>
        report(Diagnostic.Create(Rule, location, text));
}
