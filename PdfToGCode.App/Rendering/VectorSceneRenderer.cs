using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PdfToGCode.Core.Fonts;
using PdfToGCode.Core.Pdf;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.App.Rendering
{
    public class VectorSceneRenderer
    {
        private Canvas _canvas;
        private double _pageHeight;

        public VectorSceneRenderer(Canvas canvas)
        {
            _canvas = canvas;
        }

        public void SetPageHeight(double height)
        {
            _pageHeight = height;
        }

        public void Render(List<ExtractedText> textBlocks, FontData fontData)
        {
            _canvas.Children.Clear();

            foreach (var block in textBlocks)
            {
                foreach (var glyph in block.Glyphs)
                {
                    if (!fontData.Glyphs.ContainsKey(glyph.Character)) continue;

                    var geometry = fontData.Glyphs[glyph.Character];
                    double scale = glyph.FontSize / fontData.UnitsPerEm;

                    var glyphGroup = new GeometryGroup();

                    foreach (var stroke in geometry.Strokes)
                    {
                        if (stroke.Count == 0) continue;

                        var pathFigure = new PathFigure();
                        var start = Transform(stroke[0], scale, glyph.BottomLeft);
                        pathFigure.StartPoint = new System.Windows.Point(start.X, start.Y);

                        for (int i = 1; i < stroke.Count; i++)
                        {
                            var pt = Transform(stroke[i], scale, glyph.BottomLeft);
                            pathFigure.Segments.Add(new LineSegment(new System.Windows.Point(pt.X, pt.Y), true));
                        }

                        var pathGeometry = new PathGeometry();
                        pathGeometry.Figures.Add(pathFigure);
                        glyphGroup.Children.Add(pathGeometry);
                    }

                    var path = new Path
                    {
                        Data = glyphGroup,
                        Stroke = Brushes.Red,
                        StrokeThickness = 1.0
                    };

                    _canvas.Children.Add(path);
                }
            }
        }

        private PdfPoint Transform(PdfPoint fontPoint, double scale, PdfPoint originPdf)
        {
            double xPdf = fontPoint.X * scale + originPdf.X;
            double yPdf = fontPoint.Y * scale + originPdf.Y;

            double dpiScale = 96.0 / 72.0;

            double xWpf = xPdf * dpiScale;
            double yWpf = (_pageHeight - yPdf) * dpiScale;

            return new PdfPoint(xWpf, yWpf);
        }
    }
}
