using System.Collections.Generic;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Pdf
{
    public class ExtractedText
    {
        public string Text { get; set; }
        public PdfPoint BottomLeft { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double FontSize { get; set; }
        public int PageNumber { get; set; }
        public List<ExtractedGlyph> Glyphs { get; set; } = new List<ExtractedGlyph>();
    }
}
