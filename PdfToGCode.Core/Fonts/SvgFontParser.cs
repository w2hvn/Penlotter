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
                if (string.IsNullOrEmpty(unicodeStr) || unicodeStr.Length != 1) continue;

                var unicode = unicodeStr[0];
                var advXStr = glyph.Attribute("horiz-adv-x")?.Value;
                double advX = defaultAdvX;
                if (double.TryParse(advXStr, out double gVal)) advX = gVal;

                var d = glyph.Attribute("d")?.Value;

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

            var commands = Regex.Split(d, @"(?=[ML])").Where(s => !string.IsNullOrWhiteSpace(s));

            List<PdfPoint> currentStroke = null;

            foreach (var cmd in commands)
            {
                var trimmed = cmd.Trim();
                if (trimmed.Length == 0) continue;

                var type = trimmed[0];
                var coordsPart = trimmed.Substring(1).Trim();
                var coords = coordsPart.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (coords.Length < 2) continue;

                if (!double.TryParse(coords[0], out double x) || !double.TryParse(coords[1], out double y))
                    continue;

                var point = new PdfPoint(x, y);

                if (type == 'M')
                {
                    currentStroke = new List<PdfPoint>();
                    strokes.Add(currentStroke);
                    currentStroke.Add(point);
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

            return strokes;
        }
    }
}
