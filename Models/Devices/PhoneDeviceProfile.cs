using System;
using System.IO;
using System.Text.RegularExpressions;
using ImageMagick;

namespace MegaDatos.Models.Devices;

public class PhoneDeviceProfile
{
    public string Id { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ExifMake { get; set; } = string.Empty;
    public string ExifModel { get; set; } = string.Empty;
    public string Software { get; set; } = string.Empty;
    public string LensMake { get; set; } = string.Empty;
    public string LensModel { get; set; } = string.Empty;
    public double FNumber { get; set; } = 1.8;
    public double FocalLength { get; set; } = 5.4;
    public ushort FocalLength35mm { get; set; } = 24;
    public ushort DefaultIso { get; set; } = 50;
    public double DefaultExposureTime { get; set; } = 0.00833; // 1/120s
    public string Description { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "📱";

    public string DisplayName => $"{IconGlyph} {Brand} {ModelName}";

    public string OpticalSummary => $"f/{FNumber:F2} • {FocalLength:F1}mm ({FocalLength35mm}mm eq.) • ISO {DefaultIso}";

    public void ApplyToImage(MagickImage image, FileItem file, bool overwriteAll, bool preserveGps)
    {
        var exif = image.GetExifProfile() ?? new ExifProfile();

        // 1. Make & Model
        if (overwriteAll || exif.GetValue(ExifTag.Make) == null)
        {
            exif.SetValue(ExifTag.Make, ExifMake);
        }
        if (overwriteAll || exif.GetValue(ExifTag.Model) == null)
        {
            exif.SetValue(ExifTag.Model, ExifModel);
        }

        // 2. Software
        if (overwriteAll || exif.GetValue(ExifTag.Software) == null)
        {
            if (!string.IsNullOrEmpty(Software))
            {
                exif.SetValue(ExifTag.Software, Software);
            }
        }

        // 3. Lens Information
        if (overwriteAll || exif.GetValue(ExifTag.LensModel) == null)
        {
            if (!string.IsNullOrEmpty(LensModel))
            {
                exif.SetValue(ExifTag.LensModel, LensModel);
            }
        }
        if (overwriteAll || exif.GetValue(ExifTag.LensMake) == null)
        {
            if (!string.IsNullOrEmpty(LensMake))
            {
                exif.SetValue(ExifTag.LensMake, LensMake);
            }
        }

        // 4. Optical parameters (FNumber, FocalLength, FocalLengthIn35mmFilm)
        if (overwriteAll || exif.GetValue(ExifTag.FNumber) == null)
        {
            uint fNumNumerator = (uint)Math.Round(FNumber * 100);
            exif.SetValue(ExifTag.FNumber, new Rational(fNumNumerator, 100));
        }
        if (overwriteAll || exif.GetValue(ExifTag.FocalLength) == null)
        {
            uint fLenNumerator = (uint)Math.Round(FocalLength * 100);
            exif.SetValue(ExifTag.FocalLength, new Rational(fLenNumerator, 100));
        }
        if (overwriteAll || exif.GetValue(ExifTag.FocalLengthIn35mmFilm) == null)
        {
            exif.SetValue(ExifTag.FocalLengthIn35mmFilm, FocalLength35mm);
        }

        // 5. Exposure & ISO
        if (overwriteAll || exif.GetValue(ExifTag.ISOSpeedRatings) == null)
        {
            exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { DefaultIso });
        }
        if (overwriteAll || exif.GetValue(ExifTag.ExposureTime) == null)
        {
            exif.SetValue(ExifTag.ExposureTime, new Rational(1, 120));
        }
        if (overwriteAll || exif.GetValue(ExifTag.ExposureProgram) == null)
        {
            exif.SetValue(ExifTag.ExposureProgram, (ushort)2); // Normal program
        }
        if (overwriteAll || exif.GetValue(ExifTag.MeteringMode) == null)
        {
            exif.SetValue(ExifTag.MeteringMode, (ushort)5); // Pattern / Multi-segment
        }
        if (overwriteAll || exif.GetValue(ExifTag.Flash) == null)
        {
            exif.SetValue(ExifTag.Flash, (ushort)16); // Flash did not fire, auto mode
        }
        if (overwriteAll || exif.GetValue(ExifTag.WhiteBalance) == null)
        {
            exif.SetValue(ExifTag.WhiteBalance, (ushort)0); // Auto white balance
        }
        if (overwriteAll || exif.GetValue(ExifTag.ColorSpace) == null)
        {
            exif.SetValue(ExifTag.ColorSpace, (ushort)1); // sRGB
        }
        if (overwriteAll || exif.GetValue(ExifTag.SensingMethod) == null)
        {
            exif.SetValue(ExifTag.SensingMethod, (ushort)2); // One-chip color area sensor
        }
        if (overwriteAll || exif.GetValue(ExifTag.SceneCaptureType) == null)
        {
            exif.SetValue(ExifTag.SceneCaptureType, (ushort)0); // Standard
        }
        if (overwriteAll || exif.GetValue(ExifTag.PixelXDimension) == null)
        {
            exif.SetValue(ExifTag.PixelXDimension, new Number((uint)image.Width));
            exif.SetValue(ExifTag.PixelYDimension, new Number((uint)image.Height));
        }

        // 6. Timestamps synchronization
        if (exif.GetValue(ExifTag.DateTimeOriginal) == null || overwriteAll)
        {
            DateTime ts = ExtractDate(file);
            string dtStr = ts.ToString("yyyy:MM:dd HH:mm:ss");
            exif.SetValue(ExifTag.DateTimeOriginal, dtStr);
            exif.SetValue(ExifTag.DateTimeDigitized, dtStr);
            exif.SetValue(ExifTag.DateTime, dtStr);
        }

        // 7. GPS handling
        if (!preserveGps)
        {
            exif.RemoveValue(ExifTag.GPSLatitude);
            exif.RemoveValue(ExifTag.GPSLatitudeRef);
            exif.RemoveValue(ExifTag.GPSLongitude);
            exif.RemoveValue(ExifTag.GPSLongitudeRef);
            exif.RemoveValue(ExifTag.GPSAltitude);
            exif.RemoveValue(ExifTag.GPSAltitudeRef);
            exif.RemoveValue(ExifTag.GPSVersionID);
        }

        image.SetProfile(exif);
    }

    private static DateTime ExtractDate(FileItem file)
    {
        var match = Regex.Match(file.Name, @"(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int y) &&
                int.TryParse(match.Groups[2].Value, out int m) &&
                int.TryParse(match.Groups[3].Value, out int d) &&
                int.TryParse(match.Groups[4].Value, out int h) &&
                int.TryParse(match.Groups[5].Value, out int min) &&
                int.TryParse(match.Groups[6].Value, out int s))
            {
                try { return new DateTime(y, m, d, h, min, s); } catch { }
            }
        }
        if (File.Exists(file.FullPath))
        {
            return File.GetCreationTime(file.FullPath);
        }
        return file.DateModified;
    }
}
