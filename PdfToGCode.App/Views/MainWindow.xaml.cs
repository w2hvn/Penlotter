using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
        private double _pageWidth;
        private string _currentPdfPath;
        private int _currentPage = 1;
        private int _totalPages = 0;
        private List<int> _selectedPages = new List<int>();
        private int _currentSelectionIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            _pdfLoader = new PdfLoader();
            _sceneRenderer = new VectorSceneRenderer();
            LoadFont();
            UpdatePageControls();
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

        private void UpdatePageControls()
        {
            btnPrevPage.IsEnabled = _selectedPages.Count > 1 && _currentSelectionIndex > 0;
            btnNextPage.IsEnabled = _selectedPages.Count > 1 && _currentSelectionIndex < _selectedPages.Count - 1;

            if (_totalPages > 0 && _selectedPages.Count > 0)
            {
                int displayedPage = _selectedPages[_currentSelectionIndex];
                txtPageInfo.Text = $"Page {displayedPage} / {_totalPages} (Item {_currentSelectionIndex + 1}/{_selectedPages.Count})";
            }
            else
            {
                txtPageInfo.Text = "Page 0 / 0";
            }
        }

        private void LoadPage(int pageNumber)
        {
            if (string.IsNullOrEmpty(_currentPdfPath) || pageNumber < 1 || pageNumber > _totalPages) return;

            try
            {
                var result = _pdfLoader.Load(_currentPdfPath, pageNumber);

                canvasPdf.Children.Clear();
                canvasPdf.Reset();

                if (result.Image != null)
                {
                    var img = new Image
                    {
                        Source = result.Image,
                        Stretch = System.Windows.Media.Stretch.None
                    };
                    canvasPdf.Children.Add(img);
                }

                _extractedText = result.Text;
                _pageHeight = result.Height;
                _pageWidth = result.Width;
                _currentPage = pageNumber;

                canvasPreview.Children.Clear();
                canvasPreview.Reset();
                _loadedGCode = null;

                UpdatePageControls();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading page {pageNumber}: {ex.Message}");
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
                    _currentSelectionIndex = 0;

                    txtPageRange.Text = $"1-{_totalPages}";

                    LoadPage(1);
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
                    _currentSelectionIndex = 0;
                    LoadPage(_selectedPages[0]);
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

        private void btnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPages.Count > 0 && _currentSelectionIndex > 0)
            {
                _currentSelectionIndex--;
                LoadPage(_selectedPages[_currentSelectionIndex]);
            }
        }

        private void btnNextPage_Click(object sender, RoutedEventArgs e)
        {
             if (_selectedPages.Count > 0 && _currentSelectionIndex < _selectedPages.Count - 1)
            {
                _currentSelectionIndex++;
                LoadPage(_selectedPages[_currentSelectionIndex]);
            }
        }

        private void btnVectorize_Click(object sender, RoutedEventArgs e)
        {
            if (_extractedText == null)
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
                _sceneRenderer.RenderScene(canvasPreview, _extractedText, _fontData, _pageHeight, _pageWidth);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating vectors: {ex.Message}");
            }
        }

        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPdfPath) || _selectedPages.Count == 0)
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
                var settings = new GCodeSettings();
                var generator = new GCodeGenerator();
                var extractor = new PdfTextLayoutExtractor();
                var pageCodes = new List<string>();

                foreach(var pageNum in _selectedPages)
                {
                    var (text, _, _) = extractor.ExtractPage(_currentPdfPath, pageNum);

                    if (text.Count > 0)
                    {
                        var gcode = generator.Generate(text, _fontData, settings);

                        // Strip final M2/M30
                        gcode = gcode.TrimEnd();
                        if (gcode.EndsWith("M2")) gcode = gcode.Substring(0, gcode.Length - 2);
                        else if (gcode.EndsWith("M30")) gcode = gcode.Substring(0, gcode.Length - 3);

                        pageCodes.Add($"(Page {pageNum})\n" + gcode.Trim());
                    }
                }

                if (pageCodes.Count > 0)
                {
                    // Join with M0 (Pause) and add final M2
                    _loadedGCode = string.Join("\nM0\n", pageCodes) + "\nM2";
                    MessageBox.Show($"G-code generated for {_selectedPages.Count} pages!");
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
