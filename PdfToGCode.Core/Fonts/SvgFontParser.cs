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

            // Handle namespace if present, but simpler to use LocalName
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
                // Allow space even if path is empty
                if (string.IsNullOrEmpty(unicodeStr)) continue;

                // Handle single char
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

            // Split by M or L, keeping the delimiter.
            // Using a simple regex that matches M or L followed by anything until next M or L
            var matches = Regex.Matches(d, @"([ML])\s*([^ML]*)");

            List<PdfPoint> currentStroke = null;

            foreach (Match match in matches)
            {
                var type = match.Groups[1].Value[0];
                var coordsPart = match.Groups[2].Value.Trim();

                // Parse coordinates
                var coords = coordsPart.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => double.TryParse(s, out double v) ? v : (double?)null)
                                       .Where(v => v.HasValue)
                                       .Select(v => v.Value)
                                       .ToList();

                // Expecting pairs
                for (int i = 0; i < coords.Count - 1; i += 2)
                {
                    var point = new PdfPoint(coords[i], coords[i + 1]);

                    if (type == 'M')
                    {
                        currentStroke = new List<PdfPoint>();
                        strokes.Add(currentStroke);
                        currentStroke.Add(point);
                        // Subsequent points in M command are treated as L (implicit line-to)
                        type = 'L';
                    }
                    else if (type == 'L')
                    {
                        if (currentStroke == null)
                        {
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
