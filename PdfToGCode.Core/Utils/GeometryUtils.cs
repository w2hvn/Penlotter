using System;

namespace PdfToGCode.Core.Utils
{
    public struct PdfPoint
    {
        public double X { get; }
        public double Y { get; }

        public PdfPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public static PdfPoint operator +(PdfPoint a, PdfPoint b) => new PdfPoint(a.X + b.X, a.Y + b.Y);
        public static PdfPoint operator -(PdfPoint a, PdfPoint b) => new PdfPoint(a.X - b.X, a.Y - b.Y);
        public static PdfPoint operator *(PdfPoint a, double s) => new PdfPoint(a.X * s, a.Y * s);
    }
}
