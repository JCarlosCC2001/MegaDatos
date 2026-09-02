namespace MegaDatos.Models;

public class AppState
{
    public string? LastDirectory { get; set; }
    public string? LastSelectedFile { get; set; }
    public string ActiveTool { get; set; } = "Explorer";
    public string SelectedViewType { get; set; } = "Por defecto";
    public string SelectedGpsFormat { get; set; } = "Decimal";

    // Format tool state
    public string TargetFormat { get; set; } = "JPG";
    public int ConversionQuality { get; set; } = 90;

    // Resize tool state
    public string TargetWidth { get; set; } = "1920";
    public string TargetHeight { get; set; } = "1080";
    public bool KeepAspectRatio { get; set; } = true;

    // Clean tool state
    public bool CleanAllMetadata { get; set; } = true;
    public bool CleanGps { get; set; } = true;
    public bool CleanExif { get; set; } = true;
    public bool CleanAuthorship { get; set; } = true;
    public bool CleanIptc { get; set; } = true;
    public bool BatchBackupEnabled { get; set; } = true;

    // Template tool state
    public string SelectedTemplateId { get; set; } = "Photoshop";

    // Phone tool state
    public string SelectedPhoneDeviceId { get; set; } = "iphone15pro";
    public bool PhoneOverwriteAll { get; set; } = false;
    public bool PhonePreserveGps { get; set; } = true;

    // GPS Generator tool state
    public string GpsGenCenterLat { get; set; } = "-12.065130";
    public string GpsGenCenterLon { get; set; } = "-75.204860";
    public string GpsGenRadius { get; set; } = "500";
    public string GpsGenRadiusUnit { get; set; } = "Metros (m)";
    public bool GpsGenUniquePerPhoto { get; set; } = true;
    public string GpsGenBaseAltitude { get; set; } = "3250";

    // Window geometry & state
    public string WindowState { get; set; } = "Maximized";
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public int? WindowPositionX { get; set; }
    public int? WindowPositionY { get; set; }
}
