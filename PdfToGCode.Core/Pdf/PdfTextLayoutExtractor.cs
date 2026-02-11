using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;
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
            // For older PdfPig versions, iterating Paths yields PdfPath objects.
            // If PdfPath doesn't have Commands, we can't extract geometry easily.
            // But PdfPig 0.1.x usually has Commands or similar.
            // If Commands is missing in this version, maybe we can cast to IPath?
            // Or maybe iterate ExperimentalAccess.Paths?
            // The warning said ExperimentalAccess is obsolete, use Page.Paths.
            // Let's assume Page.Paths works but we need to find how to get segments.

            // NOTE: UglyToad.PdfPig version 0.1.13 PdfPath definition:
            // It implements IEnumerable<PdfSubpath>.
            // So we can iterate the path itself to get subpaths.

            foreach (var path in page.Paths)
            {
                // Shapes can be stroked (lines, borders) or filled (solid blocks, thick lines).
                if (!path.IsStroked)
                {
                    if (!path.IsFilled) continue;
                    // If only filled, ignore if White (background/mask)
                    if (IsWhite(path.FillColor)) continue;
                }

                foreach (var subpath in path) // Iterate subpaths directly
                {
                    var currentPoints = new List<CorePdfPoint>();
                    bool isClosed = false;

                    // Iterate commands in subpath
                    foreach (var command in subpath.Commands)
                    {
                        var name = command.GetType().Name;
                        dynamic cmd = command;

                        if (name == "MoveTo")
                        {
                            currentPoints.Add(new CorePdfPoint(cmd.Point.X, cmd.Point.Y));
                        }
                        else if (name == "Move")
                        {
                            currentPoints.Add(new CorePdfPoint(cmd.Location.X, cmd.Location.Y));
                        }
                        else if (name == "LineTo")
                        {
                            currentPoints.Add(new CorePdfPoint(cmd.Point.X, cmd.Point.Y));
                        }
                        else if (name == "Line")
                        {
                            currentPoints.Add(new CorePdfPoint(cmd.To.X, cmd.To.Y));
                        }
                        else if (name == "Rectangle" || name == "AppendRectangle")
                        {
                            try
                            {
                                // PdfPig 're' operation is typically AppendRectangle class.
                                // Properties: LowerLeftX, LowerLeftY, Width, Height.
                                double x = cmd.LowerLeftX;
                                double y = cmd.LowerLeftY;
                                double w = cmd.Width;
                                double h = cmd.Height;

                                // (x, y) -> (x+w, y) -> (x+w, y+h) -> (x, y+h) -> (x, y)
                                currentPoints.Add(new CorePdfPoint(x, y));
                                currentPoints.Add(new CorePdfPoint(x + w, y));
                                currentPoints.Add(new CorePdfPoint(x + w, y + h));
                                currentPoints.Add(new CorePdfPoint(x, y + h));
                                currentPoints.Add(new CorePdfPoint(x, y));
                            }
                            catch
                            {
                                // Fallback for safety if property names differ unexpectedly
                            }
                        }
                        else if (name == "ClosePath" || name == "Close")
                        {
                            isClosed = true;
                            if (currentPoints.Count > 0) currentPoints.Add(currentPoints[0]);
                        }
                        else if (name == "CubicBezierCurve")
                        {
                            var last = currentPoints.LastOrDefault();
                            if (last.Equals(default(CorePdfPoint))) last = new CorePdfPoint(0,0);

                            AddBezier(currentPoints, last,
                                      new CorePdfPoint(cmd.FirstControlPoint.X, cmd.FirstControlPoint.Y),
                                      new CorePdfPoint(cmd.SecondControlPoint.X, cmd.SecondControlPoint.Y),
                                      new CorePdfPoint(cmd.EndPoint.X, cmd.EndPoint.Y));
                        }
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

        private bool IsWhite(IColor color)
        {
            if (color == null) return false;

            // Check RGB
            if (color is RGBColor rgb)
            {
                // PDF RGB is 0-1 usually, but check just in case.
                // Assuming 0-1 for double values in PdfPig.
                return rgb.R >= 0.99 && rgb.G >= 0.99 && rgb.B >= 0.99;
            }

            // Check Grayscale
            if (color is GrayColor gray)
            {
                return gray.Gray >= 0.99;
            }

            // Check CMYK
            if (color is CMYKColor cmyk)
            {
                // White is C=0, M=0, Y=0, K=0
                return cmyk.C <= 0.01 && cmyk.M <= 0.01 && cmyk.Y <= 0.01 && cmyk.K <= 0.01;
            }

            return false;
        }
    }
}
