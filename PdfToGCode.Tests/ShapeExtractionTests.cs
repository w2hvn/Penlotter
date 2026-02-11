using System;
using System.IO;
using System.Linq;
using Xunit;
using PdfToGCode.Core.Pdf;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Content;
using PigPoint = UglyToad.PdfPig.Core.PdfPoint;

namespace PdfToGCode.Tests
{
    public class ShapeExtractionTests
    {
        [Fact]
        public void ExtractShapes_ShouldExtractLinesAndRectangles()
        {
            var pdfPath = "test_shapes.pdf";
            var builder = new PdfDocumentBuilder();
            var page = builder.AddPage(PageSize.A4);

            // Draw a line
            page.DrawLine(new PigPoint(10, 10), new PigPoint(100, 10), 1);

            // Draw a rectangle
            // Params: position, width, height, lineWidth, fill
            // Note: Reflection showed Boolean fill is last arg.
            page.DrawRectangle(new PigPoint(50, 50), 100, 50, 1, false);

            var pdfBytes = builder.Build();
            File.WriteAllBytes(pdfPath, pdfBytes);

            var extractor = new PdfTextLayoutExtractor();
            var content = extractor.ExtractPageContent(pdfPath, 1);

            Assert.NotNull(content.Shapes);
            // Expect 1 shape for line, 1 shape for rectangle (or 4 lines).
            // Usually PdfPig keeps paths together if they are part of same operation?
            // Or separate?

            // If the code works, we should have shapes.
            Assert.True(content.Shapes.Count > 0, "No shapes extracted.");

            // Check if we have the rectangle
            // Rectangle at 50,50 with w=100, h=50 => (50,50), (150,50), (150,100), (50,100).

            // Let's verify we found at least 2 shapes or one complex shape.
            // If ExtractShapesFromPage only handles MoveTo/LineTo, a Rectangle command might be ignored.

            bool foundRectangle = content.Shapes.Any(s =>
                s.Points.Any(p => Math.Abs(p.X - 50) < 1 && Math.Abs(p.Y - 50) < 1) &&
                s.Points.Any(p => Math.Abs(p.X - 150) < 1 && Math.Abs(p.Y - 100) < 1)
            );

            Assert.True(foundRectangle, "Rectangle not extracted correctly.");

            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }
}
