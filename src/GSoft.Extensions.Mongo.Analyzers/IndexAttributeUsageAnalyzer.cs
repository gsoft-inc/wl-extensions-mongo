using System.Collections.Concurrent;
using System.Collections.Immutable;
using GSoft.Extensions.Mongo.Analyzers.Internals;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace GSoft.Extensions.Mongo.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IndexAttributeUsageAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor UseIndexAttributeRule = new DiagnosticDescriptor(
        id: RuleIdentifiers.UseIndexAttribute,
        title: "Specify whether or not an index is required with IndexedByAttribute or NoIndexNeededAttribute",
        messageFormat: "Specify whether or not an index is required with IndexedByAttribute or NoIndexNeededAttribute",
        category: RuleCategories.Design,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: RuleIdentifiers.HelpUri);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        UseIndexAttributeRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStarted);
    }

    private static void OnCompilationStarted(CompilationStartAnalysisContext context)
    {
        var analyzer = new IndexAttributeUsageAnalyzerImplementation(context.Compilation);
        if (analyzer.IsValid)
        {
            context.RegisterOperationAction(analyzer.AnalyzeOperationInvocation, OperationKind.Invocation);
            context.RegisterCompilationEndAction(analyzer.CompilationEnd);
        }
    }

    private sealed class IndexAttributeUsageAnalyzerImplementation
    {
        private readonly INamedTypeSymbol? _mongoCollectionExtensionsType;
        private readonly INamedTypeSymbol? _mongoCollectionInterfaceType;
        private readonly ImmutableHashSet<INamedTypeSymbol> _mongoIndexAttributes;
        private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _symbolsWithoutAttributes = new(SymbolEqualityComparer.Default);

        public IndexAttributeUsageAnalyzerImplementation(Compilation compilation)
        {
            var indexAttributesBuilder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            indexAttributesBuilder.AddIfNotNull(compilation.FindTypeByMetadataName(KnownSymbolNames.IndexedByAttribute, KnownSymbolNames.GSoftMongoAssembly));
            indexAttributesBuilder.AddIfNotNull(compilation.FindTypeByMetadataName(KnownSymbolNames.NoIndexNeededAttribute, KnownSymbolNames.GSoftMongoAssembly));
            this._mongoIndexAttributes = indexAttributesBuilder.ToImmutable();

            this._mongoCollectionExtensionsType = compilation.FindTypeByMetadataName(KnownSymbolNames.MongoCollectionExtensions, KnownSymbolNames.MongoAssembly);
            this._mongoCollectionInterfaceType = compilation.FindTypeByMetadataName(KnownSymbolNames.MongoCollectionInterface, KnownSymbolNames.MongoAssembly);
        }

        public bool IsValid => this._mongoIndexAttributes.Count == 2 &&
                               this._mongoCollectionExtensionsType is not null &&
                               this._mongoCollectionInterfaceType is not null;

        public void AnalyzeOperationInvocation(OperationAnalysisContext context)
        {
            if (context.Operation is not IInvocationOperation operation)
            {
                return;
            }

            var containingClassSymbol = context.ContainingSymbol.ContainingType;
            if (this._symbolsWithoutAttributes.ContainsKey(containingClassSymbol))
            {
                return;
            }

            if (!this.IsMongoCollectionMethod(operation) && !this.IsMongoCollectionExtensionMethod(operation))
            {
                return;
            }

            var hasIndexAttribute = containingClassSymbol.GetAttributes()
                .Any(x => x.AttributeClass != null && this._mongoIndexAttributes.Contains(x.AttributeClass));

            if (!hasIndexAttribute)
            {
                this._symbolsWithoutAttributes.TryAdd(containingClassSymbol, true);
            }
        }

        public void CompilationEnd(CompilationAnalysisContext context)
        {
            foreach (var symbolWithoutAttributes in this._symbolsWithoutAttributes.Keys)
            {
                context.ReportDiagnostic(UseIndexAttributeRule, symbolWithoutAttributes);
            }
        }

        private bool IsMongoCollectionMethod(IInvocationOperation operation)
        {
            var containingType = operation.TargetMethod.ContainingType;
            return containingType is { IsGenericType: true, Arity: 1 } && SymbolEqualityComparer.Default.Equals(this._mongoCollectionInterfaceType, containingType.ConstructedFrom);
        }

        private bool IsMongoCollectionExtensionMethod(IInvocationOperation operation)
        {
            return SymbolEqualityComparer.Default.Equals(this._mongoCollectionExtensionsType, operation.TargetMethod.ContainingType);
        }
    }
}