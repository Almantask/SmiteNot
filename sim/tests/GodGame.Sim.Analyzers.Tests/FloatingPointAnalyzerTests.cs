using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodGame.Sim.Analyzers.Tests;

[TestFixture, Category("Fast")]
public class FloatingPointAnalyzerTests
{
    [TestCase("float x = 1f;", TestName = "Float local and literal")]
    [TestCase("double x = 0;", TestName = "Double local")]
    [TestCase("decimal x = 0;", TestName = "Decimal local")]
    [TestCase("var x = 1.5;", TestName = "Var holding a double literal")]
    [TestCase("var x = System.Math.Sqrt(4);", TestName = "Var holding a double from an API")]
    [TestCase(
        "int x = (int)System.Math.Sqrt(4);",
        TestName = "Double hidden inside an int expression"
    )]
    [TestCase("int x = (int)(double)3;", TestName = "Explicit cast to double")]
    [TestCase("System.Single x = default;", TestName = "Qualified type name")]
    [TestCase(
        "var xs = new System.Collections.Generic.List<float>();",
        TestName = "Generic argument"
    )]
    [TestCase("float? x = null;", TestName = "Nullable float")]
    [TestCase("var big = double.MaxValue;", TestName = "Member of double")]
    public void FloatingPoint_IsReported(string statement)
    {
        var diagnostics = Analyze(Wrap(statement));

        Assert.That(
            diagnostics.Select(d => d.Id),
            Has.Some.EqualTo(FloatingPointAnalyzer.DiagnosticId)
        );
    }

    [TestCase("int x = 1 + 2 * 3;", TestName = "Integer arithmetic")]
    [TestCase("long x = 1L << 40;", TestName = "Long shift")]
    [TestCase("var x = System.Math.Max(1, 2);", TestName = "Integer overload of Math.Max")]
    [TestCase("string s = \"float double 1.5f\";", TestName = "Words inside a string")]
    [TestCase(
        "int floatingCount = 0; /* float in a comment */",
        TestName = "Identifier and comment"
    )]
    public void IntegerCode_IsClean(string statement)
    {
        var diagnostics = Analyze(Wrap(statement));

        Assert.That(diagnostics.Where(d => d.Id == FloatingPointAnalyzer.DiagnosticId), Is.Empty);
    }

    [Test]
    public void FloatField_IsReported()
    {
        var diagnostics = Analyze(
            "public class C { private float _speed; public int Get() => (int)_speed; }"
        );

        Assert.That(
            diagnostics.Select(d => d.Id),
            Has.Some.EqualTo(FloatingPointAnalyzer.DiagnosticId)
        );
    }

    private static string Wrap(string statement) =>
        $"public static class C {{ public static void M() {{ {statement} }} }}";

    private static ImmutableArray<Diagnostic> Analyze(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Probe",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var compileErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.That(compileErrors, Is.Empty, "The probe source itself must compile.");

        return compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FloatingPointAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();
    }
}
