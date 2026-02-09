using System.Collections.Generic;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Pdf
{
    public class ExtractedShape
    {
        public List<PdfPoint> Points { get; set; } = new List<PdfPoint>();
        public bool IsClosed { get; set; }
        public double StrokeWidth { get; set; }
    }
}
