using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PdfToGCode.Core.Fonts;

namespace PdfToGCode.Core.Utils
{
    public class FontManager
    {
        private readonly Dictionary<string, FontData> _fontCache = new Dictionary<string, FontData>();
        private readonly SvgFontParser _parser = new SvgFontParser();

        public List<string> AvailableFonts { get; private set; } = new List<string>();

        public FontManager()
        {
            // Initialize with default or scan
        }

        public void ScanFonts(string fontsDirectory)
        {
            AvailableFonts.Clear();
            _fontCache.Clear();

            // Add embedded default
            AvailableFonts.Add("CHUINHOA (Embedded)");

            if (Directory.Exists(fontsDirectory))
            {
                var files = Directory.GetFiles(fontsDirectory, "*.svg");
                foreach (var file in files)
                {
                    AvailableFonts.Add(Path.GetFileName(file));
                }
            }
        }

        public FontData LoadFont(string fontName, string fontsDirectory)
        {
            if (_fontCache.ContainsKey(fontName)) return _fontCache[fontName];

            string content = null;

            if (fontName == "CHUINHOA (Embedded)")
            {
                // Load resource
                var assembly = typeof(SvgFontParser).Assembly;
                var resourceName = "PdfToGCode.Core.Fonts.CHUINHOA.svg";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            content = reader.ReadToEnd();
                        }
                    }
                }
            }
            else
            {
                // Load from file
                string path = Path.Combine(fontsDirectory, fontName);
                if (File.Exists(path))
                {
                    content = File.ReadAllText(path);
                }
            }

            if (content != null)
            {
                var data = _parser.Parse(content);
                _fontCache[fontName] = data;
                return data;
            }

            return null;
        }
    }
}
