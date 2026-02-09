using System.Collections.Generic;
using System.Linq;
using Xunit;
using PdfToGCode.Core.GCode;
using PdfToGCode.Core.Pdf;
using PdfToGCode.Core.Fonts;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Tests
{
    public class GCodeGeneratorTests
    {
        [Fact]
        public void Generate_ShouldProduceValidGCode()
        {
            var settings = new GCodeSettings();
            var fontData = new FontData
            {
                UnitsPerEm = 1000,
                Glyphs = new Dictionary<char, GlyphGeometry>
                {
                    ['A'] = new GlyphGeometry
                    {
                        Unicode = 'A',
                        AdvanceWidth = 600,
                        Strokes = new List<List<PdfPoint>>
                        {
                            new List<PdfPoint> { new PdfPoint(0, 0), new PdfPoint(100, 100) }
                        }
                    }
                }
            };

            var glyphA = new ExtractedGlyph
            {
                Character = 'A',
                BottomLeft = new PdfPoint(100, 100), // PDF units (points)
                FontSize = 10,
                Width = 6,
                Height = 10
            };

            var textBlocks = new List<ExtractedText>
            {
                new ExtractedText
                {
                    Text = "A",
                    Glyphs = new List<ExtractedGlyph> { glyphA }
                }
            };

            var generator = new GCodeGenerator();
            var gcode = generator.Generate(textBlocks, fontData, settings);

            Assert.Contains("G21", gcode);
            Assert.Contains("G90", gcode);
            Assert.Contains($"G0 Z{settings.ZUp}", gcode);

            // Check coordinate transformation
            // Font point (0,0) -> PDF (100, 100)
            // GCode X = 100 * 25.4/72
            // GCode Y = 100 * 25.4/72

            double expectedXStart = 100 * 25.4 / 72.0;
            double expectedYStart = 100 * 25.4 / 72.0;

            // Font point (100,100)
            // Scale = 10/1000 = 0.01
            // PDF X = 100 * 0.01 + 100 = 101
            // PDF Y = 100 * 0.01 + 100 = 101
            // GCode X = 101 * 25.4/72
            // GCode Y = 101 * 25.4/72

            double expectedXEnd = 101 * 25.4 / 72.0;
            double expectedYEnd = 101 * 25.4 / 72.0;

            Assert.Contains($"X{expectedXStart:F3} Y{expectedYStart:F3}", gcode);
            Assert.Contains($"X{expectedXEnd:F3} Y{expectedYEnd:F3}", gcode);
        }
    }
}
