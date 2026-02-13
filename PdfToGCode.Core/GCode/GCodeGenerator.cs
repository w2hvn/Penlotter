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

                // Track if we are inside a word to use ZGap
                // Logic:
                // - Move to first glyph of block: ZSafe (Safe Lift)
                // - Move between glyphs in block: ZGap (Low Lift)
                // - Move between strokes in glyph: ZGap (Low Lift)
                // - End of block: ZSafe (Safe Lift) implicitly happens at start of next loop (or end)

                bool isFirstGlyphInBlock = true;

                foreach (var glyph in block.Glyphs)
                {
                    FontData glyphFont = fontData;
                    if (!glyphFont.Glyphs.ContainsKey(glyph.Character))
                    {
                        if (bodyFont != null && bodyFont.Glyphs.ContainsKey(glyph.Character))
                        {
                            glyphFont = bodyFont;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var geometry = glyphFont.Glyphs[glyph.Character];
                    double scale = (glyph.FontSize / glyphFont.UnitsPerEm) * settings.TextScale;

                    bool isFirstStrokeInGlyph = true;

                    foreach (var stroke in geometry.Strokes)
                    {
                        if (stroke.Count == 0) continue;

                        var start = Transform(stroke[0], scale, glyph.Origin);

                        // Determine Z Lift Type
                        // If First Stroke of First Glyph -> Already at Safe Z (from previous loop or init)
                        // If First Stroke of Subsequent Glyph -> Move with ZGap (if distance small?) -> User requested ZGap for intra-word
                        // If Subsequent Stroke of Any Glyph -> Move with ZGap

                        double liftHeight = (isFirstGlyphInBlock && isFirstStrokeInGlyph) ? settings.ZUp : settings.ZGap;

                        // Execute Lift Move (to new start point)
                        // If Z is currently Down, we must lift.
                        // If we are at Safe Z (start of job), we just move XY.
                        // But usually we finish previous stroke with Pen Up (Safe Z or Gap Z).

                        // Optimized Logic:
                        // 1. Pen is currently UP (at ZSafe or ZGap).
                        // 2. Move to new Start XY.
                        // 3. Pen Down.
                        // 4. Draw.
                        // 5. Pen Up (to ZGap or ZSafe depending on what comes next).

                        // Wait, standard G-code generators usually:
                        // G0 Z{Safe}
                        // G0 X{Start} Y{Start}
                        // G1 Z{Down}
                        // ...
                        // G0 Z{Safe}

                        // Modified Logic for Optimization:
                        // We need to know "Height needed for NEXT move".
                        // BUT my current loop structure appends "Pen Up" at the END of a stroke.
                        // I should change AppendPenUp to take a target height.

                        // Let's rewrite the movement sequence:
                        // Move to Start (at required height) -> Pen Down -> Draw -> Pen Up (to required height for NEXT move)

                        // Actually, standard is: Lift -> Move -> Down.
                        // So I just need to decide "Lift Height" before the "Move to Start".

                        if (isFirstGlyphInBlock && isFirstStrokeInGlyph)
                        {
                            // Inter-word move: Lift to Safe, Move to Start
                            AppendZMove(sb, settings, settings.ZUp);
                        }
                        else
                        {
                            // Intra-word/Intra-char move: Lift to Gap, Move to Start
                            AppendZMove(sb, settings, settings.ZGap);
                        }

                        // Move to XY
                        sb.AppendLine($"G0 X{start.X:F3} Y{start.Y:F3}");

                        // Pen Down
                        AppendPenDown(sb, settings);

                        // Draw
                        for (int i = 1; i < stroke.Count; i++)
                        {
                            var point = Transform(stroke[i], scale, glyph.Origin);
                            sb.AppendLine($"G1 X{point.X:F3} Y{point.Y:F3}");
                        }

                        // We delay the "Pen Up" logic to the start of next loop?
                        // No, we must lift before moving.
                        // My code above calls AppendZMove at START of loop.
                        // This implies the PREVIOUS loop left the pen DOWN?
                        // No, previous loop called AppendPenUp.

                        // Current implementation:
                        // ...
                        // AppendPenUp(sb, settings); // This lifts to ZUp.

                        // I need to REMOVE the unconditional AppendPenUp at end of loop.
                        // Instead, handle lifting at start of next, OR lift to specific height at end.

                        // If I lift to ZGap at end of stroke, then next loop starts.
                        // If next loop needs ZSafe, it will lift further? Yes.
                        // If next loop needs ZGap, we are already there.

                        // So: At end of stroke, lift to ZGap.
                        // UNLESS it's the last stroke of the last glyph? Then lift to ZSafe.

                        // Let's do this:
                        // Always lift to ZGap at end of stroke.
                        // EXCEPT if we are crossing blocks, we might want ZSafe.

                        // Ideally:
                        // Move XY (at current Z).
                        // If current Z is ZGap and we need ZSafe for this move? We lift first.

                        // Refactored Loop:
                        // 1. Determine required Z for Travel (Safe or Gap).
                        // 2. Lift to that Z.
                        // 3. Move XY.
                        // 4. Down.
                        // 5. Draw.
                        // 6. Lift to ??? (Ideally ZGap to save time, but if next is ZSafe we lift more).
                        // Actually, if we just lift to ZGap, and then move XY... if the move is "Safe" it implies we SHOULD have lifted to ZSafe.

                        // So correct logic:
                        // 1. Determine "Travel Mode" to THIS stroke.
                        //    - First stroke of block: Safe Travel.
                        //    - Others: Gap Travel.
                        // 2. Lift to appropriate Z (Safe or Gap).
                        // 3. Move XY.
                        // 4. Down.
                        // 5. Draw.

                        isFirstStrokeInGlyph = false;
                    }
                    isFirstGlyphInBlock = false;
                }

                // End of Block: Lift to Safe Z
                AppendZMove(sb, settings, settings.ZUp);
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

            bool hasLetters = false;
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    hasLetters = true;
                    if (!char.IsUpper(c)) return false;
                }
            }

            // Only consider Title if it contains at least one letter (and all letters are uppercase).
            // Pure numbers or symbols should use Body font.
            return hasLetters;
        }

        // Replaced simple AppendPenUp with generic AppendZMove for optimization
        private void AppendPenUp(StringBuilder sb, GCodeSettings settings)
        {
            AppendZMove(sb, settings, settings.ZUp);
        }

        private void AppendZMove(StringBuilder sb, GCodeSettings settings, double zHeight)
        {
            if (settings.IsServoMode)
            {
                sb.AppendLine($"M3 S{zHeight}");
            }
            else
            {
                sb.AppendLine($"G0 Z{zHeight} F{settings.TravelSpeed}");
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
