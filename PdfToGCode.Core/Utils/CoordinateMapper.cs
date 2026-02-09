namespace PdfToGCode.Core.Utils
{
    public static class CoordinateMapper
    {
        // Default PDF DPI is 72.
        public const double PdfDpi = 72.0;

        // Convert points to millimeters.
        public static double PointsToMm(double points)
        {
            return points * 25.4 / PdfDpi;
        }

        // Convert millimeters to points.
        public static double MmToPoints(double mm)
        {
            return mm * PdfDpi / 25.4;
        }

        public static PdfPoint ConvertPdfToGCode(PdfPoint point)
        {
            return new PdfPoint(PointsToMm(point.X), PointsToMm(point.Y));
        }

        /// <summary>
        /// Converts PDF coordinates (Bottom-Left origin) to Canvas/Screen coordinates (Top-Left origin).
        /// </summary>
        /// <param name="point">The point in PDF coordinates.</param>
        /// <param name="pageHeight">The height of the page in the same units as the point.</param>
        /// <returns>The point in Top-Left origin coordinates.</returns>
        public static PdfPoint ConvertPdfToCanvas(PdfPoint point, double pageHeight)
        {
            return new PdfPoint(point.X, pageHeight - point.Y);
        }
    }
}
