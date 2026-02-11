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
        public string Generate(ExtractedPageContent content, FontData titleFont, FontData bodyFont, GCodeSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("G21"); // mm
            sb.AppendLine("G90"); // Absolute positioning
            if (settings.UseG64) sb.AppendLine("G64"); // Constant velocity

            // Initial move to Safe Z/Servo Up
            AppendPenUp(sb, settings);

            // 1. Shapes (Tables, Borders) First
            var sortedShapes = content.Shapes
                .OrderByDescending(s => GetBoundingBox(s).MaxY)
                .ToList();

            foreach (var shape in sortedShapes)
            {
                if (shape.Points.Count < 2) continue;

                // Move to start
                var start = CoordinateMapper.ConvertPdfToGCode(shape.Points[0]);
                sb.AppendLine($"G0 X{start.X:F3} Y{start.Y:F3}");

                // Pen Down
                AppendPenDown(sb, settings);

                for (int i = 1; i < shape.Points.Count; i++)
                {
                    var pt = CoordinateMapper.ConvertPdfToGCode(shape.Points[i]);
                    sb.AppendLine($"G1 X{pt.X:F3} Y{pt.Y:F3}");
                }

                // Pen Up
                AppendPenUp(sb, settings);
            }

            // 2. Text (Top-Down, Left-Right)
            // Need to associate font with block during iteration.
            // TextBlocks contain Glyphs. Glyphs are rendered per font.
            // We should sort TextBlocks first, then render using appropriate font.

            // Sort Blocks
            var sortedBlocks = content.TextBlocks
                .OrderByDescending(t => Math.Round(t.BottomLeft.Y / 5.0) * 5.0) // Approx Y
                .ThenBy(t => t.BottomLeft.X)
                .ToList();

            // Note: Previously we flattened to Glyphs.
            // Now we need block context for font selection.
            // But we still need sorting of individual glyphs?
            // Usually glyphs within a block are sorted.
            // Sorting blocks is generally safe for "Top-Down Left-Right".
            // However, if we want strict glyph sorting across blocks on same line, we might need to flatten with metadata.

            // Let's iterate blocks, and for each block use specific font.
            // But if multiple blocks are on same line (e.g. Title part 1, Title part 2), block sorting handles it.

            foreach (var block in sortedBlocks)
            {
                var fontData = IsAllUpperCase(block.Text) ? titleFont : bodyFont;

                foreach (var glyph in block.Glyphs)
                {
                    if (!fontData.Glyphs.ContainsKey(glyph.Character)) continue;

                    var geometry = fontData.Glyphs[glyph.Character];
                    double scale = glyph.FontSize / fontData.UnitsPerEm;

                    foreach (var stroke in geometry.Strokes)
                    {
                        if (stroke.Count == 0) continue;

                        var start = Transform(stroke[0], scale, glyph.Origin);

                        sb.AppendLine($"G0 X{start.X:F3} Y{start.Y:F3}");

                        AppendPenDown(sb, settings);

                        for (int i = 1; i < stroke.Count; i++)
                        {
                            var point = Transform(stroke[i], scale, glyph.Origin);
                            sb.AppendLine($"G1 X{point.X:F3} Y{point.Y:F3}");
                        }

                        AppendPenUp(sb, settings);
                    }
                }
            }

            sb.AppendLine("M2");
            return sb.ToString();
        }

        // Overload for single font
        public string Generate(ExtractedPageContent content, FontData fontData, GCodeSettings settings)
        {
            return Generate(content, fontData, fontData, settings);
        }

        private bool IsAllUpperCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (char c in text)
            {
                if (char.IsLetter(c) && !char.IsUpper(c)) return false;
            }
            return true;
        }

        private void AppendPenUp(StringBuilder sb, GCodeSettings settings)
        {
            if (settings.IsServoMode)
            {
                sb.AppendLine($"M3 S{settings.ZUp}");
            }
            else
            {
                sb.AppendLine($"G0 Z{settings.ZUp} F{settings.TravelSpeed}");
            }
        }

        private void AppendPenDown(StringBuilder sb, GCodeSettings settings)
        {
            if (settings.IsServoMode)
            {
                sb.AppendLine($"M3 S{settings.ZDown}");
                sb.AppendLine("G4 P0.1");
            }
            else
            {
                sb.AppendLine($"G1 Z{settings.ZDown} F{settings.FeedRate}");
            }
        }

        private PdfPoint Transform(PdfPoint fontPoint, double scale, PdfPoint originPdf)
        {
            double xPoints = fontPoint.X * scale + originPdf.X;
            double yPoints = fontPoint.Y * scale + originPdf.Y;
            return CoordinateMapper.ConvertPdfToGCode(new PdfPoint(xPoints, yPoints));
        }

        private (double MaxY, double MinY) GetBoundingBox(ExtractedShape shape)
        {
            if (shape.Points.Count == 0) return (0, 0);
            double max = double.MinValue;
            double min = double.MaxValue;
            foreach (var p in shape.Points)
            {
                if (p.Y > max) max = p.Y;
                if (p.Y < min) min = p.Y;
            }
            return (max, min);
        }
    }
}
