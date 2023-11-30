using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Workleap.Extensions.Mongo.Analyzers.Internals;

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

    public static void ReportDiagnostic(this OperationAnalysisContext context, DiagnosticDescriptor diagnosticDescriptor, IInvocationOperation operation)
    {
        var containingMethodName = context.ContainingSymbol.Name;
        var containingClassName = context.ContainingSymbol.ContainingType.Name;
        var operationLocation = operation.Syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, operationLocation, containingMethodName, containingClassName));
    }
}