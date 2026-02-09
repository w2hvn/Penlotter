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

            // Updated Regex to robustly capture commands and their arguments
            // This captures a letter (command) followed by anything that isn't a letter
            var matches = Regex.Matches(d, @"([MmLl])\s*([^MmLl]*)");

            List<PdfPoint> currentStroke = null;

            foreach (Match match in matches)
            {
                var type = char.ToUpper(match.Groups[1].Value[0]); // Normalize to uppercase
                var coordsPart = match.Groups[2].Value.Trim();

                // Parse coordinates - handle various separators
                var coords = coordsPart.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => double.TryParse(s, out double v) ? v : (double?)null)
                                       .Where(v => v.HasValue)
                                       .Select(v => v.Value)
                                       .ToList();

                // Expecting pairs (x, y)
                for (int i = 0; i < coords.Count - 1; i += 2)
                {
                    var point = new PdfPoint(coords[i], coords[i + 1]);

                    if (type == 'M')
                    {
                        // Start a new stroke (Move To)
                        currentStroke = new List<PdfPoint>();
                        strokes.Add(currentStroke);
                        currentStroke.Add(point);

                        // Subsequent pairs in M command are treated as L (implicit Line To)
                        type = 'L';
                    }
                    else if (type == 'L')
                    {
                        // Continue current stroke (Line To)
                        if (currentStroke == null)
                        {
                            // If L appears without prior M, start a new stroke (robustness)
                            currentStroke = new List<PdfPoint>();
                            strokes.Add(currentStroke);
                        }
                        currentStroke.Add(point);
                    }
                }
            }

            return strokes;
        }
    }
}
