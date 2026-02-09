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
        private PdfTextLayoutExtractor _extractor = new PdfTextLayoutExtractor();

        public (List<ExtractedText> Text, double Width, double Height, BitmapSource Image) Load(string path, int pageNumber = 1)
        {
            if (!File.Exists(path)) return (new List<ExtractedText>(), 0, 0, null);

            var (width, height) = _extractor.GetPageDimensions(path, pageNumber);

            // Extract all words and filter
            var allText = _extractor.ExtractWords(path);
            var filteredText = new List<ExtractedText>();
            foreach(var t in allText)
            {
                if(t.PageNumber == pageNumber)
                    filteredText.Add(t);
            }

            BitmapSource image = null;
            try
            {
                // PdfiumViewer uses 0-based index
                using (var doc = PdfDocument.Load(path))
                {
                    // Calculate pixels for 96 DPI
                    int w = (int)(width / 72.0 * 96.0);
                    int h = (int)(height / 72.0 * 96.0);

                    using (var bitmap = doc.Render(pageNumber - 1, w, h, 96, 96, PdfRenderFlags.CorrectFromDpi))
                    {
                        image = ToBitmapSource(bitmap);
                    }
                }
            }
            catch
            {
                // Ignore rendering errors (e.g. if native lib missing)
            }

            return (filteredText, width, height, image);
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
