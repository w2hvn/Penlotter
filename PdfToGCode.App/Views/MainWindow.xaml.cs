using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PdfToGCode.App.Pdf;
using PdfToGCode.App.Rendering;
using PdfToGCode.Core.Fonts;
using PdfToGCode.Core.GCode;
using PdfToGCode.Core.Pdf;

namespace PdfToGCode.App.Views
{
    public partial class MainWindow : Window
    {
        private PdfLoader _pdfLoader;
        private SvgFontParser _fontParser;
        private VectorSceneRenderer _vectorRenderer;
        private GCodeGenerator _gcodeGenerator;

        private List<ExtractedText> _extractedText;
        private FontData _fontData;
        private double _pageWidth;
        private double _pageHeight;

        public MainWindow()
        {
            InitializeComponent();
            _pdfLoader = new PdfLoader();
            _fontParser = new SvgFontParser();
            _vectorRenderer = new VectorSceneRenderer(VectorCanvas);
            _gcodeGenerator = new GCodeGenerator();

            // Try to load default font
            if (File.Exists("CHUINHOA.svg"))
            {
                 LoadFont("CHUINHOA.svg");
            }
            else if (File.Exists("../PdfToGCode.Core/Fonts/CHUINHOA.svg"))
            {
                 LoadFont("../PdfToGCode.Core/Fonts/CHUINHOA.svg");
            }
        }

        private void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "PDF Files|*.pdf" };
            if (dialog.ShowDialog() == true)
            {
                LoadPdf(dialog.FileName);
            }
        }

        private void LoadPdf(string path)
        {
            try
            {
                var result = _pdfLoader.Load(path);
                if (result.Text == null)
                {
                    MessageBox.Show("Could not load PDF text.");
                    return;
                }

                _extractedText = result.Text;
                _pageWidth = result.Width;
                _pageHeight = result.Height;

                PdfPreviewImage.Source = result.Image;

                // Set canvas size (scaled to 96 DPI)
                double dpiScale = 96.0 / 72.0;
                VectorCanvas.Width = _pageWidth * dpiScale;
                VectorCanvas.Height = _pageHeight * dpiScale;

                // Pass PageHeight in Points (as used in Transform logic)
                _vectorRenderer.SetPageHeight(_pageHeight);

                RenderVectors();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading PDF: {ex.Message}");
            }
        }

        private void BtnLoadFont_Click(object sender, RoutedEventArgs e)
        {
             var dialog = new OpenFileDialog { Filter = "SVG Files|*.svg" };
             if (dialog.ShowDialog() == true)
             {
                 LoadFont(dialog.FileName);
             }
        }

        private void LoadFont(string path)
        {
            try
            {
                var content = File.ReadAllText(path);
                _fontData = _fontParser.Parse(content);
                RenderVectors();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading font: {ex.Message}");
            }
        }

        private void RenderVectors()
        {
            if (_extractedText != null && _fontData != null)
            {
                _vectorRenderer.Render(_extractedText, _fontData);
            }
        }

        private void BtnGenerateGCode_Click(object sender, RoutedEventArgs e)
        {
            if (_extractedText == null || _fontData == null)
            {
                MessageBox.Show("Please load PDF and Font first.");
                return;
            }

            var dialog = new SaveFileDialog { Filter = "G-Code Files|*.nc;*.gcode" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var settings = new GCodeSettings(); // Use defaults
                    var gcode = _gcodeGenerator.Generate(_extractedText, _fontData, settings);
                    File.WriteAllText(dialog.FileName, gcode);
                    MessageBox.Show("G-Code saved successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating G-Code: {ex.Message}");
                }
            }
        }
    }
}
