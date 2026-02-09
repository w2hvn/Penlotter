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
        /// Loads PDF page, extracts content (text + shapes) and renders image.
        /// </summary>
        public (ExtractedPageContent Content, double Width, double Height, BitmapSource Image, int TotalPages) Load(string path, int pageNumber = 1)
        {
            if (!File.Exists(path)) return (new ExtractedPageContent(), 0, 0, null, 0);

            // 1. Get content (Text + Shapes)
            // Need to update PdfTextLayoutExtractor to expose method returning Content + Dimensions?
            // Actually ExtractPageContent returns just Content.
            // Dimensions are inside or need separate call?
            // ExtractPageContent calls ExtractWordsFromPage etc.
            // But width/height logic is separate in GetPageDimensions.
            // Let's call both or update extractor.

            // Reusing existing flow logic:
            var content = _extractor.ExtractPageContent(path, pageNumber);
            var (width, height) = _extractor.GetPageDimensions(path, pageNumber);

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
                        if (width == 0 || height == 0)
                        {
                            var s = doc.PageSizes[pageNumber - 1];
                            width = s.Width;
                            height = s.Height;
                        }

                        int w = (int)(width / 72.0 * 96.0);
                        int h = (int)(height / 72.0 * 96.0);

                        using (var bitmap = doc.Render(pageNumber - 1, w, h, 96, 96, PdfRenderFlags.CorrectFromDpi))
                        {
                            image = ToBitmapSource(bitmap);
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return (content, width, height, image, totalPages);
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
