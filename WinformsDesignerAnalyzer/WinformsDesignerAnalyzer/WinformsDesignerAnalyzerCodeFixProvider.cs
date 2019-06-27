using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace WinformsDesignerAnalyzer
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WinformsDesignerAnalyzerCodeFixProvider)), Shared]
	public class WinformsDesignerAnalyzerCodeFixProvider : CodeFixProvider
	{
		private const string title = "Create .Designer.cs";

		public sealed override ImmutableArray<string> FixableDiagnosticIds
		{
			get { return ImmutableArray.Create(WinformsDesignerAnalyzerAnalyzer.DiagnosticId); }
		}

		public sealed override FixAllProvider GetFixAllProvider()
		{
			return WellKnownFixAllProviders.BatchFixer;
		}

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

			context.RegisterCodeFix(
				CodeAction.Create(
					title: title,
					createChangedSolution: c => CreateDesignerFileAsync(context.Document, root, declaration, c),
					equivalenceKey: title),
				diagnostic);
		}

		private Task<Solution> CreateDesignerFileAsync(Document document, SyntaxNode root, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
		{
			const string template = @"
namespace {0}
{{
	partial class {1}
	{{
	}}
}}
";
			var g = SyntaxGenerator.GetGenerator(document);
			var newType = (TypeDeclarationSyntax)g.WithModifiers(typeDecl, DeclarationModifiers.Partial);
			var newRoot = root.ReplaceNode(typeDecl, newType);
			document = document.WithSyntaxRoot(newRoot);

			var @namespace = (NamespaceDeclarationSyntax)typeDecl.Parent;
			var designerCode = string.Format(template, @namespace.Name, typeDecl.Identifier);

			var newDocument = document.Project.AddDocument(document.Name.Replace(".cs", "") + ".Designer.cs", designerCode, document.Folders);
			return Task.FromResult(newDocument.Project.Solution);
		}
	}
}
