using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dvx.Analyzers
{
    // Trivial spike rule: warn on every class whose name ends with "Plugin".
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PluginSuffixAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            "DVXSPIKE001", "Class name ends with Plugin", "Class '{0}' ends with 'Plugin'",
            "Spike", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(c =>
            {
                var type = (INamedTypeSymbol)c.Symbol;
                if (type.TypeKind == TypeKind.Class && type.Name.EndsWith("Plugin"))
                    c.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name));
            }, SymbolKind.NamedType);
        }
    }
}
