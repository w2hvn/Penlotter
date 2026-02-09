using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
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

        public async Task RenderSceneAsync(ZoomPanCanvas canvas, List<PageData> pages, FontData fontData)
        {
            if (canvas == null) return;

            canvas.Children.Clear();
            canvas.Reset();

            if (pages == null || pages.Count == 0 || fontData == null) return;

            // 1. Calculate Geometries in Background
            // We compute the visual data (Paths) but creation of UI objects (PathGeometry) must be on UI thread or Freezable.
            // PathGeometry is a Freezable. If created on background thread, it must be frozen to be used on UI thread.
            // Let's create `PathGeometry` on background and Freeze it.

            var renderData = await Task.Run(() =>
            {
                var results = new List<(PathGeometry Geometry, double X, double Y, double Width, double Height, int PageNum)>();
                double currentX = 50;
                double margin = 50;

                foreach (var page in pages)
                {
                    // Geometry for this page
                    var pathGeometry = new PathGeometry();

                    if (page.TextBlocks != null)
                    {
                        foreach (var block in page.TextBlocks)
                        {
                            foreach (var glyph in block.Glyphs)
                            {
                                var strokes = _glyphRenderer.RenderText(glyph.Character.ToString(), glyph.Origin.X, glyph.Origin.Y, glyph.FontSize, fontData);
                                if (strokes == null) continue;

                                foreach (var stroke in strokes)
                                {
                                    if (stroke.Count < 2) continue;

                                    var startPdf = stroke[0];
                                    var startCanvas = CoordinateMapper.ConvertPdfToCanvas(startPdf, page.Height);
                                    // Local coordinates relative to page (0,0) at currentX
                                    // But PathGeometry points are absolute.
                                    // We can apply a Transform later, OR add currentX here.
                                    // Adding here is simpler for single geometry.

                                    var startPoint = new Point(startCanvas.X + currentX, startCanvas.Y);

                                    var figure = new PathFigure
                                    {
                                        StartPoint = startPoint,
                                        IsClosed = false
                                    };

                                    var segment = new PolyLineSegment();
                                    for (int i = 1; i < stroke.Count; i++)
                                    {
                                        var pdfPoint = stroke[i];
                                        var canvasPoint = CoordinateMapper.ConvertPdfToCanvas(pdfPoint, page.Height);
                                        segment.Points.Add(new Point(canvasPoint.X + currentX, canvasPoint.Y));
                                    }

                                    figure.Segments.Add(segment);
                                    pathGeometry.Figures.Add(figure);
                                }
                            }
                        }
                    }

                    if (pathGeometry.Figures.Count > 0)
                    {
                        pathGeometry.Freeze(); // Make it cross-thread accessible
                    }

                    results.Add((pathGeometry, currentX, 0, page.Width, page.Height, page.PageNumber));
                    currentX += page.Width + margin;
                }

                return results;
            });

            // 2. Update UI
            foreach (var item in renderData)
            {
                // Draw Page Border
                var pageRect = new Rectangle
                {
                    Width = item.Width,
                    Height = item.Height,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(pageRect, item.X);
                Canvas.SetTop(pageRect, item.Y);
                canvas.Children.Add(pageRect);

                // Draw Label
                var label = new TextBlock
                {
                    Text = $"Page {item.PageNum}",
                    Foreground = Brushes.Black,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                };
                Canvas.SetLeft(label, item.X);
                Canvas.SetTop(label, -25);
                canvas.Children.Add(label);

                // Draw Path
                if (item.Geometry.Figures.Count > 0)
                {
                    var path = new Path
                    {
                        Data = item.Geometry,
                        Stroke = Brushes.Red,
                        StrokeThickness = 0.5
                    };
                    canvas.Children.Add(path);
                }
            }
        }
    }

    public class PageData
    {
        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<ExtractedText> TextBlocks { get; set; } = new List<ExtractedText>();
    }
}
