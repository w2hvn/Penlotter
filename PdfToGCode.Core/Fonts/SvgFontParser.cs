using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Fonts
{
    public class SvgFontParser
    {
        public FontData Parse(string svgContent)
        {
            var fontData = new FontData();
            if (string.IsNullOrWhiteSpace(svgContent)) return fontData;

            XDocument doc;
            try
            {
                doc = XDocument.Parse(svgContent);
            }
            catch
            {
                return fontData;
            }

            var fontElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "font");
            if (fontElement == null) return fontData;

            var fontFace = fontElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "font-face");
            if (fontFace != null)
            {
                if (double.TryParse(fontFace.Attribute("units-per-em")?.Value, out double u)) fontData.UnitsPerEm = u;
                if (double.TryParse(fontFace.Attribute("ascent")?.Value, out double a)) fontData.Ascent = a;
                if (double.TryParse(fontFace.Attribute("descent")?.Value, out double d)) fontData.Descent = d;
            }

            var defaultAdvXStr = fontElement.Attribute("horiz-adv-x")?.Value;
            double defaultAdvX = 0;
            if (double.TryParse(defaultAdvXStr, out double val)) defaultAdvX = val;

            foreach (var glyph in fontElement.Descendants().Where(e => e.Name.LocalName == "glyph"))
            {
                var unicodeStr = glyph.Attribute("unicode")?.Value;
                if (string.IsNullOrEmpty(unicodeStr)) continue;

                if (unicodeStr.Length != 1) continue;

                var unicode = unicodeStr[0];
                var advXStr = glyph.Attribute("horiz-adv-x")?.Value;
                double advX = defaultAdvX;
                if (double.TryParse(advXStr, out double gVal)) advX = gVal;

                var d = glyph.Attribute("d")?.Value ?? string.Empty;

                var strokes = ParsePath(d);
                fontData.Glyphs[unicode] = new GlyphGeometry
                {
                    Unicode = unicode,
                    AdvanceWidth = advX,
                    Strokes = strokes
                };
            }

            return fontData;
        }

        private List<List<PdfPoint>> ParsePath(string d)
        {
            var strokes = new List<List<PdfPoint>>();
            if (string.IsNullOrWhiteSpace(d)) return strokes;

            // Updated Regex to capture commands and arguments
            var matches = Regex.Matches(d, @"([MLHVQCAZmlhvqcaz])\s*([^MLHVQCAZmlhvqcaz]*)");

            List<PdfPoint> currentStroke = null;
            PdfPoint currentPos = new PdfPoint(0, 0);
            PdfPoint startPos = new PdfPoint(0, 0); // Start of current subpath (for Z)

            foreach (Match match in matches)
            {
                var command = match.Groups[1].Value[0];
                var argsStr = match.Groups[2].Value;

                var args = argsStr.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => double.TryParse(s, out double v) ? v : (double?)null)
                                  .Where(v => v.HasValue)
                                  .Select(v => v.Value)
                                  .ToList();

                int i = 0;
                bool isRelative = char.IsLower(command);
                char type = char.ToUpper(command);

                while (true)
                {
                    if (type == 'M')
                    {
                        if (i + 1 >= args.Count) break;

                        double ax = args[i];
                        double ay = args[i+1];
                        i += 2;

                        var pt = isRelative ? new PdfPoint(currentPos.X + ax, currentPos.Y + ay) : new PdfPoint(ax, ay);

                        currentStroke = new List<PdfPoint>();
                        strokes.Add(currentStroke);
                        currentStroke.Add(pt);
                        currentPos = pt;
                        startPos = pt; // New subpath start

                        // Implicit L
                        type = 'L';
                        // If M is relative (m), implicit L is relative (l).
                        // If M is absolute (M), implicit L is absolute (L).
                        // isRelative stays the same.
                    }
                    else if (type == 'L')
                    {
                        if (i + 1 >= args.Count) break;
                        double ax = args[i];
                        double ay = args[i+1];
                        i += 2;

                        var pt = isRelative ? new PdfPoint(currentPos.X + ax, currentPos.Y + ay) : new PdfPoint(ax, ay);

                        if (currentStroke == null) { currentStroke = new List<PdfPoint>(); strokes.Add(currentStroke); currentStroke.Add(currentPos); }
                        currentStroke.Add(pt);
                        currentPos = pt;
                    }
                    else if (type == 'H')
                    {
                        if (i >= args.Count) break;
                        double ax = args[i];
                        i++;

                        var pt = isRelative ? new PdfPoint(currentPos.X + ax, currentPos.Y) : new PdfPoint(ax, currentPos.Y);

                        if (currentStroke == null) { currentStroke = new List<PdfPoint>(); strokes.Add(currentStroke); currentStroke.Add(currentPos); }
                        currentStroke.Add(pt);
                        currentPos = pt;
                    }
                    else if (type == 'V')
                    {
                        if (i >= args.Count) break;
                        double ay = args[i];
                        i++;

                        var pt = isRelative ? new PdfPoint(currentPos.X, currentPos.Y + ay) : new PdfPoint(currentPos.X, ay);

                        if (currentStroke == null) { currentStroke = new List<PdfPoint>(); strokes.Add(currentStroke); currentStroke.Add(currentPos); }
                        currentStroke.Add(pt);
                        currentPos = pt;
                    }
                    else if (type == 'C') // Cubic Bezier: (x1 y1 x2 y2 x y)
                    {
                        if (i + 5 >= args.Count) break;

                        double x1 = args[i], y1 = args[i+1];
                        double x2 = args[i+2], y2 = args[i+3];
                        double x = args[i+4], y = args[i+5];
                        i += 6;

                        var p1 = isRelative ? new PdfPoint(currentPos.X + x1, currentPos.Y + y1) : new PdfPoint(x1, y1);
                        var p2 = isRelative ? new PdfPoint(currentPos.X + x2, currentPos.Y + y2) : new PdfPoint(x2, y2);
                        var p3 = isRelative ? new PdfPoint(currentPos.X + x, currentPos.Y + y) : new PdfPoint(x, y);

                        if (currentStroke == null) { currentStroke = new List<PdfPoint>(); strokes.Add(currentStroke); currentStroke.Add(currentPos); }

                        // Linearize Bezier
                        AddBezierCubic(currentStroke, currentPos, p1, p2, p3);
                        currentPos = p3;
                    }
                    else if (type == 'Q') // Quadratic Bezier: (x1 y1 x y)
                    {
                        if (i + 3 >= args.Count) break;

                        double x1 = args[i], y1 = args[i+1];
                        double x = args[i+2], y = args[i+3];
                        i += 4;

                        var p1 = isRelative ? new PdfPoint(currentPos.X + x1, currentPos.Y + y1) : new PdfPoint(x1, y1);
                        var p2 = isRelative ? new PdfPoint(currentPos.X + x, currentPos.Y + y) : new PdfPoint(x, y);

                        if (currentStroke == null) { currentStroke = new List<PdfPoint>(); strokes.Add(currentStroke); currentStroke.Add(currentPos); }

                        // Convert Quad to Cubic
                        // CP1 = P0 + 2/3 (P1 - P0)
                        // CP2 = P2 + 2/3 (P1 - P2)
                        var cp1 = new PdfPoint(currentPos.X + (2.0/3.0)*(p1.X - currentPos.X), currentPos.Y + (2.0/3.0)*(p1.Y - currentPos.Y));
                        var cp2 = new PdfPoint(p2.X + (2.0/3.0)*(p2.X - p2.X), p2.Y + (2.0/3.0)*(p1.Y - p2.Y)); // Typo in previous logic: p2.X - p2.X is 0.
                        // Wait, P2 + 2/3(P1 - P2). Correct.
                        // Previous logic: p2.X + ... (p1.X - p2.X). Yes.
                        // I wrote p1.X - p2.X correctly in previous block but let's recheck.

                        // Correct logic for Quad to Cubic:
                        // C1 = P0 + (2/3)(P1 - P0)
                        // C2 = P2 + (2/3)(P1 - P2)

                        cp1 = new PdfPoint(currentPos.X + (2.0/3.0)*(p1.X - currentPos.X), currentPos.Y + (2.0/3.0)*(p1.Y - currentPos.Y));
                        cp2 = new PdfPoint(p2.X + (2.0/3.0)*(p1.X - p2.X), p2.Y + (2.0/3.0)*(p1.Y - p2.Y));

                        AddBezierCubic(currentStroke, currentPos, cp1, cp2, p2);
                        currentPos = p2;
                    }
                    else if (type == 'A') // Arc (Simplify to Line)
                    {
                         // Elliptical Arc: (rx ry x-axis-rotation large-arc-flag sweep-flag x y)
                         if (i + 6 >= args.Count) break;
                         // Skip flags and radii, just draw line to end point for single-line simplification
                         double x = args[i+5];
                         double y = args[i+6];
                         i += 7;

                         var endPt = isRelative ? new PdfPoint(currentPos.X + x, currentPos.Y + y) : new PdfPoint(x, y);

                         if (currentStroke == null) { currentStroke = new List<PdfPoint>(); strokes.Add(currentStroke); currentStroke.Add(currentPos); }
                         currentStroke.Add(endPt);
                         currentPos = endPt;
                    }
                    else if (type == 'Z')
                    {
                        // Close Path
                        if (currentStroke != null && currentStroke.Count > 0)
                        {
                            currentStroke.Add(startPos); // Line back to start
                            currentPos = startPos;
                        }
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return strokes;
        }

        private void AddBezierCubic(List<PdfPoint> stroke, PdfPoint p0, PdfPoint p1, PdfPoint p2, PdfPoint p3)
        {
            // Estimate length
            double dist01 = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
            double dist12 = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            double dist23 = Math.Sqrt(Math.Pow(p3.X - p2.X, 2) + Math.Pow(p3.Y - p2.Y, 2));
            double total = dist01 + dist12 + dist23;

            // Target step size. Fonts usually have larger coordinate space (e.g. 1000 or 2048 UPM).
            // So 1.0 might be too small (thousands of steps).
            // We should use a ratio of total UPM or just a larger value like 10-20 units?
            // If font is scale to PDF size later, we care about relative smoothness.
            // But here we work in Font Units.
            // Let's assume UPM ~ 1000. Step size 10 => 100 steps for full width.
            // Let's use 20 units as target resolution for font space.

            double resolution = 20.0;
            int steps = (int)Math.Ceiling(total / resolution);

            if (steps < 2) steps = 2;
            if (steps > 50) steps = 50; // Cap to avoid too many points for complex glyphs

            for (int i = 1; i <= steps; i++)
            {
                double t = i / (double)steps;
                double u = 1 - t;
                double tt = t * t;
                double uu = u * u;
                double uuu = uu * u;
                double ttt = tt * t;

                double x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
                double y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;

                stroke.Add(new PdfPoint(x, y));
            }
        }
    }
}
