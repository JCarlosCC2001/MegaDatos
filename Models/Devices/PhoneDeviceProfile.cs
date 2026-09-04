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
    public string ExifByteOrder { get; set; } = "II";

    public string DisplayName => $"{IconGlyph} {Brand} {ModelName}";

    public string OpticalSummary => $"f/{FNumber:F2} • {FocalLength:F1}mm ({FocalLength35mm}mm eq.) • ISO {DefaultIso}";

    public void ApplyToImage(MagickImage image, FileItem file, bool overwriteAll, bool preserveGps)
    {
        // ... (Mantenemos el código anterior por retrocompatibilidad o lo dejamos intacto, aunque ya no se usará desde MainViewModel para Phone)
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

        // 4. Optical parameters
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
            exif.SetValue(ExifTag.ExposureProgram, (ushort)2);
        }
        if (overwriteAll || exif.GetValue(ExifTag.MeteringMode) == null)
        {
            exif.SetValue(ExifTag.MeteringMode, (ushort)5);
        }
        if (overwriteAll || exif.GetValue(ExifTag.Flash) == null)
        {
            exif.SetValue(ExifTag.Flash, (ushort)16);
        }
        if (overwriteAll || exif.GetValue(ExifTag.WhiteBalance) == null)
        {
            exif.SetValue(ExifTag.WhiteBalance, (ushort)0);
        }
        if (overwriteAll || exif.GetValue(ExifTag.ColorSpace) == null)
        {
            exif.SetValue(ExifTag.ColorSpace, (ushort)1);
        }
        if (overwriteAll || exif.GetValue(ExifTag.SensingMethod) == null)
        {
            exif.SetValue(ExifTag.SensingMethod, (ushort)2);
        }
        if (overwriteAll || exif.GetValue(ExifTag.SceneCaptureType) == null)
        {
            exif.SetValue(ExifTag.SceneCaptureType, (ushort)0);
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

    public async System.Threading.Tasks.Task ApplyWithExifToolAsync(string imagePath, FileItem file, bool overwriteAll, bool preserveGps)
    {
        string exiftoolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "exiftool.exe");
        if (!File.Exists(exiftoolPath))
        {
            exiftoolPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "exiftool.exe");
        }

        // Soporte si el usuario olvidó renombrar el ejecutable descargado
        if (!File.Exists(exiftoolPath))
        {
            exiftoolPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "exiftool(-k).exe");
        }
        if (!File.Exists(exiftoolPath))
        {
            exiftoolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "exiftool(-k).exe");
        }

        if (!File.Exists(exiftoolPath))
        {
            throw new FileNotFoundException("No se encontró exiftool.exe en la carpeta Assets.", exiftoolPath);
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exiftoolPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        psi.ArgumentList.Add("-overwrite_original");
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("-P");
        
        // Siempre forzamos la reescritura total del contenedor EXIF con el ByteOrder especificado
        // para garantizar la coherencia forense del dispositivo.
        psi.ArgumentList.Add("-all=");

        Action addPhoneTags = () =>
        {
            psi.ArgumentList.Add($"-EXIF:Make={ExifMake}");
            psi.ArgumentList.Add($"-EXIF:Model={ExifModel}");
            if (!string.IsNullOrEmpty(Software)) psi.ArgumentList.Add($"-EXIF:Software={Software}");
            if (!string.IsNullOrEmpty(LensMake)) psi.ArgumentList.Add($"-EXIF:LensMake={LensMake}");
            if (!string.IsNullOrEmpty(LensModel)) psi.ArgumentList.Add($"-EXIF:LensModel={LensModel}");

            psi.ArgumentList.Add($"-EXIF:FNumber={FNumber}");
            psi.ArgumentList.Add($"-EXIF:FocalLength={FocalLength}");
            psi.ArgumentList.Add($"-EXIF:FocalLengthIn35mmFormat={FocalLength35mm}");
            psi.ArgumentList.Add($"-EXIF:ISO={DefaultIso}");
            psi.ArgumentList.Add($"-EXIF:ExposureTime={DefaultExposureTime}");
            psi.ArgumentList.Add($"-EXIF:ExposureProgram=2");
            psi.ArgumentList.Add($"-EXIF:MeteringMode=5");
            psi.ArgumentList.Add($"-EXIF:Flash=16");
            psi.ArgumentList.Add($"-EXIF:WhiteBalance=0");
            psi.ArgumentList.Add($"-EXIF:ColorSpace=1");
            psi.ArgumentList.Add($"-EXIF:SensingMethod=2");
            psi.ArgumentList.Add($"-EXIF:SceneCaptureType=0");

            DateTime ts = ExtractDate(file);
            string dtStr = ts.ToString("yyyy:MM:dd HH:mm:ss");
            psi.ArgumentList.Add($"-EXIF:DateTimeOriginal={dtStr}");
            psi.ArgumentList.Add($"-EXIF:CreateDate={dtStr}");
            psi.ArgumentList.Add($"-EXIF:ModifyDate={dtStr}");
        };

        if (overwriteAll)
        {
            // Modo "Reemplazar todo": Elimina todos los metadatos y aplica solo los del celular.
            if (preserveGps)
            {
                // Si requiere preservar GPS en modo reemplazo total, copiamos SOLO la data GPS original.
                psi.ArgumentList.Add("-tagsfromfile");
                psi.ArgumentList.Add("@");
                psi.ArgumentList.Add("-gps:all");
            }
            
            // Agregamos los datos del teléfono incondicionalmente
            addPhoneTags();
        }
        else
        {
            // Modo "Rellenar faltantes": Preserva todo el original (software, IPTC, XMP), solo rellena campos vacíos de hardware
            
            // 1. Agregamos los datos del teléfono (actúan como valores por defecto/fallback)
            addPhoneTags();

            // 2. Copiamos TODOS los metadatos originales encima (los originales sobreescribirán los fallbacks de arriba)
            psi.ArgumentList.Add("-tagsfromfile");
            psi.ArgumentList.Add("@");
            psi.ArgumentList.Add("-all:all");

            // 3. Si no desea preservar GPS, lo borramos explícitamente al final
            if (!preserveGps)
            {
                psi.ArgumentList.Add("-gps:all=");
            }
        }

        psi.ArgumentList.Add("-unsafe");
        psi.ArgumentList.Add($"-ExifByteOrder={ExifByteOrder}");

        psi.ArgumentList.Add(imagePath);

        using var process = System.Diagnostics.Process.Start(psi);
        if (process != null)
        {
            var errTask = process.StandardError.ReadToEndAsync();
            var outTask = process.StandardOutput.ReadToEndAsync();

            await System.Threading.Tasks.Task.WhenAll(errTask, outTask, process.WaitForExitAsync());

            string errors = errTask.Result;
            string output = outTask.Result;

            if (process.ExitCode != 0)
            {
                string msg = string.IsNullOrWhiteSpace(errors) ? 
                    (string.IsNullOrWhiteSpace(output) ? "Cierre silencioso con código " + process.ExitCode : output) 
                    : errors;
                throw new Exception($"ExifTool error ({process.ExitCode}): {msg}");
            }
        }
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
