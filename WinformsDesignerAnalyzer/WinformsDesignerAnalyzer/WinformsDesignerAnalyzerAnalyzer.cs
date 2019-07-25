using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsDesignerAnalyzer
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class WinformsDesignerAnalyzerAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "WDA001";

		private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
		private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
		private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
		private const string Category = "Naming";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
		{
			context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
		}

		private static void AnalyzeSymbol(SymbolAnalysisContext context)
		{
			var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

			if (!IsSubtypeFrom(namedTypeSymbol, "System.Windows.Forms.ContainerControl"))
			{
				return;
			}

            if (!namedTypeSymbol.GetMembers().Any(m => m.Kind == SymbolKind.Method && m.Name == "InitializeComponent"))
            {
                return;
            }

			if (namedTypeSymbol.Locations.Any(l => l.GetLineSpan().Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)))
			{
				return;
			}

			var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);
			context.ReportDiagnostic(diagnostic);
		}

		private static bool IsSubtypeFrom(INamedTypeSymbol namedTypeSymbol, string fullTypeName)
		{
			namedTypeSymbol = namedTypeSymbol?.BaseType;

			while (namedTypeSymbol != null)
			{
                if (namedTypeSymbol.ToDisplayString() == fullTypeName)
				{
					return true;
				}

				namedTypeSymbol = namedTypeSymbol.BaseType;
			}

			return false;
		}
	}
}
