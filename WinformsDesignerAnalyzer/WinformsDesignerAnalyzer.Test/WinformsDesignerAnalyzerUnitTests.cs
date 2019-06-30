using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    private void InitializeComponent()
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
    private void InitializeComponent()
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

		[TestMethod]
		public void WhenContainsDesignerFile_NoDiagnostics()
		{
			var test = @"
using System.Windows.Forms;

class SomeForm : Form
{
    private void InitializeComponent()
    {
    };
}";

			var designerTest = @"
partial class SomeForm
{
}
";

			VerifyCSharpDiagnostic(new[] { (test, "SomeForm.cs"), (designerTest, "SomeForm.Designer.cs") });
		}

        [TestMethod]
        public void WhenContainsRegion_ShouldFix()
        {
            var test = @"
using System.Windows.Forms;

class SomeForm : Form
{
	#region Component fields

	private System.ComponentModel.IContainer components = null;

	#endregion

    #region Standard WinForms code

	/// <summary>
	/// Clean up any resources being used.
	/// </summary>
	protected override void Dispose( bool disposing )
	{
		if( disposing )
		{
			if (components != null) 
			{
				components.Dispose();
			}
		}
		base.Dispose( disposing );
	}


	#region Designer generated code
	/// <summary>
	/// Required method for Designer support - do not modify
	/// the contents of this method with the code editor.
	/// </summary>
	private void InitializeComponent()
	{
	}

	#endregion

	#endregion
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




}";

            var designer = @"partial class SomeForm
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (components != null)
            {
                components.Dispose();
            }
        }
        base.Dispose(disposing);
    }


    private void InitializeComponent()
    {
    }

    private System.ComponentModel.IContainer components = null;
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
