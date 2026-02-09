using System.Collections.Generic;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Fonts
{
    public class GlyphRenderer
    {
        /// <summary>
        /// Renders text into a list of strokes (paths).
        /// Coordinates are in the same system as the input (usually Cartesian with Y up).
        /// </summary>
        /// <param name="text">The text to render.</param>
        /// <param name="x">The X coordinate of the starting position (baseline).</param>
        /// <param name="y">The Y coordinate of the starting position (baseline).</param>
        /// <param name="fontSize">The font size.</param>
        /// <param name="fontData">The font data.</param>
        /// <returns>A list of strokes, where each stroke is a list of points.</returns>
        public List<List<PdfPoint>> RenderText(string text, double x, double y, double fontSize, FontData fontData)
        {
            var result = new List<List<PdfPoint>>();
            if (string.IsNullOrEmpty(text) || fontData == null) return result;

            double currentX = x;
            double scale = fontSize / fontData.UnitsPerEm;

            foreach (char c in text)
            {
                if (!fontData.Glyphs.TryGetValue(c, out var glyph))
                {
                    // Fallback to space or question mark if needed, or just skip
                    // For now, let's try to find a space if char is missing
                    if (c == ' ')
                    {
                         // Space usually has advance width but no strokes
                         if (fontData.Glyphs.TryGetValue(' ', out var spaceGlyph))
                         {
                             currentX += spaceGlyph.AdvanceWidth * scale;
                         }
                         else
                         {
                             // Default space width if not defined?
                             currentX += (fontData.UnitsPerEm / 4.0) * scale;
                         }
                         continue;
                    }
                    continue;
                }

                foreach (var stroke in glyph.Strokes)
                {
                    var newStroke = new List<PdfPoint>();
                    foreach (var point in stroke)
                    {
                        double px = currentX + point.X * scale;
                        double py = y + point.Y * scale; // Assuming Y is up and relative to baseline
                        newStroke.Add(new PdfPoint(px, py));
                    }
                    result.Add(newStroke);
                }

                currentX += glyph.AdvanceWidth * scale;
            }

            return result;
        }
    }
}
