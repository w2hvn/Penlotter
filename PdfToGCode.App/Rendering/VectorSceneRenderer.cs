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

        public void RenderScene(ZoomPanCanvas canvas, List<PageData> pages, FontData fontData)
        {
            if (canvas == null) return;

            canvas.Children.Clear();
            canvas.Reset();

            if (pages == null || pages.Count == 0 || fontData == null) return;

            // Layout strategy: Stack horizontally with margin
            double currentX = 50; // Initial margin
            double margin = 50;   // Gap between pages

            foreach (var page in pages)
            {
                // Draw Page Border (Physical sheet)
                var pageRect = new Rectangle
                {
                    Width = page.Width,
                    Height = page.Height,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Fill = Brushes.Transparent
                };

                Canvas.SetLeft(pageRect, currentX);
                Canvas.SetTop(pageRect, 0);
                canvas.Children.Add(pageRect);

                // Add page number label
                var label = new TextBlock
                {
                    Text = $"Page {page.PageNumber}",
                    Foreground = Brushes.Black,
                    FontSize = 14,
                    FontWeight = System.Windows.FontWeights.Bold
                };
                Canvas.SetLeft(label, currentX);
                Canvas.SetTop(label, -25); // Above page
                canvas.Children.Add(label);

                // Check text blocks
                if (page.TextBlocks != null)
                {
                    var pathGeometry = new PathGeometry();

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
                                // Convert relative to page top-left, then add currentX offset
                                var startCanvas = CoordinateMapper.ConvertPdfToCanvas(startPdf, page.Height);
                                var startPoint = new System.Windows.Point(startCanvas.X + currentX, startCanvas.Y);

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
                                    segment.Points.Add(new System.Windows.Point(canvasPoint.X + currentX, canvasPoint.Y));
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

                // Advance X
                currentX += page.Width + margin;
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
