using System.Linq;
using Xunit;
using PdfToGCode.Core.Fonts;

namespace PdfToGCode.Tests
{
    public class SvgFontParserTests
    {
        [Fact]
        public void Parse_ShouldParseFontCorrectly()
        {
            var svgContent = @"<svg>
  <defs>
    <font horiz-adv-x=""1000"">
      <font-face units-per-em=""1000"" ascent=""800"" descent=""-200""/>
      <glyph unicode=""A"" horiz-adv-x=""600"" d=""M100 0 L300 800 L500 0 M180 300 L420 300""/>
      <glyph unicode=""B"" horiz-adv-x=""600"" d=""M100 0 L100 800 L400 800 L400 500 L100 500 L400 500 L400 0 L100 0""/>
      <glyph unicode=""C"" horiz-adv-x=""600"" d=""M100 0 100 800 400 800""/> <!-- Implicit L -->
      <glyph unicode=""D"" horiz-adv-x=""600"" d=""M100 0 h100 v800 h-100 Z""/> <!-- Relative H V Z -->
      <glyph unicode=""E"" horiz-adv-x=""600"" d=""M0 0 C 100 100, 200 100, 300 0""/> <!-- Bezier -->
    </font>
  </defs>
</svg>";

            var parser = new SvgFontParser();
            var fontData = parser.Parse(svgContent);

            Assert.Equal(1000, fontData.UnitsPerEm);

            // Check D (Relative H, V, Z)
            var glyphD = fontData.Glyphs['D'];
            Assert.Single(glyphD.Strokes);
            // M100 0 -> h100 (200,0) -> v800 (200,800) -> h-100 (100,800) -> Z (100,0)
            var strokeD = glyphD.Strokes[0];
            Assert.Equal(5, strokeD.Count);
            Assert.Equal(100, strokeD[0].X); Assert.Equal(0, strokeD[0].Y);
            Assert.Equal(200, strokeD[1].X); Assert.Equal(0, strokeD[1].Y);
            Assert.Equal(200, strokeD[2].X); Assert.Equal(800, strokeD[2].Y);
            Assert.Equal(100, strokeD[3].X); Assert.Equal(800, strokeD[3].Y);
            Assert.Equal(100, strokeD[4].X); Assert.Equal(0, strokeD[4].Y); // Back to start

            // Check E (Bezier)
            var glyphE = fontData.Glyphs['E'];
            Assert.Single(glyphE.Strokes);
            // Start + segments (adaptive, so more than 2, likely more than 10 for length ~300)
            Assert.True(glyphE.Strokes[0].Count > 2);

            var lastIndex = glyphE.Strokes[0].Count - 1;
            Assert.Equal(0, glyphE.Strokes[0][0].X);
            Assert.Equal(0, glyphE.Strokes[0][0].Y);

            // Check endpoint
            Assert.Equal(300, glyphE.Strokes[0][lastIndex].X);
            Assert.Equal(0, glyphE.Strokes[0][lastIndex].Y);
        }
    }
}
