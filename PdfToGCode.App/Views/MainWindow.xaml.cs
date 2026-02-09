using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfToGCode.App.Pdf;
using PdfToGCode.App.Rendering;
using PdfToGCode.Core.Fonts;
using PdfToGCode.Core.GCode;
using PdfToGCode.Core.Pdf;
using PdfToGCode.Core.Utils;

namespace PdfToGCode.App.Views
{
    public partial class MainWindow : Window
    {
        private PdfLoader _pdfLoader;
        private VectorSceneRenderer _sceneRenderer;
        private List<ExtractedText> _extractedText;
        private FontData _fontData;
        private string _loadedGCode;
        private double _pageHeight;

        public MainWindow()
        {
            InitializeComponent();
            _pdfLoader = new PdfLoader();
            _sceneRenderer = new VectorSceneRenderer();
            LoadFont();
        }

        private void LoadFont()
        {
            try
            {
                var assembly = typeof(SvgFontParser).Assembly;
                // Resource name might vary. Usually AssemblyName.Folder.File
                // Checking default namespace of Core project.
                var resourceName = "PdfToGCode.Core.Fonts.CHUINHOA.svg";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        // Try finding resource
                        var resources = assembly.GetManifestResourceNames();
                        var found = resources.FirstOrDefault(r => r.EndsWith("CHUINHOA.svg"));
                        if (found != null)
                        {
                            using (var s = assembly.GetManifestResourceStream(found))
                            using (var reader = new StreamReader(s))
                            {
                                var content = reader.ReadToEnd();
                                var parser = new SvgFontParser();
                                _fontData = parser.Parse(content);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Could not find font resource CHUINHOA.svg. Please ensure it is embedded in PdfToGCode.Core.");
                        }
                    }
                    else
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var content = reader.ReadToEnd();
                            var parser = new SvgFontParser();
                            _fontData = parser.Parse(content);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading font: {ex.Message}");
            }
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "PDF Files (*.pdf)|*.pdf";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var result = _pdfLoader.Load(dlg.FileName, 1); // Load page 1 for now

                    canvasPdf.Children.Clear();
                    canvasPdf.Reset();

                    if (result.Image != null)
                    {
                        var img = new Image
                        {
                            Source = result.Image,
                            Stretch = System.Windows.Media.Stretch.None
                        };
                        // Add image to ZoomPanCanvas
                        canvasPdf.Children.Add(img);
                        // Optionally center or fit?
                        // For now, it will be at (0,0)
                    }

                    _extractedText = result.Text;
                    if (_extractedText.Count == 0)
                    {
                         MessageBox.Show("Warning: No text extracted from PDF. This might be an image-only PDF.");
                    }

                    _pageHeight = result.Height;

                    // Clear vector preview
                    canvasPreview.Children.Clear();
                    canvasPreview.Reset();
                    _loadedGCode = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading PDF: {ex.Message}");
                }
            }
        }

        private void btnVectorize_Click(object sender, RoutedEventArgs e)
        {
            if (_extractedText == null || _extractedText.Count == 0)
            {
                MessageBox.Show("Please import a PDF with text first.");
                return;
            }

            if (_fontData == null)
            {
                MessageBox.Show("Font data not loaded.");
                return;
            }

            try
            {
                _sceneRenderer.RenderScene(canvasPreview, _extractedText, _fontData, _pageHeight);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating vectors: {ex.Message}");
            }
        }

        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_extractedText == null || _extractedText.Count == 0)
            {
                MessageBox.Show("Please import a PDF with text first.");
                return;
            }

            if (_fontData == null)
            {
                MessageBox.Show("Font data not loaded.");
                return;
            }

            try
            {
                var settings = new GCodeSettings(); // Use default settings for now
                var generator = new GCodeGenerator();
                _loadedGCode = generator.Generate(_extractedText, _fontData, settings);

                MessageBox.Show("G-code generated successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating G-code: {ex.Message}");
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedGCode))
            {
                MessageBox.Show("Please generate G-code first.");
                return;
            }

            var dlg = new SaveFileDialog();
            dlg.Filter = "G-code Files (*.gcode;*.nc)|*.gcode;*.nc";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, _loadedGCode);
                    MessageBox.Show("G-code saved successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}");
                }
            }
        }
    }
}
