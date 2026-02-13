using System;
using System.IO;
using System.Text.Json;

namespace PdfToGCode.Core.GCode
{
    public class GCodeSettings
    {
        public double FeedRate { get; set; } = 1000;
        public double TravelSpeed { get; set; } = 3000;
        public double ZUp { get; set; } = 5.0;
        public double ZGap { get; set; } = 1.0; // Low lift for small gaps
        public double ZDown { get; set; } = -1.0;
        public double TextScale { get; set; } = 1.0;
        public bool UseG64 { get; set; } = true;
        public bool IsServoMode { get; set; } = false;

        // Serial Settings
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 115200;

        public static GCodeSettings Load(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<GCodeSettings>(json);
                    if (settings != null) return settings;
                }
                catch
                {
                    // Ignore errors, return default
                }
            }
            return new GCodeSettings();
        }

        public void Save(string path)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
