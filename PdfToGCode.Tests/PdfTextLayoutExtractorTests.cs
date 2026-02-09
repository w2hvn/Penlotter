using System.IO;
using System.Linq;
using Xunit;
using PdfToGCode.Core.Pdf;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using CorePdfPoint = PdfToGCode.Core.Utils.PdfPoint;
using PigPoint = UglyToad.PdfPig.Core.PdfPoint;

namespace PdfToGCode.Tests
{
    public class PdfTextLayoutExtractorTests
    {
        [Fact]
        public void ExtractWords_ShouldExtractCorrectly()
        {
            var pdfPath = "test_extract.pdf";
            var builder = new PdfDocumentBuilder();
            var page = builder.AddPage(PageSize.A4);

            var font = builder.AddStandard14Font(Standard14Font.Helvetica);

            // Add text "Hello World" at (100, 100)
            page.AddText("Hello World", 12, new PigPoint(100, 100), font);

            var pdfBytes = builder.Build();
            File.WriteAllBytes(pdfPath, pdfBytes);

            var extractor = new PdfTextLayoutExtractor();
            var results = extractor.ExtractWords(pdfPath);

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Text == "Hello");
            Assert.Contains(results, r => r.Text == "World");

            var hello = results.First(r => r.Text == "Hello");
            // Coordinates should be close to (100, 100)
            Assert.True(hello.BottomLeft.X >= 99);
            Assert.True(hello.BottomLeft.Y >= 99);

            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }
}
