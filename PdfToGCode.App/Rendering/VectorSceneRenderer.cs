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
        private readonly GlyphRenderer _glyphRenderer;

        public VectorSceneRenderer()
        {
            _glyphRenderer = new GlyphRenderer();
        }

        public void RenderScene(ZoomPanCanvas canvas, List<ExtractedText> textBlocks, FontData fontData, double pageHeight)
        {
            if (canvas == null) return;

            canvas.Children.Clear();
            canvas.Reset();

            if (textBlocks == null || fontData == null) return;

            var pathGeometry = new PathGeometry();

            foreach (var block in textBlocks)
            {
                foreach (var glyph in block.Glyphs)
                {
                    // Render glyph strokes
                    var strokes = _glyphRenderer.RenderText(glyph.Character.ToString(), glyph.Origin.X, glyph.Origin.Y, glyph.FontSize, fontData);

                    if (strokes == null) continue;

                    foreach (var stroke in strokes)
                    {
                        if (stroke.Count < 2) continue;

                        // Start point
                        var startPdf = stroke[0];
                        var startCanvas = CoordinateMapper.ConvertPdfToCanvas(startPdf, pageHeight);
                        var startPoint = new System.Windows.Point(startCanvas.X, startCanvas.Y);

                        var figure = new PathFigure
                        {
                            StartPoint = startPoint,
                            IsClosed = false // Single line font usually not closed, but can be
                        };

                        var segment = new PolyLineSegment();
                        for (int i = 1; i < stroke.Count; i++)
                        {
                            var pdfPoint = stroke[i];
                            var canvasPoint = CoordinateMapper.ConvertPdfToCanvas(pdfPoint, pageHeight);
                            segment.Points.Add(new System.Windows.Point(canvasPoint.X, canvasPoint.Y));
                        }

                        figure.Segments.Add(segment);
                        pathGeometry.Figures.Add(figure);
                    }
                }
            }

            if (pathGeometry.Figures.Count > 0)
            {
                var path = new Path
                {
                    Data = pathGeometry,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1
                };
                canvas.Children.Add(path);
            }
        }
    }
}
