using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;

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

        private async Task<Solution> CreateDesignerFileAsync(Document document, SyntaxNode root, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
		{
			var generator = SyntaxGenerator.GetGenerator(document);
			var newType = (TypeDeclarationSyntax)generator.WithModifiers(typeDecl, DeclarationModifiers.Partial);

            var designerClass = (TypeDeclarationSyntax)generator.ClassDeclaration(typeDecl.Identifier.Text);
            designerClass = (TypeDeclarationSyntax)generator.WithModifiers(designerClass, DeclarationModifiers.Partial);

            (newType, designerClass) = MoveRegion(newType, designerClass, "Component fields");
            (newType, designerClass) = MoveRegion(newType, designerClass, "Designer generated code");
            (newType, designerClass) = MoveRegion(newType, designerClass, "Standard WinForms code");

            SyntaxNode designerRoot = designerClass;

            if (typeDecl.Parent is NamespaceDeclarationSyntax @namespace)
            {
                designerRoot = generator.NamespaceDeclaration(@namespace.Name, designerRoot);
            }

            designerRoot = generator.CompilationUnit(designerRoot);

            var newRoot = root.ReplaceNode(typeDecl, newType);

            document = document.WithSyntaxRoot(newRoot);

            document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);

            var newDocument = document.Project.AddDocument(document.Name.Replace(".cs", "") + ".Designer.cs", designerRoot, document.Folders);
            newDocument = await Formatter.FormatAsync(newDocument, cancellationToken: cancellationToken).ConfigureAwait(false);
            return newDocument.Project.Solution;
		}

        private static (TypeDeclarationSyntax from, TypeDeclarationSyntax to) MoveRegion(TypeDeclarationSyntax from, TypeDeclarationSyntax to, string regionName)
        {
            var region = from
                .DescendantNodes(descendIntoTrivia: true)
                .OfType<RegionDirectiveTriviaSyntax>()
                .FirstOrDefault(r => r.ToFullString().Contains(regionName));

            if (region != null)
            {
                var r = from.DescendantTrivia()
                    .FirstOrDefault(t => t.IsKind(SyntaxKind.RegionDirectiveTrivia) && t.ToFullString().Contains(regionName));

                var members = from.Members.Where(n => n.GetLeadingTrivia().Contains(r));

                to = to.WithMembers(to.Members.InsertRange(0, members.Select(PrepareMember)));
                from = from.RemoveNodes(members, SyntaxRemoveOptions.KeepDirectives);

                region = from
                    .DescendantNodes(descendIntoTrivia: true)
                    .OfType<RegionDirectiveTriviaSyntax>()
                    .FirstOrDefault(x => x.ToFullString().Contains(regionName));

                if (region.GetNextDirective() is EndRegionDirectiveTriviaSyntax endregion)
                {
                    from = from.RemoveNodes(new[] { (SyntaxNode)region, endregion }, SyntaxRemoveOptions.KeepNoTrivia);
                }
            }
            return (from, to);

            MemberDeclarationSyntax PrepareMember(MemberDeclarationSyntax member)
            {
                return member.ReplaceTrivia(member.GetLeadingTrivia(), (old, @new) =>
                {
                    return @new.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || old.IsKind(SyntaxKind.RegionDirectiveTrivia) || old.IsKind(SyntaxKind.EndRegionDirectiveTrivia)
                        ? default : @new;
                });
            }
        }
	}
}
