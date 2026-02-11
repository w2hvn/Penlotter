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

        public async Task RenderSceneAsync(ZoomPanCanvas canvas, List<PageData> pages, FontData titleFont, FontData bodyFont)
        {
            if (canvas == null) return;

            canvas.Children.Clear();
            canvas.Reset();

            if (pages == null || pages.Count == 0 || titleFont == null || bodyFont == null) return;

            // 1. Calculate Geometries
            var renderData = await Task.Run(() =>
            {
                var results = new List<(PathGeometry TextGeometry, PathGeometry ShapeGeometry, double X, double Y, double Width, double Height, int PageNum)>();
                double currentX = 50;
                double margin = 50;

                foreach (var page in pages)
                {
                    var textGeometry = new PathGeometry();
                    var shapeGeometry = new PathGeometry();

                    // Process Shapes
                    if (page.Content.Shapes != null)
                    {
                        foreach (var shape in page.Content.Shapes)
                        {
                            if (shape.Points.Count < 2) continue;

                            var startPdf = shape.Points[0];
                            var startCanvas = CoordinateMapper.ConvertPdfToCanvas(startPdf, page.Height);
                            var startPoint = new Point(startCanvas.X + currentX, startCanvas.Y);

                            var figure = new PathFigure
                            {
                                StartPoint = startPoint,
                                IsClosed = shape.IsClosed
                            };

                            var segment = new PolyLineSegment();
                            for (int i = 1; i < shape.Points.Count; i++)
                            {
                                var pdfPoint = shape.Points[i];
                                var canvasPoint = CoordinateMapper.ConvertPdfToCanvas(pdfPoint, page.Height);
                                segment.Points.Add(new Point(canvasPoint.X + currentX, canvasPoint.Y));
                            }

                            figure.Segments.Add(segment);
                            shapeGeometry.Figures.Add(figure);
                        }
                    }

                    // Process Text
                    if (page.Content.TextBlocks != null)
                    {
                        foreach (var block in page.Content.TextBlocks)
                        {
                            // Determine font based on text content (All Caps = Title)
                            var fontToUse = IsAllUpperCase(block.Text) ? titleFont : bodyFont;

                            foreach (var glyph in block.Glyphs)
                            {
                                // Font Fallback Logic
                                FontData glyphFont = fontToUse;
                                if (!glyphFont.Glyphs.ContainsKey(glyph.Character))
                                {
                                    if (bodyFont != null && bodyFont.Glyphs.ContainsKey(glyph.Character))
                                    {
                                        glyphFont = bodyFont;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                var strokes = _glyphRenderer.RenderText(glyph.Character.ToString(), glyph.Origin.X, glyph.Origin.Y, glyph.FontSize, glyphFont);
                                if (strokes == null) continue;

                                foreach (var stroke in strokes)
                                {
                                    if (stroke.Count < 2) continue;

                                    var startPdf = stroke[0];
                                    var startCanvas = CoordinateMapper.ConvertPdfToCanvas(startPdf, page.Height);
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
                                    textGeometry.Figures.Add(figure);
                                }
                            }
                        }
                    }

                    if (textGeometry.CanFreeze) textGeometry.Freeze();
                    if (shapeGeometry.CanFreeze) shapeGeometry.Freeze();

                    results.Add((textGeometry, shapeGeometry, currentX, 0, page.Width, page.Height, page.PageNumber));
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

                // Draw Shapes (Black/Gray)
                if (item.ShapeGeometry.Figures.Count > 0)
                {
                    var path = new Path
                    {
                        Data = item.ShapeGeometry,
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    };
                    canvas.Children.Add(path);
                }

                // Draw Text (Red)
                if (item.TextGeometry.Figures.Count > 0)
                {
                    var path = new Path
                    {
                        Data = item.TextGeometry,
                        Stroke = Brushes.Red,
                        StrokeThickness = 0.5
                    };
                    canvas.Children.Add(path);
                }
            }
        }

        private bool IsAllUpperCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            bool hasLetters = false;
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    hasLetters = true;
                    if (!char.IsUpper(c)) return false;
                }
            }
            return hasLetters;
        }
    }

    public class PageData
    {
        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public ExtractedPageContent Content { get; set; } = new ExtractedPageContent();
        public List<ExtractedText> TextBlocks
        {
            get => Content.TextBlocks;
            set => Content.TextBlocks = value;
        }
    }
}
