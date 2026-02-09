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
        /// <returns>Extracted text, Page Width, Page Height, Rendered Image.</returns>
        public (List<ExtractedText> Text, double Width, double Height, BitmapSource Image) Load(string path, int pageNumber = 1)
        {
            if (!File.Exists(path)) return (new List<ExtractedText>(), 0, 0, null);

            // 1. Get dimensions and text in one go (using PdfPig)
            var (text, width, height) = _extractor.ExtractPage(path, pageNumber);

            // 2. Render image (using PdfiumViewer)
            BitmapSource image = null;
            try
            {
                using (var doc = PdfDocument.Load(path))
                {
                    // Calculate pixels for 96 DPI (Standard screen DPI)
                    // width/height are in Points (1/72 inch).
                    int w = (int)(width / 72.0 * 96.0);
                    int h = (int)(height / 72.0 * 96.0);

                    // PdfiumViewer uses 0-based index
                    using (var bitmap = doc.Render(pageNumber - 1, w, h, 96, 96, PdfRenderFlags.CorrectFromDpi))
                    {
                        image = ToBitmapSource(bitmap);
                    }
                }
            }
            catch
            {
                // Fallback if rendering fails
            }

            return (text, width, height, image);
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
