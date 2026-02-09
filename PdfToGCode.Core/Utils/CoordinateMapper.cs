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
    }
}
