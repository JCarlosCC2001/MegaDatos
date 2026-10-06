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

        var args = new System.Collections.Generic.List<string>();

        args.Add("-overwrite_original");
        args.Add("-m");
        args.Add("-P");
        
        // Siempre forzamos la reescritura total del contenedor EXIF con el ByteOrder especificado
        // para garantizar la coherencia forense del dispositivo.
        args.Add("-all=");

        Action addPhoneTags = () =>
        {
            // Borrar absolutamente todo de ExifIFD para garantizar que SOLO queden los que vamos a inyectar a continuación
            args.Add("-ExifIFD:all=");

            args.Add($"-EXIF:Make={ExifMake}");
            args.Add($"-EXIF:Model={ExifModel}");
            if (!string.IsNullOrEmpty(Software)) args.Add($"-EXIF:Software={Software}");
            args.Add($"-EXIF:FNumber={FNumber}");
            args.Add($"-EXIF:FocalLength={FocalLength}");
            args.Add($"-EXIF:ISO={DefaultIso}");
            args.Add($"-EXIF:ExposureTime={DefaultExposureTime}");
            args.Add($"-EXIF:ExposureProgram#=2");
            args.Add($"-EXIF:MeteringMode#=5");
            args.Add($"-EXIF:Flash#=16");
            args.Add($"-EXIF:WhiteBalance#=0");
            args.Add($"-EXIF:LightSource#=0"); // Unknown

            DateTime ts = ExtractDate(file);
            string dtStr = ts.ToString("yyyy:MM:dd HH:mm:ss");
            args.Add($"-EXIF:DateTimeOriginal={dtStr}");
            args.Add($"-EXIF:CreateDate={dtStr}");
            args.Add($"-EXIF:ModifyDate={dtStr}");
        };

        if (overwriteAll)
        {
            // Modo "Reemplazar todo": Elimina todos los metadatos y aplica solo los del celular.
            if (preserveGps)
            {
                // Si requiere preservar GPS en modo reemplazo total, copiamos SOLO la data GPS original.
                args.Add("-tagsfromfile");
                args.Add("@");
                args.Add("-gps:all");
            }
            
            // Agregamos los datos del teléfono incondicionalmente
            addPhoneTags();
        }
        else
        {
            // Modo "Preservar": Preserva todo el original (software, IPTC, XMP), pero el hardware del teléfono sobreescribe el original
            
            // 1. Copiamos TODOS los metadatos originales primero
            args.Add("-tagsfromfile");
            args.Add("@");
            args.Add("-all:all");

            // 2. Agregamos los datos del teléfono (sobreescribirán los datos de hardware originales copiados)
            addPhoneTags();

            // 3. Si no desea preservar GPS, lo borramos explícitamente al final
            if (!preserveGps)
            {
                args.Add("-gps:all=");
            }
        }

        // Rescatar datos originales importantes para no sobrescribirlos o perderlos.
        // Esto evita que al usar "Sobrescribir Todo", se pierda el trabajo previo de una plantilla 
        // (como la Orientación, Descripción o las Dimensiones exactas inyectadas previamente).
        args.Add("-tagsfromfile");
        args.Add("@");
        args.Add("-EXIF:Software");
        args.Add("-IFD0:Orientation");
        args.Add("-IFD0:ImageWidth");
        args.Add("-IFD0:ImageHeight");
        args.Add("-EXIF:ImageDescription");
        args.Add("-icc_profile"); // Conservar el perfil de color original (ICC-header, ICC_Profile)

        // Eliminar campos explícitamente a petición del usuario
        args.Add("-EXIF:YCbCrSubSampling=");
        args.Add("-EXIF:YCbCrPositioning=");

        args.Add("-unsafe");
        args.Add($"-ExifByteOrder={ExifByteOrder}");

        args.Add(imagePath);

        var result = await MegaDatos.Services.ExifToolService.ExecuteAsync(args, maxRetries: 1);

        if (!result.Success)
        {
            string msg = string.IsNullOrWhiteSpace(result.Error) ? 
                (string.IsNullOrWhiteSpace(result.Output) ? "Cierre silencioso con error" : result.Output) 
                : result.Error;
            throw new Exception($"ExifTool error: {msg}");
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
