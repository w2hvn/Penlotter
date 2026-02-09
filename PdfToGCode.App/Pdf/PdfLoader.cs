using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using PdfToGCode.Core.Pdf;
using PdfiumViewer;
using System.Drawing.Imaging;

namespace PdfToGCode.App.Pdf
{
    public class PdfLoader
    {
        private readonly PdfTextLayoutExtractor _extractor = new PdfTextLayoutExtractor();

        /// <summary>
        /// Loads PDF page, extracts text and renders image.
        /// </summary>
        /// <param name="path">PDF file path.</param>
        /// <param name="pageNumber">1-based page number.</param>
        /// <returns>Extracted text, Page Width, Page Height, Rendered Image, Total Pages.</returns>
        public (List<ExtractedText> Text, double Width, double Height, BitmapSource Image, int TotalPages) Load(string path, int pageNumber = 1)
        {
            if (!File.Exists(path)) return (new List<ExtractedText>(), 0, 0, null, 0);

            // 1. Get dimensions and text in one go (using PdfPig)
            // Note: ExtractPage might fail if pageNumber is out of range, check internally
            var (text, width, height) = _extractor.ExtractPage(path, pageNumber);

            // 2. Render image (using PdfiumViewer)
            BitmapSource image = null;
            int totalPages = 0;
            try
            {
                using (var doc = PdfDocument.Load(path))
                {
                    totalPages = doc.PageCount;

                    if (pageNumber > 0 && pageNumber <= totalPages)
                    {
                        // Calculate pixels for 96 DPI (Standard screen DPI)
                        // width/height are in Points (1/72 inch).
                        // If PdfPig width is 0 (failed extraction?), fall back to Pdfium page size?
                        // Let's rely on PdfPig for dimensions if successful.

                        if (width == 0 || height == 0)
                        {
                            var s = doc.PageSizes[pageNumber - 1];
                            width = s.Width;
                            height = s.Height;
                        }

                        int w = (int)(width / 72.0 * 96.0);
                        int h = (int)(height / 72.0 * 96.0);

                        // PdfiumViewer uses 0-based index
                        using (var bitmap = doc.Render(pageNumber - 1, w, h, 96, 96, PdfRenderFlags.CorrectFromDpi))
                        {
                            image = ToBitmapSource(bitmap);
                        }
                    }
                }
            }
            catch
            {
                // Fallback if rendering fails
            }

            return (text, width, height, image, totalPages);
        }

        private BitmapSource ToBitmapSource(System.Drawing.Image source)
        {
            using (var stream = new MemoryStream())
            {
                source.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                var result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();
                return result;
            }
        }
    }
}
