using System;
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

        public void RenderScene(ZoomPanCanvas canvas, List<ExtractedText> textBlocks, FontData fontData, double pageHeight, double pageWidth)
        {
            if (canvas == null) return;

            canvas.Children.Clear();
            canvas.Reset();

            // Draw Page Border
            // In WPF Canvas, (0,0) is top-left.
            // In PDF, (0,0) is bottom-left.
            // CoordinateMapper.ConvertPdfToCanvas handles this flip.
            // However, for the page rectangle itself, we want to draw it from (0,0) to (Width, Height) in WPF space.
            // CoordinateMapper assumes a point (x,y) in PDF space.
            // PDF (0,0) -> WPF (0, H).
            // PDF (W,H) -> WPF (W, 0).
            // So the rectangle in WPF space is still (0,0) to (Width, Height), but filled differently?
            // Actually, WPF Canvas origin is top-left. A rectangle of WxH at (0,0) covers the visible area.

            var pageRect = new Rectangle
            {
                Width = pageWidth,
                Height = pageHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Transparent // Transparent so grid behind (if any) or just white background shows
            };

            // Add border at (0,0)
            Canvas.SetLeft(pageRect, 0);
            Canvas.SetTop(pageRect, 0);
            canvas.Children.Add(pageRect);

            if (textBlocks == null || textBlocks.Count == 0 || fontData == null) return;

            var pathGeometry = new PathGeometry();

            foreach (var block in textBlocks)
            {
                foreach (var glyph in block.Glyphs)
                {
                    var strokes = _glyphRenderer.RenderText(glyph.Character.ToString(), glyph.Origin.X, glyph.Origin.Y, glyph.FontSize, fontData);

                    if (strokes == null) continue;

                    foreach (var stroke in strokes)
                    {
                        if (stroke.Count < 2) continue;

                        var startPdf = stroke[0];
                        var startCanvas = CoordinateMapper.ConvertPdfToCanvas(startPdf, pageHeight);
                        var startPoint = new System.Windows.Point(startCanvas.X, startCanvas.Y);

                        var figure = new PathFigure
                        {
                            StartPoint = startPoint,
                            IsClosed = false
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
                    Stroke = Brushes.Red,
                    StrokeThickness = 0.5
                };
                canvas.Children.Add(path);
            }
        }
    }
}
