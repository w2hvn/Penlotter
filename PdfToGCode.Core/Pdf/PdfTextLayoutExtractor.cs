using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using PdfToGCode.Core.Utils;

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
                        BottomLeft = new PdfPoint(letter.GlyphRectangle.BottomLeft.X, letter.GlyphRectangle.BottomLeft.Y),
                        Origin = new PdfPoint(letter.StartBaseLine.X, letter.StartBaseLine.Y),
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
                    BottomLeft = new PdfPoint(word.BoundingBox.BottomLeft.X, word.BoundingBox.BottomLeft.Y),
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
            // PdfPig ExperimentalAccess to paths
            var paths = page.ExperimentalAccess.Paths;
            foreach (var path in paths)
            {
                if (!path.IsStroked) continue; // Only stroke, ignore filled for now (G-code usually cuts strokes)

                var shape = new ExtractedShape();
                shape.IsClosed = false; // Need to determine from subpaths

                foreach (var subpath in path.GetSubpaths())
                {
                    // Linearize subpath commands to PdfPoints
                    var points = new List<PdfPoint>();
                    foreach (var command in subpath.Commands)
                    {
                        if (command is UglyToad.PdfPig.Graphics.Operations.PathConstruction.MoveTo move)
                        {
                            points.Add(new PdfPoint(move.Point.X, move.Point.Y));
                        }
                        else if (command is UglyToad.PdfPig.Graphics.Operations.PathConstruction.LineTo line)
                        {
                            points.Add(new PdfPoint(line.Point.X, line.Point.Y));
                        }
                        else if (command is UglyToad.PdfPig.Graphics.Operations.PathConstruction.ClosePath)
                        {
                            shape.IsClosed = true;
                            // Add start point to end if not already?
                            if (points.Count > 0)
                            {
                                points.Add(points[0]);
                            }
                        }
                        // Handle Bezier curves? PdfPig might have CubicBezierCurve
                        else if (command is UglyToad.PdfPig.Graphics.Operations.PathConstruction.CubicBezierCurve cubic)
                        {
                            // Flatten bezier
                            var last = points.LastOrDefault();
                            if (last.Equals(default(PdfPoint))) last = new PdfPoint(0,0); // Should have MoveTo before

                            AddBezier(points, last,
                                      new PdfPoint(cubic.FirstControlPoint.X, cubic.FirstControlPoint.Y),
                                      new PdfPoint(cubic.SecondControlPoint.X, cubic.SecondControlPoint.Y),
                                      new PdfPoint(cubic.EndPoint.X, cubic.EndPoint.Y));
                        }
                    }

                    if (points.Count > 1)
                    {
                        // Each subpath is a shape or part of shape.
                        // Ideally return list of list points, but ExtractedShape has List<PdfPoint>.
                        // So one ExtractedShape = one subpath for simplicity.
                        var subShape = new ExtractedShape { Points = points, IsClosed = shape.IsClosed, StrokeWidth = path.LineWidth };
                        shapes.Add(subShape);
                    }
                }
            }
        }

        private void AddBezier(List<PdfPoint> points, PdfPoint p0, PdfPoint p1, PdfPoint p2, PdfPoint p3)
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

                points.Add(new PdfPoint(x, y));
            }
        }
    }
}
