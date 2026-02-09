using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Pdf
{
    public class ExtractedGlyph
    {
        public char Character { get; set; }
        public PdfPoint BottomLeft { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double FontSize { get; set; }
        public string FontName { get; set; }
    }
}
