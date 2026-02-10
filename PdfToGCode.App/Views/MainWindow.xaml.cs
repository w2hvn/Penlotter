using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private FontManager _fontManager;
        private Dictionary<int, string> _generatedGCode = new Dictionary<int, string>();

        private string _currentPdfPath;
        private int _totalPages = 0;
        private List<int> _selectedPages = new List<int>();
        private string _fontsDir;

        public MainWindow()
        {
            InitializeComponent();
            _pdfLoader = new PdfLoader();
            _sceneRenderer = new VectorSceneRenderer();
            _fontManager = new FontManager();

            _fontsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
            if (!Directory.Exists(_fontsDir)) Directory.CreateDirectory(_fontsDir);

            InitializeFonts();
            UpdateStatus("Ready");
        }

        private async void InitializeFonts()
        {
            await Task.Run(() => _fontManager.ScanFonts(_fontsDir));

            cboFonts.ItemsSource = _fontManager.AvailableFonts;
            if (_fontManager.AvailableFonts.Count > 0)
            {
                cboFonts.SelectedIndex = 0;
            }
        }

        private async void cboFonts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboFonts.SelectedItem == null) return;
            string fontName = cboFonts.SelectedItem.ToString();

            UpdateStatus($"Loading Font: {fontName}...", true);
            try
            {
                await Task.Run(() =>
                {
                    _fontData = _fontManager.LoadFont(fontName, _fontsDir);
                });

                if (_fontData != null)
                {
                    UpdateStatus("Font Loaded");
                    if (_loadedPages.Count > 0)
                    {
                        btnVectorize_Click(this, new RoutedEventArgs());
                    }
                }
                else
                {
                    UpdateStatus("Error loading font");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Font Load Error: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void chkServo_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Placeholder logic if needed (e.g., change label text)
            // But we read the value on Generate click.
        }

        private void UpdateStatus(string message, bool isBusy = false)
        {
            txtStatus.Text = message;
            SetBusy(isBusy);
        }

        private void SetBusy(bool isBusy)
        {
            progressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            this.IsEnabled = !isBusy;
        }

        private async void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "PDF Files (*.pdf)|*.pdf";
            if (dlg.ShowDialog() == true)
            {
                _currentPdfPath = dlg.FileName;
                UpdateStatus("Reading PDF info...", true);

                try
                {
                    await Task.Run(() =>
                    {
                        var result = _pdfLoader.Load(_currentPdfPath, 1);
                        _totalPages = result.TotalPages;
                    });

                    _selectedPages.Clear();
                    for(int i=1; i<=_totalPages; i++) _selectedPages.Add(i);

                    txtPageRange.Text = $"1-{_totalPages}";

                    await LoadSelectedPagesAsync();
                    UpdateStatus($"Loaded {_currentPdfPath} ({_totalPages} pages)");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Import Error: {ex.Message}");
                    MessageBox.Show($"Error opening PDF: {ex.Message}");
                }
                finally
                {
                    SetBusy(false);
                }
            }
        }

        private async void btnSetRange_Click(object sender, RoutedEventArgs e)
        {
            if (_totalPages == 0) return;

            string rangeText = txtPageRange.Text;
            UpdateStatus("Parsing range...", true);

            try
            {
                var parsed = await Task.Run(() => PageRangeParser.Parse(rangeText, _totalPages));

                if (parsed.Count > 0)
                {
                    _selectedPages = parsed;
                    await LoadSelectedPagesAsync();
                    UpdateStatus($"Selected {_selectedPages.Count} pages");
                }
                else
                {
                    MessageBox.Show("Invalid page range or no pages in range.");
                    UpdateStatus("Invalid range");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Range Error: {ex.Message}");
                MessageBox.Show($"Error parsing range: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task LoadSelectedPagesAsync()
        {
            if (string.IsNullOrEmpty(_currentPdfPath) || _selectedPages.Count == 0) return;

            UpdateStatus("Loading Pages...", true);
            canvasPdf.Children.Clear();
            canvasPdf.Reset();
            _loadedPages.Clear();
            _generatedGCode.Clear();

            try
            {
                var pages = await Task.Run(() =>
                {
                    var list = new List<(PageData Data, BitmapSource Image)>();
                    foreach (var pageNum in _selectedPages)
                    {
                        var result = _pdfLoader.Load(_currentPdfPath, pageNum);

                        var pageData = new PageData
                        {
                            PageNumber = pageNum,
                            Width = result.Width,
                            Height = result.Height,
                            Content = result.Content
                        };

                        list.Add((pageData, result.Image));
                    }
                    return list;
                });

                double currentX = 50;
                double margin = 50;

                foreach (var item in pages)
                {
                    if (item.Image != null)
                    {
                        var border = new Border
                        {
                            BorderBrush = Brushes.Black,
                            BorderThickness = new Thickness(1),
                            Child = new Image
                            {
                                Source = item.Image,
                                Stretch = Stretch.None
                            }
                        };

                        Canvas.SetLeft(border, currentX);
                        Canvas.SetTop(border, 0);
                        canvasPdf.Children.Add(border);

                        var label = new TextBlock
                        {
                            Text = $"Page {item.Data.PageNumber}",
                            Foreground = Brushes.Black,
                            FontSize = 14,
                            FontWeight = FontWeights.Bold
                        };
                        Canvas.SetLeft(label, currentX);
                        Canvas.SetTop(label, -25);
                        canvasPdf.Children.Add(label);

                        currentX += item.Image.Width + margin;
                    }
                    else
                    {
                        currentX += 500 + margin;
                    }

                    _loadedPages.Add(item.Data);
                }

                canvasPreview.Children.Clear();
                canvasPreview.Reset();

                if (_fontData != null)
                {
                    btnVectorize_Click(this, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Error loading pages: {ex.Message}");
            }
        }

        private async void btnVectorize_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedPages.Count == 0) return;

            if (_fontData == null)
            {
                MessageBox.Show("Please select a font.");
                return;
            }

            UpdateStatus("Vectorizing...", true);
            try
            {
                await _sceneRenderer.RenderSceneAsync(canvasPreview, _loadedPages, _fontData);
                UpdateStatus("Vector Preview Ready");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Vectorize Error: {ex.Message}");
                MessageBox.Show($"Error generating vectors: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedPages.Count == 0)
            {
                MessageBox.Show("Please load pages first.");
                return;
            }

            if (_fontData == null)
            {
                MessageBox.Show("Please select a font.");
                return;
            }

            if (!double.TryParse(txtZDown.Text, out double zDown))
            {
                MessageBox.Show("Invalid Z Down value.");
                return;
            }

            if (!double.TryParse(txtZSafe.Text, out double zSafe))
            {
                MessageBox.Show("Invalid Z Safe value.");
                return;
            }

            bool isServo = chkServo.IsChecked == true;

            UpdateStatus("Generating G-code...", true);

            try
            {
                _generatedGCode.Clear();

                await Task.Run(() =>
                {
                    var settings = new GCodeSettings
                    {
                        ZDown = zDown,
                        ZUp = zSafe,
                        IsServoMode = isServo
                    };

                    var generator = new GCodeGenerator();

                    foreach(var page in _loadedPages)
                    {
                        if (page.Content.TextBlocks.Count > 0 || page.Content.Shapes.Count > 0)
                        {
                            var gcode = generator.Generate(page.Content, _fontData, settings);
                            _generatedGCode[page.PageNumber] = gcode;
                        }
                    }
                });

                if (_generatedGCode.Count > 0)
                {
                    MessageBox.Show($"G-code generated for {_generatedGCode.Count} pages!");
                    UpdateStatus($"G-code Generated ({_generatedGCode.Count} files)");
                }
                else
                {
                     MessageBox.Show("No text or shapes found on selected pages.");
                     UpdateStatus("No content extracted");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Generate Error: {ex.Message}");
                MessageBox.Show($"Error generating G-code: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
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
                UpdateStatus("Saving files...", true);
                try
                {
                    string basePath = dlg.FileName;
                    int savedCount = await Task.Run(() =>
                    {
                        string dir = Path.GetDirectoryName(basePath);
                        string name = Path.GetFileNameWithoutExtension(basePath);
                        string ext = Path.GetExtension(basePath);
                        if (string.IsNullOrEmpty(ext)) ext = ".gcode";

                        int count = 0;
                        foreach (var kvp in _generatedGCode)
                        {
                            int pageNum = kvp.Key;
                            string content = kvp.Value;
                            string finalPath = Path.Combine(dir, $"{name}_{pageNum}{ext}");
                            File.WriteAllText(finalPath, content);
                            count++;
                        }
                        return count;
                    });

                    MessageBox.Show($"Saved {savedCount} files successfully.");
                    UpdateStatus("Files Saved");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Save Error: {ex.Message}");
                    MessageBox.Show($"Error saving files: {ex.Message}");
                }
                finally
                {
                    SetBusy(false);
                }
            }
        }
    }
}
