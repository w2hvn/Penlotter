using System.Collections.Generic;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Pdf
{
    public class ExtractedPageContent
    {
        public List<ExtractedText> TextBlocks { get; set; } = new List<ExtractedText>();
        public List<ExtractedShape> Shapes { get; set; } = new List<ExtractedShape>();
    }
}
