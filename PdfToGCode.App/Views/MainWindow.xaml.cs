using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        private Dictionary<int, string> _generatedGCode = new Dictionary<int, string>();

        private string _currentPdfPath;
        private int _totalPages = 0;
        private List<int> _selectedPages = new List<int>();

        public MainWindow()
        {
            InitializeComponent();
            _pdfLoader = new PdfLoader();
            _sceneRenderer = new VectorSceneRenderer();
            LoadFontAsync();
            UpdateStatus("Ready");
        }

        private async void LoadFontAsync()
        {
            UpdateStatus("Loading Font...", true);
            try
            {
                await Task.Run(() =>
                {
                    var assembly = typeof(SvgFontParser).Assembly;
                    var resourceName = "PdfToGCode.Core.Fonts.CHUINHOA.svg";

                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                var content = reader.ReadToEnd();
                                var parser = new SvgFontParser();
                                _fontData = parser.Parse(content);
                            }
                        }
                    }
                });

                if (_fontData == null)
                    MessageBox.Show("Could not load embedded font resource.");
                else
                    UpdateStatus("Font Loaded");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Font Error: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdateStatus(string message, bool isBusy = false)
        {
            txtStatus.Text = message;
            SetBusy(isBusy);
        }

        private void SetBusy(bool isBusy)
        {
            progressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            // Optionally disable buttons
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

            // Prepare list for background processing if needed, but PdfLoader does image gen which might need UI thread or bitmap freezing
            // PdfLoader.Load returns BitmapSource which must be created on UI thread or frozen.
            // Let's run extraction in background but image creation is tricky with PdfiumViewer rendering to Bitmap (System.Drawing) then conversion.
            // PdfLoader.Load currently does both.
            // We should ideally split or ensure BitmapSource is frozen.
            // PdfLoader.Load freezes the bitmap, so it should be safe to pass across threads.

            try
            {
                // Process in chunks or parallel? Parallel might crash Pdfium. Serial is safer.
                var pages = await Task.Run(() =>
                {
                    var list = new List<(PageData Data, BitmapSource Image)>();
                    double currentX = 50;
                    double margin = 50;

                    foreach (var pageNum in _selectedPages)
                    {
                        var result = _pdfLoader.Load(_currentPdfPath, pageNum);

                        // We can't access UI controls here (Canvas).
                        // We return data to add later.

                        var pageData = new PageData
                        {
                            PageNumber = pageNum,
                            Width = result.Width,
                            Height = result.Height,
                            TextBlocks = result.Text
                        };

                        list.Add((pageData, result.Image));
                    }
                    return list;
                });

                // Update UI
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
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Error loading pages: {ex.Message}");
            }
        }

        private async void btnVectorize_Click(object sender, RoutedEventArgs e)
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

            UpdateStatus("Vectorizing...", true);
            try
            {
                // VectorSceneRenderer logic might be heavy.
                // Refactor rendering to calculate paths in background, then add to canvas.
                // VectorSceneRenderer.RenderScene currently does it all.
                // We should modify it or just wrap it?
                // Wrapping `RenderScene` in Task.Run won't work because it touches UI (Canvas).
                // We need to split logic.
                // For now, let's just await a Task that calculates geometries?
                // Or: Modify VectorSceneRenderer to separate calculation from drawing.

                // Let's assume VectorSceneRenderer is updated to be async-friendly or we do it here.
                // Since I can't modify VectorSceneRenderer in this step (Plan says "Refactor Rendering" is next step),
                // I will placeholder this and rely on next step.
                // But wait, the plan says "Refactor Rendering for Async" is Step 4.
                // So I should implement the Async handler here but call the (to be updated) renderer.

                // Assuming RenderSceneAsync signature:
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
                MessageBox.Show("Font data not loaded.");
                return;
            }

            UpdateStatus("Generating G-code...", true);

            try
            {
                _generatedGCode.Clear();

                await Task.Run(() =>
                {
                    var settings = new GCodeSettings();
                    var generator = new GCodeGenerator();

                    foreach(var page in _loadedPages)
                    {
                        if (page.TextBlocks.Count > 0)
                        {
                            var gcode = generator.Generate(page.TextBlocks, _fontData, settings);
                            // Locking needed? No, purely local loop or dictionary access.
                            // But Dictionary is not thread safe if parallel.
                            // This is sequential loop in Task, so safe.
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
                     MessageBox.Show("No text found on selected pages.");
                     UpdateStatus("No text extracted");
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
