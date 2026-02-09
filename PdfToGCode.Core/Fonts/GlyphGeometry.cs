using System.Collections.Generic;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Fonts
{
    public class GlyphGeometry
    {
        public char Unicode { get; set; }
        public double AdvanceWidth { get; set; }
        public List<List<PdfPoint>> Strokes { get; set; } = new List<List<PdfPoint>>();
    }
}
