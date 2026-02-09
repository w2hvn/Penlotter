using System.Collections.Generic;

namespace PdfToGCode.Core.Fonts
{
    public class FontData
    {
        public double UnitsPerEm { get; set; } = 1000;
        public double Ascent { get; set; }
        public double Descent { get; set; }
        public Dictionary<char, GlyphGeometry> Glyphs { get; set; } = new Dictionary<char, GlyphGeometry>();
    }
}
