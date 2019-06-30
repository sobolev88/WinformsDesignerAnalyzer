using System.Collections.Immutable;
using System.Composition;
using System.Linq;
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
					createChangedSolution: c => CreateDesignerFileAsync(context.Document, root, declaration),
					equivalenceKey: title),
				diagnostic);
		}

		private Task<Solution> CreateDesignerFileAsync(Document document, SyntaxNode root, TypeDeclarationSyntax typeDecl)
		{
			var generator = SyntaxGenerator.GetGenerator(document);
			var newType = (TypeDeclarationSyntax)generator.WithModifiers(typeDecl, DeclarationModifiers.Partial);
			var newRoot = root.ReplaceNode(typeDecl, newType);
			document = document.WithSyntaxRoot(newRoot);

            var designerRoot = generator.ClassDeclaration(typeDecl.Identifier.Text);
            designerRoot = generator.WithModifiers(designerRoot, DeclarationModifiers.Partial);

            if (typeDecl.Parent is NamespaceDeclarationSyntax @namespace)
            {
                designerRoot = generator.NamespaceDeclaration(@namespace.Name, designerRoot);
            }

            designerRoot = generator.CompilationUnit(designerRoot);

            var newDocument = document.Project.AddDocument(document.Name.Replace(".cs", "") + ".Designer.cs", designerRoot, document.Folders);
			return Task.FromResult(newDocument.Project.Solution);
		}
	}
}
