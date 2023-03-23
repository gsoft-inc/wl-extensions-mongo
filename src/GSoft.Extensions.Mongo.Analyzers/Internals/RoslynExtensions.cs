using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GSoft.Extensions.Mongo.Analyzers.Internals;

internal static class RoslynExtensions
{
    public static INamedTypeSymbol? FindTypeByMetadataName(this Compilation compilation, string typeMetadataName, string? assemblyName = null)
    {
        var assemblies = GetAllAssemblies(compilation);

        if (assemblyName != null)
        {
            assemblies = assemblies.Where(x => x.Name == assemblyName);
        }

        foreach (var assembly in assemblies)
        {
            if (assembly.GetTypeByMetadataName(typeMetadataName) is { } symbol)
            {
                return symbol;
            }
        }

        return null;
    }

    private static IEnumerable<IAssemblySymbol> GetAllAssemblies(Compilation compilation)
    {
        yield return compilation.Assembly;

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
            {
                yield return assembly;
            }
        }
    }

    public static void ReportDiagnostic(this CompilationAnalysisContext context, DiagnosticDescriptor diagnosticDescriptor, ISymbol symbol)
    {
        if (symbol.Locations.Length <= 0)
        {
            return;
        }

        var firstLocation = symbol.Locations.First();
        context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, firstLocation));
    }
}