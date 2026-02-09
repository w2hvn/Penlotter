using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using PdfToGCode.App.Pdf;
using PdfToGCode.App.Rendering;
using PdfToGCode.Core.Fonts;
using PdfToGCode.Core.GCode;
using PdfToGCode.Core.Pdf;
using PdfToGCode.Core.Utils;
using Path = System.IO.Path;

namespace PdfToGCode.App.Views
{
    public partial class MainWindow : Window
    {
        private PdfLoader _pdfLoader;
        private VectorSceneRenderer _sceneRenderer;
        private List<PageData> _loadedPages = new List<PageData>();
        private FontData _fontData;
        private Dictionary<int, string> _generatedGCode = new Dictionary<int, string>();

        private string _currentPdfPath;
        private int _totalPages = 0;
        private List<int> _selectedPages = new List<int>();

        public MainWindow()
        {
            InitializeComponent();
            _pdfLoader = new PdfLoader();
            _sceneRenderer = new VectorSceneRenderer();
            LoadFont();
            txtPageInfo.Text = "No PDF loaded";

            // Hide navigation buttons as we now show all selected pages
            btnPrevPage.Visibility = Visibility.Collapsed;
            btnNextPage.Visibility = Visibility.Collapsed;
        }

        private void LoadFont()
        {
            try
            {
                var assembly = typeof(SvgFontParser).Assembly;
                var resourceName = "PdfToGCode.Core.Fonts.CHUINHOA.svg";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
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
                            MessageBox.Show("Could not find font resource CHUINHOA.svg.");
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

        private void LoadSelectedPages()
        {
            if (string.IsNullOrEmpty(_currentPdfPath) || _selectedPages.Count == 0) return;

            try
            {
                canvasPdf.Children.Clear();
                canvasPdf.Reset();

                _loadedPages.Clear();
                _generatedGCode.Clear();

                double currentX = 50;
                double margin = 50;

                // Load all selected pages and display side-by-side
                foreach (var pageNum in _selectedPages)
                {
                    var result = _pdfLoader.Load(_currentPdfPath, pageNum);

                    if (result.Image != null)
                    {
                        var border = new Border
                        {
                            BorderBrush = Brushes.Black,
                            BorderThickness = new Thickness(1),
                            Child = new Image
                            {
                                Source = result.Image,
                                Stretch = Stretch.None
                            }
                        };

                        Canvas.SetLeft(border, currentX);
                        Canvas.SetTop(border, 0);
                        canvasPdf.Children.Add(border);

                        var label = new TextBlock
                        {
                            Text = $"Page {pageNum}",
                            Foreground = Brushes.Black,
                            FontSize = 14,
                            FontWeight = FontWeights.Bold
                        };
                        Canvas.SetLeft(label, currentX);
                        Canvas.SetTop(label, -25);
                        canvasPdf.Children.Add(label);

                        currentX += result.Image.Width + margin;
                    }
                    else
                    {
                        currentX += 500 + margin; // Fallback
                    }

                    _loadedPages.Add(new PageData
                    {
                        PageNumber = pageNum,
                        Width = result.Width,
                        Height = result.Height,
                        TextBlocks = result.Text
                    });
                }

                canvasPreview.Children.Clear();
                canvasPreview.Reset();

                txtPageInfo.Text = $"Showing {_selectedPages.Count} pages";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading pages: {ex.Message}");
            }
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "PDF Files (*.pdf)|*.pdf";
            if (dlg.ShowDialog() == true)
            {
                _currentPdfPath = dlg.FileName;

                try
                {
                    var result = _pdfLoader.Load(_currentPdfPath, 1);
                    _totalPages = result.TotalPages;

                    _selectedPages.Clear();
                    for(int i=1; i<=_totalPages; i++) _selectedPages.Add(i);

                    txtPageRange.Text = $"1-{_totalPages}";

                    LoadSelectedPages();
                }
                catch (Exception ex)
                {
                     MessageBox.Show($"Error opening PDF: {ex.Message}");
                }
            }
        }

        private void btnSetRange_Click(object sender, RoutedEventArgs e)
        {
            if (_totalPages == 0) return;

            string rangeText = txtPageRange.Text;
            try
            {
                var parsed = PageRangeParser.Parse(rangeText, _totalPages);
                if (parsed.Count > 0)
                {
                    _selectedPages = parsed;
                    LoadSelectedPages();
                }
                else
                {
                    MessageBox.Show("Invalid page range or no pages in range.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing range: {ex.Message}");
            }
        }

        private void btnPrevPage_Click(object sender, RoutedEventArgs e) { }
        private void btnNextPage_Click(object sender, RoutedEventArgs e) { }

        private void btnVectorize_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedPages.Count == 0)
            {
                MessageBox.Show("Please import a PDF first.");
                return;
            }

            if (_fontData == null)
            {
                MessageBox.Show("Font data not loaded.");
                return;
            }

            try
            {
                _sceneRenderer.RenderScene(canvasPreview, _loadedPages, _fontData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating vectors: {ex.Message}");
            }
        }

        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedPages.Count == 0)
            {
                MessageBox.Show("Please load pages first.");
                return;
            }

            if (_fontData == null)
            {
                MessageBox.Show("Font data not loaded.");
                return;
            }

            try
            {
                var settings = new GCodeSettings();
                var generator = new GCodeGenerator();

                _generatedGCode.Clear();

                foreach(var page in _loadedPages)
                {
                    if (page.TextBlocks.Count > 0)
                    {
                        var gcode = generator.Generate(page.TextBlocks, _fontData, settings);
                        _generatedGCode[page.PageNumber] = gcode;
                    }
                }

                if (_generatedGCode.Count > 0)
                {
                    MessageBox.Show($"G-code generated for {_generatedGCode.Count} pages!");
                }
                else
                {
                     MessageBox.Show("No text found on selected pages.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating G-code: {ex.Message}");
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedGCode.Count == 0)
            {
                MessageBox.Show("Please generate G-code first.");
                return;
            }

            var dlg = new SaveFileDialog();
            dlg.Filter = "G-code Files (*.gcode;*.nc)|*.gcode;*.nc";
            if (!string.IsNullOrEmpty(_currentPdfPath))
            {
                dlg.FileName = Path.GetFileNameWithoutExtension(_currentPdfPath);
            }

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string basePath = dlg.FileName;
                    string dir = Path.GetDirectoryName(basePath);
                    string name = Path.GetFileNameWithoutExtension(basePath);
                    string ext = Path.GetExtension(basePath);
                    if (string.IsNullOrEmpty(ext)) ext = ".gcode";

                    int savedCount = 0;
                    foreach (var kvp in _generatedGCode)
                    {
                        int pageNum = kvp.Key;
                        string content = kvp.Value;
                        string finalPath = Path.Combine(dir, $"{name}_{pageNum}{ext}");
                        File.WriteAllText(finalPath, content);
                        savedCount++;
                    }

                    MessageBox.Show($"Saved {savedCount} files successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving files: {ex.Message}");
                }
            }
        }
    }
}
