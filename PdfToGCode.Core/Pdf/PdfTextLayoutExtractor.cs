using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
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

        public List<ExtractedText> ExtractWords(string pdfPath)
        {
            // Delegates to ExtractPage logic but for all pages?
            // Or keep as is.
            // Let's implement ExtractPage and reuse logic if possible, or just duplicate loop content.
            var results = new List<ExtractedText>();
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath)) return results;

            try
            {
                using (var document = PdfDocument.Open(pdfPath))
                {
                    foreach (var page in document.GetPages())
                    {
                        ExtractWordsFromPage(page, results);
                    }
                }
            }
            catch
            {
                // Handle exceptions
            }
            return results;
        }

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
    }
}
