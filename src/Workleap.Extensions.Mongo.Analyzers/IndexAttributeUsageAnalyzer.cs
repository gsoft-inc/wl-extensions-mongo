using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Workleap.Extensions.Mongo.Analyzers.Internals;

namespace Workleap.Extensions.Mongo.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IndexAttributeUsageAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor UseIndexAttributeRule = new DiagnosticDescriptor(
        id: RuleIdentifiers.UseIndexAttribute,
        title: "Add 'IndexBy' or 'NoIndexNeeded' attributes on the containing method or class",
        messageFormat: "Add 'IndexBy' or 'NoIndexNeeded' attributes on the method '{0}' or the class '{1}' to specify required indexes",
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
        if (analyzer.ShouldScan)
        {
            context.RegisterOperationAction(analyzer.AnalyzeOperationInvocation, OperationKind.Invocation);
        }
    }

    private sealed class IndexAttributeUsageAnalyzerImplementation
    {
        /// <summary>
        /// Symbol representing the type MongoDB.Driver.IMongoCollectionExtensions, if we see this symbol trigger the validation
        /// </summary>
        private readonly INamedTypeSymbol? _mongoCollectionExtensionsType;
        
        /// <summary>
        /// Symbol representing the type MongoDB.Driver.IMongoCollection, if we see this symbol trigger the validation
        /// </summary>
        private readonly INamedTypeSymbol? _mongoCollectionInterfaceType;
        
        /// <summary>
        /// List of attribute that satisfy the requirement
        /// </summary>
        private readonly ImmutableHashSet<INamedTypeSymbol> _mongoIndexAttributes;
        
        /// <summary>
        /// Already known symbol with issues: prevent duplicate diagnostics
        /// </summary>
        private readonly ConcurrentDictionary<ISymbol, bool> _symbolsWithoutAttributes = new(SymbolEqualityComparer.Default);

        public IndexAttributeUsageAnalyzerImplementation(Compilation compilation)
        {
            var indexAttributesBuilder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            indexAttributesBuilder.AddIfNotNull(compilation.FindTypeByMetadataName(KnownSymbolNames.IndexedByAttribute, KnownSymbolNames.WorkleapMongoAssembly));
            indexAttributesBuilder.AddIfNotNull(compilation.FindTypeByMetadataName(KnownSymbolNames.NoIndexNeededAttribute, KnownSymbolNames.WorkleapMongoAssembly));
            this._mongoIndexAttributes = indexAttributesBuilder.ToImmutable();

            this._mongoCollectionExtensionsType = compilation.FindTypeByMetadataName(KnownSymbolNames.MongoCollectionExtensions, KnownSymbolNames.MongoAssembly);
            this._mongoCollectionInterfaceType = compilation.FindTypeByMetadataName(KnownSymbolNames.MongoCollectionInterface, KnownSymbolNames.MongoAssembly);
        }

        /// <summary>
        /// We should only scan if the assembly(?) is referencing MongoDB.Driver and Workleap.Extension.Mongo
        /// </summary>
        public bool ShouldScan => this._mongoIndexAttributes.Count == 2 &&
                               this._mongoCollectionExtensionsType is not null &&
                               this._mongoCollectionInterfaceType is not null;

        public void AnalyzeOperationInvocation(OperationAnalysisContext context)
        {
            if (context.Operation is not IInvocationOperation operation)
            {
                return;
            }

            var containingMethodSymbol = context.ContainingSymbol;
            if (this._symbolsWithoutAttributes.ContainsKey(containingMethodSymbol))
            {
                return;
            }

            if (!this.IsMongoCollectionMethod(operation) && !this.IsMongoCollectionExtensionMethod(operation))
            {
                return;
            }

            var hasIndexAttribute = this.IsMethodOrClassContainingIndexAttribute(context);
            if (!hasIndexAttribute)
            {
                context.ReportDiagnostic(UseIndexAttributeRule, operation);
                this._symbolsWithoutAttributes.TryAdd(containingMethodSymbol, true);
            }
        }

        private bool IsMethodOrClassContainingIndexAttribute(OperationAnalysisContext context)
        {
            var doesClassHaveAttribute = context.ContainingSymbol.ContainingType.GetAttributes()
                .Any(this.IndexAttributePredicate());

            if (doesClassHaveAttribute)
            {
                return true;
            }

            var doesMethodHaveAttribute = context.ContainingSymbol.GetAttributes()
                .Any(this.IndexAttributePredicate());

            return doesMethodHaveAttribute;
        }

        private Func<AttributeData, bool> IndexAttributePredicate()
        {
            return x => x.AttributeClass != null && this._mongoIndexAttributes.Contains(x.AttributeClass);
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