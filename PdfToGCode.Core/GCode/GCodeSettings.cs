namespace PdfToGCode.Core.GCode
{
    public class GCodeSettings
    {
        public double FeedRate { get; set; } = 1000;
        public double TravelSpeed { get; set; } = 3000;
        public double ZUp { get; set; } = 5.0;
        public double ZDown { get; set; } = -1.0;
        public bool UseG64 { get; set; } = true;
        public bool IsServoMode { get; set; } = false;
    }
}
