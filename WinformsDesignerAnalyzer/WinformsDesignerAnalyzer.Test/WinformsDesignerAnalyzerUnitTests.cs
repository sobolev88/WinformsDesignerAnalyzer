using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using TestHelper;

namespace WinformsDesignerAnalyzer.Test
{
	[TestClass]
	public class UnitTest : CodeFixVerifier
	{
		[TestMethod]
		public void WhenEmpty_NoDiagnostics()
		{
			var test = @"";

			VerifyCSharpDiagnostic(test);
		}

        [TestMethod]
        public void WhenNotForm_NoDiagnostics()
        {
            var test = @"
class SomeClass
{
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void WhenEmptyForm_NoDiagnostics()
        {
            var test = @"
using System.Windows.Forms;

class SomeForm : Form
{
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
		public void WhenContainsInitializeComponentsAndOneFile_ShouldDiagnostics()
		{
			var test = @"
using System.Windows.Forms;

class SomeForm : Form
{
    private void InitializeComponents()
    {
    };
}";

			var expected = new DiagnosticResult
			{
				Id = "WinformsDesignerAnalyzer",
				Message = "Form 'SomeForm' does not contains designer file.",
				Severity = DiagnosticSeverity.Warning,
				Locations =
					new[] {
							new DiagnosticResultLocation("Test0.cs", 4, 7)
						}
			};

			VerifyCSharpDiagnostic(test, expected);

			var fixtest = @"
using System.Windows.Forms;

partial class SomeForm : Form
{
    private void InitializeComponents()
    {
    };
}";

            var designer = @"partial class SomeForm
{
}";

            var documents = GetFixedCSharpDocuments(test);
            documents.Should().HaveCount(2);
            documents[0].Should().Be(fixtest);
            documents[1].Should().Be(designer);
		}

		protected override CodeFixProvider GetCSharpCodeFixProvider()
		{
			return new WinformsDesignerAnalyzerCodeFixProvider();
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new WinformsDesignerAnalyzerAnalyzer();
		}
	}
}
