using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using CorePdfPoint = PdfToGCode.Core.Utils.PdfPoint;

namespace PdfToGCode.Core.Pdf
{
    public class PdfTextLayoutExtractor
    {
        public (double Width, double Height) GetPageDimensions(string pdfPath, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return (0, 0);

            try
            {
                using (var document = PdfDocument.Open(pdfPath))
                {
                    if (pageNumber > document.NumberOfPages || pageNumber < 1) return (0, 0);
                    var page = document.GetPage(pageNumber);
                    return (page.Width, page.Height);
                }
            }
            catch
            {
                return (0, 0);
            }
        }

        public ExtractedPageContent ExtractPageContent(string pdfPath, int pageNumber)
        {
            var content = new ExtractedPageContent();
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return content;

            try
            {
                using (var document = PdfDocument.Open(pdfPath))
                {
                    if (pageNumber > document.NumberOfPages || pageNumber < 1) return content;
                    var page = document.GetPage(pageNumber);

                    ExtractWordsFromPage(page, content.TextBlocks);
                    ExtractShapesFromPage(page, content.Shapes);
                }
            }
            catch
            {
                // Handle exceptions
            }
            return content;
        }

        // Backward compatibility method
        public (List<ExtractedText> Text, double Width, double Height) ExtractPage(string pdfPath, int pageNumber)
        {
            var results = new List<ExtractedText>();
            double width = 0;
            double height = 0;

            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return (results, width, height);

            try
            {
                using (var document = PdfDocument.Open(pdfPath))
                {
                    if (pageNumber > document.NumberOfPages || pageNumber < 1) return (results, 0, 0);
                    var page = document.GetPage(pageNumber);
                    width = page.Width;
                    height = page.Height;

                    ExtractWordsFromPage(page, results);
                }
            }
            catch
            {
                // Handle exceptions
            }

            return (results, width, height);
        }

        private void ExtractWordsFromPage(Page page, List<ExtractedText> results)
        {
            var words = page.GetWords();
            foreach (var word in words)
            {
                if (word.Letters.Count == 0) continue;

                var glyphs = new List<ExtractedGlyph>();
                foreach (var letter in word.Letters)
                {
                    if (string.IsNullOrEmpty(letter.Value)) continue;

                    glyphs.Add(new ExtractedGlyph
                    {
                        Character = letter.Value[0],
                        BottomLeft = new CorePdfPoint(letter.GlyphRectangle.BottomLeft.X, letter.GlyphRectangle.BottomLeft.Y),
                        Origin = new CorePdfPoint(letter.StartBaseLine.X, letter.StartBaseLine.Y),
                        Width = letter.Width,
                        Height = letter.GlyphRectangle.Height,
                        FontSize = letter.FontSize,
                        FontName = letter.FontName ?? string.Empty
                    });
                }

                if (glyphs.Count == 0) continue;

                results.Add(new ExtractedText
                {
                    Text = word.Text,
                    BottomLeft = new CorePdfPoint(word.BoundingBox.BottomLeft.X, word.BoundingBox.BottomLeft.Y),
                    Width = word.BoundingBox.Width,
                    Height = word.BoundingBox.Height,
                    FontSize = word.Letters[0].FontSize,
                    PageNumber = page.Number,
                    Glyphs = glyphs
                });
            }
        }

        private void ExtractShapesFromPage(Page page, List<ExtractedShape> shapes)
        {
            // Iterate all paths (Stroked and Filled to catch tables/lines)
            foreach (var path in page.Paths)
            {
                if (!path.IsStroked && !path.IsFilled) continue;

                foreach (var subpath in path)
                {
                    var currentPoints = new List<CorePdfPoint>();
                    bool isClosed = false;

                    try
                    {
                        foreach (var command in subpath.Commands)
                        {
                            var name = command.GetType().Name;
                            dynamic cmd = command;

                            // Handle MoveTo
                            if (name == "MoveTo")
                            {
                                currentPoints.Add(new CorePdfPoint(cmd.Point.X, cmd.Point.Y));
                            }
                            // Handle LineTo
                            else if (name == "LineTo")
                            {
                                currentPoints.Add(new CorePdfPoint(cmd.Point.X, cmd.Point.Y));
                            }
                            // Handle ClosePath
                            else if (name == "ClosePath")
                            {
                                isClosed = true;
                                if (currentPoints.Count > 0) currentPoints.Add(currentPoints[0]);
                            }
                            // Handle Curves
                            else if (name == "CubicBezierCurve")
                            {
                                var last = currentPoints.LastOrDefault();
                                if (last.Equals(default(CorePdfPoint))) last = new CorePdfPoint(0,0);

                                // Bezier uses FirstControlPoint, SecondControlPoint, EndPoint
                                // Sometimes names vary? Check EndPoint vs Point?
                                // Standard PdfPig is StartPoint, FirstControlPoint, SecondControlPoint, EndPoint (for Cubic)

                                AddBezier(currentPoints, last,
                                          new CorePdfPoint(cmd.FirstControlPoint.X, cmd.FirstControlPoint.Y),
                                          new CorePdfPoint(cmd.SecondControlPoint.X, cmd.SecondControlPoint.Y),
                                          new CorePdfPoint(cmd.EndPoint.X, cmd.EndPoint.Y));
                            }
                            // Handle Rectangle? (Usually converted to Move/Line in subpath?)
                            // If direct op exists:
                            else if (name == "Rectangle") // Rarely used in path commands, usually x,y,w,h
                            {
                                // If present, convert to 4 lines
                                // dynamic rect = cmd; ...
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed commands to prevent full failure
                    }

                    if (currentPoints.Count > 1)
                    {
                        shapes.Add(new ExtractedShape { Points = currentPoints, IsClosed = isClosed, StrokeWidth = path.LineWidth });
                    }
                }
            }
        }

        private void AddBezier(List<CorePdfPoint> points, CorePdfPoint p0, CorePdfPoint p1, CorePdfPoint p2, CorePdfPoint p3)
        {
            int steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                double t = i / (double)steps;
                double u = 1 - t;
                double tt = t * t;
                double uu = u * u;
                double uuu = uu * u;
                double ttt = tt * t;

                double x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
                double y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;

                points.Add(new CorePdfPoint(x, y));
            }
        }
    }
}
