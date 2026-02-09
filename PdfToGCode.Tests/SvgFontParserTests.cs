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
    </font>
  </defs>
</svg>";

            var parser = new SvgFontParser();
            var fontData = parser.Parse(svgContent);

            Assert.Equal(1000, fontData.UnitsPerEm);
            Assert.True(fontData.Glyphs.ContainsKey('A'));
            Assert.True(fontData.Glyphs.ContainsKey('B'));

            var glyphA = fontData.Glyphs['A'];
            Assert.Equal('A', glyphA.Unicode);
            Assert.Equal(600, glyphA.AdvanceWidth);
            Assert.Equal(2, glyphA.Strokes.Count);

            // Check Stroke 1: (100,0) -> (300,800) -> (500,0)
            Assert.Equal(3, glyphA.Strokes[0].Count);
            Assert.Equal(100, glyphA.Strokes[0][0].X);
            Assert.Equal(0, glyphA.Strokes[0][0].Y);
            Assert.Equal(300, glyphA.Strokes[0][1].X);
            Assert.Equal(800, glyphA.Strokes[0][1].Y);
            Assert.Equal(500, glyphA.Strokes[0][2].X);
            Assert.Equal(0, glyphA.Strokes[0][2].Y);

            // Check Stroke 2: (180,300) -> (420,300)
            Assert.Equal(2, glyphA.Strokes[1].Count);
            Assert.Equal(180, glyphA.Strokes[1][0].X);
            Assert.Equal(300, glyphA.Strokes[1][0].Y);
            Assert.Equal(420, glyphA.Strokes[1][1].X);
            Assert.Equal(300, glyphA.Strokes[1][1].Y);
        }
    }
}
