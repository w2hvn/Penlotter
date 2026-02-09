using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.Core.Pdf
{
    public class PdfTextLayoutExtractor
    {
        public (double Width, double Height) GetPageDimensions(string pdfPath, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return (0, 0);

            using (var document = PdfDocument.Open(pdfPath))
            {
                if (pageNumber > document.NumberOfPages || pageNumber < 1) return (0, 0);
                var page = document.GetPage(pageNumber);
                return (page.Width, page.Height);
            }
        }

        public List<ExtractedText> ExtractWords(string pdfPath)
        {
            var results = new List<ExtractedText>();
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return results;

            using (var document = PdfDocument.Open(pdfPath))
            {
                foreach (var page in document.GetPages())
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
                                Width = letter.Width,
                                Height = letter.GlyphRectangle.Height,
                                FontSize = letter.FontSize,
                                FontName = letter.FontName
                            });
                        }

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
            }
            return results;
        }
    }
}
