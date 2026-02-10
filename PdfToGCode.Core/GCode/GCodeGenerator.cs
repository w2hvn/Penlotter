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
        public string Generate(ExtractedPageContent content, FontData fontData, GCodeSettings settings)
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
            var allGlyphs = content.TextBlocks.SelectMany(t => t.Glyphs).ToList();

            var sortedGlyphs = allGlyphs
                .OrderByDescending(g => Math.Round(g.Origin.Y / 5.0) * 5.0)
                .ThenBy(g => g.Origin.X)
                .ToList();

            foreach (var glyph in sortedGlyphs)
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

            sb.AppendLine("M2");
            return sb.ToString();
        }

        private void AppendPenUp(StringBuilder sb, GCodeSettings settings)
        {
            if (settings.IsServoMode)
            {
                // M3 S<Value> or M5 usually.
                // Common servo: M3 S0 (Up) / M3 S255 (Down) depending on config.
                // We use ZUp as the S-value for UP.
                sb.AppendLine($"M3 S{settings.ZUp}");
                // Or "M5" if strictly Spindle Off?
                // Servo plotters usually keep M3 active and vary S.
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
                // We use ZDown as the S-value for DOWN.
                sb.AppendLine($"M3 S{settings.ZDown}");
                // Add optional delay? (G4 P0.2)
                sb.AppendLine("G4 P0.1"); // Small dwell for servo to move
            }
            else
            {
                sb.AppendLine($"G1 Z{settings.ZDown} F{settings.FeedRate}");
            }
        }

        // Backward compatibility
        public string Generate(List<ExtractedText> textBlocks, FontData fontData, GCodeSettings settings)
        {
            return Generate(new ExtractedPageContent { TextBlocks = textBlocks }, fontData, settings);
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
