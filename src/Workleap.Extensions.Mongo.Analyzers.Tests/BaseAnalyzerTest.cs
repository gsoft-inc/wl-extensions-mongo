using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Analyzers.Tests;

public class BaseAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    private const string CSharp10GlobalUsings = @"
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
";

    private const string MongoGlobalUsings = @"
global using Workleap.Extensions.Mongo;
global using MongoDB.Driver;";

    private const string SourceFileName = "Program.cs";

    protected BaseAnalyzerTest()
    {
        this.TestState.Sources.Add(CSharp10GlobalUsings);
        this.TestState.Sources.Add(MongoGlobalUsings);

        this.TestState.ReferenceAssemblies = ReferenceAssemblies.Net.Net60;
        this.TestState.AdditionalReferences.Add(typeof(IMongoDocument).Assembly); // Workleap.Extensions.Mongo
        this.TestState.AdditionalReferences.Add(typeof(MongoClient).Assembly); // MongoDB.Driver
    }

    protected override CompilationOptions CreateCompilationOptions()
        => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false);

    protected override ParseOptions CreateParseOptions()
        => new CSharpParseOptions(LanguageVersion.CSharp10, DocumentationMode.Diagnose);

    public BaseAnalyzerTest<TAnalyzer> WithExpectedDiagnostic(DiagnosticDescriptor descriptor, int startLine, int startColumn, int endLine, int endColumn, params object[] args)
    {
        this.TestState.ExpectedDiagnostics.Add(new DiagnosticResult(descriptor)
            .WithSpan(SourceFileName, startLine, startColumn, endLine, endColumn)
            .WithArguments(args));
        return this;
    }

    public BaseAnalyzerTest<TAnalyzer> WithExpectedDiagnostic(DiagnosticDescriptor descriptor, int locationIndex, params object[] args)
    {
        this.TestState.ExpectedDiagnostics.Add(new DiagnosticResult(descriptor)
            .WithLocation(locationIndex)
            .WithArguments(args));
        return this;
    }

    protected BaseAnalyzerTest<TAnalyzer> WithSourceCode(string sourceCode)
    {
        this.TestState.Sources.Add((SourceFileName, sourceCode));
        return this;
    }
}