using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using TestHelper;

namespace WinformsDesignerAnalyzer.Test
{
	public class UnitTest : CodeFixVerifier
	{
        [Test]
		public void WhenEmpty_NoDiagnostics()
		{
			var test = @"";

			VerifyCSharpDiagnostic(test);
		}

        [Test]
        public void WhenNotForm_NoDiagnostics()
        {
            var test = @"
class SomeClass
{
}";

            VerifyCSharpDiagnostic(test);
        }

        [Test]
        public void WhenEmptyForm_NoDiagnostics()
        {
            var test = @"
using System.Windows.Forms;

class SomeForm : Form
{
}";

            VerifyCSharpDiagnostic(test);
        }

        [Test]
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
				Id = "WDA001",
				Message = "Control 'SomeForm' does not contains designer file.",
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

		[Test]
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

        [TestCase("Form")]
        [TestCase("UserControl")]
        public void WhenContainsRegion_ShouldFix(string baseClass)
        {
            var testTemplate = @"
using System.Windows.Forms;

class SomeForm : BASE_CLASS
{
	#region Component fields

	private System.ComponentModel.IContainer components = null;
	private System.Windows.Forms.Panel panel1;

	#endregion

    public SomeForm()
    {
        InitializeComponent();
    }

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

            var test = testTemplate.Replace("BASE_CLASS", baseClass);

            var expected = new DiagnosticResult
            {
                Id = "WDA001",
                Message = "Control 'SomeForm' does not contains designer file.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 4, 7)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtestTemplate = @"
using System.Windows.Forms;

partial class SomeForm : BASE_CLASS
{



    public SomeForm()
    {
        InitializeComponent();
    }



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
    private System.Windows.Forms.Panel panel1;
}";

            var fixtest = fixtestTemplate.Replace("BASE_CLASS", baseClass);

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
