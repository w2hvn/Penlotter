using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfToGCode.Core.Fonts;
using PdfToGCode.Core.Pdf;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.GCode
{
    public class GCodeGenerator
    {
        public string Generate(List<ExtractedText> textBlocks, FontData fontData, GCodeSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("G21"); // mm
            sb.AppendLine("G90"); // Absolute positioning
            if (settings.UseG64) sb.AppendLine("G64"); // Constant velocity
            sb.AppendLine($"G0 Z{settings.ZUp} F{settings.TravelSpeed}");

            // Flatten glyphs
            var allGlyphs = textBlocks.SelectMany(t => t.Glyphs).ToList();

            // Sort logic: Top to Bottom, Left to Right
            // Group by approximate Y to handle slight misalignment
            // 5 points tolerance ~ 1.76mm
            // Using Origin (Baseline) for sorting is much better than BottomLeft,
            // because descenders (g, y, p, q, j) have lower BottomLeft than baseline.
            var sortedGlyphs = allGlyphs
                .OrderByDescending(g => Math.Round(g.Origin.Y / 5.0) * 5.0)
                .ThenBy(g => g.Origin.X)
                .ToList();

            foreach (var glyph in sortedGlyphs)
            {
                if (!fontData.Glyphs.ContainsKey(glyph.Character)) continue;

                var geometry = fontData.Glyphs[glyph.Character];

                // Calculate scale
                // Font units are arbitrary (UnitsPerEm)
                // We want final height in points to match glyph.FontSize
                // So scale = glyph.FontSize / UnitsPerEm
                double scale = glyph.FontSize / fontData.UnitsPerEm;

                foreach (var stroke in geometry.Strokes)
                {
                    if (stroke.Count == 0) continue;

                    // Move to start of stroke
                    // Use Origin (StartBaseLine) as the anchor point
                    var start = Transform(stroke[0], scale, glyph.Origin);

                    sb.AppendLine($"G0 X{start.X:F3} Y{start.Y:F3}");
                    sb.AppendLine($"G1 Z{settings.ZDown} F{settings.FeedRate}");

                    for (int i = 1; i < stroke.Count; i++)
                    {
                        var point = Transform(stroke[i], scale, glyph.Origin);
                        sb.AppendLine($"G1 X{point.X:F3} Y{point.Y:F3}");
                    }

                    sb.AppendLine($"G0 Z{settings.ZUp} F{settings.TravelSpeed}");
                }
            }

            sb.AppendLine("M2"); // End of program
            return sb.ToString();
        }

        private PdfPoint Transform(PdfPoint fontPoint, double scale, PdfPoint originPdf)
        {
            // fontPoint: coordinates in font units
            // scale: scaling factor to convert font units to PDF points
            // originPdf: position of the glyph in PDF points (baseline origin)

            // Calculate position in PDF points
            // fontPoint.Y is relative to baseline (positive up)
            // originPdf.Y is baseline Y in PDF coords (positive up)
            double xPoints = fontPoint.X * scale + originPdf.X;
            double yPoints = fontPoint.Y * scale + originPdf.Y;

            // Convert PDF points to G-code millimeters
            return CoordinateMapper.ConvertPdfToGCode(new PdfPoint(xPoints, yPoints));
        }
    }
}
