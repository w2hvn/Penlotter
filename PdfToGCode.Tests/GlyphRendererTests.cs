using System.Collections.Generic;
using System.Linq;
using Xunit;
using PdfToGCode.Core.Fonts;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Tests
{
    public class GlyphRendererTests
    {
        [Fact]
        public void RenderText_ShouldGenerateCorrectPaths()
        {
            // Setup font data
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
                            new List<PdfPoint> { new PdfPoint(100, 0), new PdfPoint(500, 800) }
                        }
                    },
                    ['B'] = new GlyphGeometry
                    {
                        Unicode = 'B',
                        AdvanceWidth = 600,
                        Strokes = new List<List<PdfPoint>>
                        {
                            new List<PdfPoint> { new PdfPoint(100, 0), new PdfPoint(100, 800) }
                        }
                    }
                }
            };

            var renderer = new GlyphRenderer();
            double fontSize = 100; // Scale = 100/1000 = 0.1
            // Render "AB" at (0,0)
            var paths = renderer.RenderText("AB", 0, 0, fontSize, fontData);

            // Expect 2 strokes (one from A, one from B)
            Assert.Equal(2, paths.Count);

            // A: (100,0) -> (10 + 0, 0 + 0) = (10, 0)
            //    (500,800) -> (50 + 0, 80 + 0) = (50, 80)
            var strokeA = paths[0];
            Assert.Equal(2, strokeA.Count);
            Assert.Equal(10, strokeA[0].X);
            Assert.Equal(0, strokeA[0].Y);
            Assert.Equal(50, strokeA[1].X);
            Assert.Equal(80, strokeA[1].Y);

            // Advance Width of A is 600 * 0.1 = 60.
            // B starts at (60, 0).
            // B: (100,0) -> (10 + 60, 0 + 0) = (70, 0)
            //    (100,800) -> (10 + 60, 80 + 0) = (70, 80)
            var strokeB = paths[1];
            Assert.Equal(2, strokeB.Count);
            Assert.Equal(70, strokeB[0].X);
            Assert.Equal(0, strokeB[0].Y);
            Assert.Equal(70, strokeB[1].X);
            Assert.Equal(80, strokeB[1].Y);
        }

        [Fact]
        public void RenderText_ShouldHandleMissingChars()
        {
            var fontData = new FontData { UnitsPerEm = 1000 };
            var renderer = new GlyphRenderer();
            var paths = renderer.RenderText("X", 0, 0, 100, fontData);
            Assert.Empty(paths);
        }

        [Fact]
        public void RenderText_ShouldHandleSpace()
        {
            var fontData = new FontData
            {
                UnitsPerEm = 1000,
                Glyphs = new Dictionary<char, GlyphGeometry>
                {
                    ['A'] = new GlyphGeometry { AdvanceWidth = 600 }
                }
            };
            var renderer = new GlyphRenderer();
            // " A" -> Space (simulated width 250) + A (width 600)
            // Scale 0.1
            // Space width = 1000/4 * 0.1 = 250 * 0.1 = 25.
            // A starts at 25.

            // Wait, logic says: if space not in font, width = UnitsPerEm/4 * scale.

            var paths = renderer.RenderText(" A", 0, 0, 100, fontData);

            // Should have 0 strokes? A has no strokes defined in test data, just AdvanceWidth.
            Assert.Empty(paths);

            // Let's verify position by adding strokes to A
            fontData.Glyphs['A'].Strokes.Add(new List<PdfPoint>{new PdfPoint(0,0)});
            paths = renderer.RenderText(" A", 0, 0, 100, fontData);

            Assert.Single(paths);
            Assert.Equal(25, paths[0][0].X);
        }
    }
}
