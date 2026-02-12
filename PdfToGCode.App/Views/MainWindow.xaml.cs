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
using PdfToGCode.Core.Services;
using PdfToGCode.Core.Utils;
using Path = System.IO.Path;

namespace PdfToGCode.App.Views
{
    public partial class MainWindow : Window
    {
        private PdfLoader _pdfLoader;
        private VectorSceneRenderer _sceneRenderer;
        private List<PageData> _loadedPages = new List<PageData>();

        private FontData _titleFontData;
        private FontData _bodyFontData;
        private FontManager _fontManager;
        private GrblSender _sender;

        private Dictionary<int, string> _generatedGCode = new Dictionary<int, string>();

        private string _currentPdfPath;
        private int _totalPages = 0;
        private List<int> _selectedPages = new List<int>();
        private string _fontsDir;
        private string _settingsPath;

        public MainWindow()
        {
            InitializeComponent();
            _pdfLoader = new PdfLoader();
            _sceneRenderer = new VectorSceneRenderer();
            _fontManager = new FontManager();
            _sender = new GrblSender();
            _sender.OnLog += msg => Dispatcher.Invoke(() => txtSerialStatus.Text = msg);
            _sender.OnProgress += (curr, total) => Dispatcher.Invoke(() => {
                progSend.Maximum = total;
                progSend.Value = curr;
            });
            _sender.OnConnectionChanged += connected => Dispatcher.Invoke(() => {
                btnConnect.Content = connected ? "Disconnect" : "Connect";
                btnConnect.Background = connected ? Brushes.Red : new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                btnSend.IsEnabled = connected;
                cboPorts.IsEnabled = !connected;
                cboBaudRate.IsEnabled = !connected;
            });

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _fontsDir = Path.Combine(baseDir, "Fonts");
            _settingsPath = Path.Combine(baseDir, "settings.json");

            if (!Directory.Exists(_fontsDir)) Directory.CreateDirectory(_fontsDir);

            InitializeFonts();
            LoadSettings();
            RefreshPorts();
            UpdateStatus("Ready");
        }

        private void RefreshPorts()
        {
            cboPorts.ItemsSource = System.IO.Ports.SerialPort.GetPortNames();
            if (cboPorts.Items.Count > 0) cboPorts.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            var settings = GCodeSettings.Load(_settingsPath);
            txtFeedRate.Text = settings.FeedRate.ToString();
            txtTravelSpeed.Text = settings.TravelSpeed.ToString();
            txtZDown.Text = settings.ZDown.ToString();
            txtZSafe.Text = settings.ZUp.ToString();
            chkServo.IsChecked = settings.IsServoMode;

            cboPorts.Text = settings.PortName;
            cboBaudRate.Text = settings.BaudRate.ToString();
        }

        private void SaveSettings()
        {
            if (double.TryParse(txtFeedRate.Text, out double feed) &&
                double.TryParse(txtTravelSpeed.Text, out double travel) &&
                double.TryParse(txtZDown.Text, out double zDown) &&
                double.TryParse(txtZSafe.Text, out double zUp) &&
                int.TryParse(cboBaudRate.Text, out int baud))
            {
                var settings = new GCodeSettings
                {
                    FeedRate = feed,
                    TravelSpeed = travel,
                    ZDown = zDown,
                    ZUp = zUp,
                    IsServoMode = chkServo.IsChecked == true,
                    PortName = cboPorts.Text,
                    BaudRate = baud
                };
                settings.Save(_settingsPath);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_sender.IsConnected) _sender.Disconnect();
            SaveSettings();
        }

        private void btnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_sender.IsConnected)
            {
                _sender.Disconnect();
            }
            else
            {
                if (cboPorts.SelectedItem == null)
                {
                    MessageBox.Show("Please select a COM port.");
                    return;
                }

                if (int.TryParse(cboBaudRate.Text, out int baud))
                {
                    try
                    {
                        btnConnect.IsEnabled = false;
                        UpdateStatus("Connecting...", true);
                        await _sender.ConnectAsync(cboPorts.SelectedItem.ToString(), baud);
                        UpdateStatus("Connected");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Connection failed: {ex.Message}");
                        UpdateStatus("Connection Failed");
                    }
                    finally
                    {
                        btnConnect.IsEnabled = true;
                        SetBusy(false);
                    }
                }
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedGCode.Count == 0)
            {
                MessageBox.Show("Please generate G-code first.");
                return;
            }

            if (!_sender.IsConnected)
            {
                MessageBox.Show("Not connected to machine.");
                return;
            }

            btnSend.IsEnabled = false;
            btnStop.IsEnabled = true;
            btnPause.IsEnabled = true;

            try
            {
                // Iterate through selected pages that have generated gcode
                // Assuming loaded pages are the ones we want to send
                foreach (var page in _loadedPages)
                {
                    if (!_generatedGCode.ContainsKey(page.PageNumber)) continue;

                    // Visualize current page
                    await _sceneRenderer.RenderSceneAsync(canvasVisualizer, new List<PageData> { page }, _titleFontData, _bodyFontData);

                    string gcode = _generatedGCode[page.PageNumber];
                    UpdateStatus($"Sending Page {page.PageNumber}...", true);

                    await _sender.SendGCodeAsync(gcode);

                    // After page complete
                    UpdateStatus($"Page {page.PageNumber} Complete.");

                    // If not last page, pause for paper change
                    if (page != _loadedPages.Last())
                    {
                        var result = MessageBox.Show($"Page {page.PageNumber} finished. Please change paper and click OK to continue.",
                                                     "Next Page", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                        if (result == MessageBoxResult.Cancel)
                        {
                            _sender.Stop();
                            break;
                        }
                    }
                }
                MessageBox.Show("All pages sent successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sending Error: {ex.Message}");
            }
            finally
            {
                btnSend.IsEnabled = true;
                btnStop.IsEnabled = false;
                btnPause.IsEnabled = false;
                UpdateStatus("Ready");
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (btnPause.Content.ToString() == "Pause")
            {
                _sender.Pause();
                btnPause.Content = "Resume";
            }
            else
            {
                _sender.Resume();
                btnPause.Content = "Pause";
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _sender.Stop();
        }

        // Jogging & Setup
        private async void SendCommand(string cmd)
        {
            if (!_sender.IsConnected) return;
            try
            {
                await _sender.SendGCodeAsync(cmd);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Jog Error: {ex.Message}");
            }
        }

        private double GetJogStep()
        {
            if (cboJogStep.SelectedItem is ComboBoxItem item && double.TryParse(item.Content.ToString(), out double step))
                return step;
            return 1.0;
        }

        private void btnJogYPos_Click(object sender, RoutedEventArgs e) => SendCommand($"G91 G0 Y{GetJogStep()}");
        private void btnJogYNeg_Click(object sender, RoutedEventArgs e) => SendCommand($"G91 G0 Y-{GetJogStep()}");
        private void btnJogXPos_Click(object sender, RoutedEventArgs e) => SendCommand($"G91 G0 X{GetJogStep()}");
        private void btnJogXNeg_Click(object sender, RoutedEventArgs e) => SendCommand($"G91 G0 X-{GetJogStep()}");
        private void btnJogZPos_Click(object sender, RoutedEventArgs e) => SendCommand($"G91 G0 Z{GetJogStep()}");
        private void btnJogZNeg_Click(object sender, RoutedEventArgs e) => SendCommand($"G91 G0 Z-{GetJogStep()}");

        private void btnSetHome_Click(object sender, RoutedEventArgs e) => SendCommand("G92 X0 Y0 Z0");

        private void btnSendCmd_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtManualCmd.Text))
            {
                SendCommand(txtManualCmd.Text);
                txtManualCmd.Clear();
            }
        }

        private async void InitializeFonts()
        {
            await Task.Run(() => _fontManager.ScanFonts(_fontsDir));

            cboTitleFont.ItemsSource = _fontManager.AvailableFonts;
            cboBodyFont.ItemsSource = _fontManager.AvailableFonts;

            if (_fontManager.AvailableFonts.Count > 0)
            {
                cboTitleFont.SelectedIndex = 0;
                cboBodyFont.SelectedIndex = 0;
            }
        }

        private async void cboTitleFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboTitleFont.SelectedItem == null) return;
            string fontName = cboTitleFont.SelectedItem.ToString();

            UpdateStatus($"Loading Title Font: {fontName}...", true);
            try
            {
                await Task.Run(() => _titleFontData = _fontManager.LoadFont(fontName, _fontsDir));
                CheckFontsAndVectorize();
            }
            catch (Exception ex) { UpdateStatus($"Font Load Error: {ex.Message}"); }
            finally { SetBusy(false); }
        }

        private async void cboBodyFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboBodyFont.SelectedItem == null) return;
            string fontName = cboBodyFont.SelectedItem.ToString();

            UpdateStatus($"Loading Body Font: {fontName}...", true);
            try
            {
                await Task.Run(() => _bodyFontData = _fontManager.LoadFont(fontName, _fontsDir));
                CheckFontsAndVectorize();
            }
            catch (Exception ex) { UpdateStatus($"Font Load Error: {ex.Message}"); }
            finally { SetBusy(false); }
        }

        private void CheckFontsAndVectorize()
        {
            if (_titleFontData != null && _bodyFontData != null)
            {
                UpdateStatus("Fonts Loaded");
                if (_loadedPages.Count > 0)
                {
                    // Debounce or just trigger? Just trigger.
                    // Must be called on UI thread? Yes, this method is called from SelectionChanged (UI thread).
                    // But loading was async.
                    btnVectorize_Click(this, new RoutedEventArgs());
                }
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

                CheckFontsAndVectorize();
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Error loading pages: {ex.Message}");
            }
        }

        private async void btnVectorize_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedPages.Count == 0) return;

            if (_titleFontData == null || _bodyFontData == null)
            {
                MessageBox.Show("Please select fonts for both Title and Body.");
                return;
            }

            UpdateStatus("Vectorizing...", true);
            try
            {
                await _sceneRenderer.RenderSceneAsync(canvasPreview, _loadedPages, _titleFontData, _bodyFontData);
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

            if (_titleFontData == null || _bodyFontData == null)
            {
                MessageBox.Show("Please select fonts for both Title and Body.");
                return;
            }

            if (!double.TryParse(txtZDown.Text, out double zDown) ||
                !double.TryParse(txtZSafe.Text, out double zSafe) ||
                !double.TryParse(txtFeedRate.Text, out double feedRate) ||
                !double.TryParse(txtTravelSpeed.Text, out double travelSpeed))
            {
                MessageBox.Show("Invalid settings values.");
                return;
            }

            bool isServo = chkServo.IsChecked == true;

            UpdateStatus("Generating G-code...", true);

            try
            {
                _generatedGCode.Clear();
                SaveSettings();

                await Task.Run(() =>
                {
                    var settings = new GCodeSettings
                    {
                        ZDown = zDown,
                        ZUp = zSafe,
                        FeedRate = feedRate,
                        TravelSpeed = travelSpeed,
                        IsServoMode = isServo
                    };

                    var generator = new GCodeGenerator();

                    foreach(var page in _loadedPages)
                    {
                        if (page.Content.TextBlocks.Count > 0 || page.Content.Shapes.Count > 0)
                        {
                            // Update Generator to support dual fonts
                            var gcode = generator.Generate(page.Content, _titleFontData, _bodyFontData, settings);
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
