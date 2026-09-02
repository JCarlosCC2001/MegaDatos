using System;
using System.IO;
using ImageMagick;

namespace MegaDatos.Models.Templates;

public class ScreenshotTemplate : IMetadataTemplate
{
    public string Id => "Screenshot";
    public string Name => "Captura de Pantalla";
    public string Description => "Configura el archivo como una captura de pantalla del sistema operativo (Windows Snipping Tool). Elimina datos de cámara y GPS, fijando la densidad en 96 DPI.";
    public string IconGlyph => "🖥️";
    public string Category => "Sistema Operativo";
    public string PreservesSummary => "Imagen capturada, dimensiones en píxeles y espacio de color sRGB.";
    public string RemovesSummary => "Datos ópticos de cámara (lente, apertura, ISO, obturación, flash) y coordenadas GPS.";
    public string InjectsSummary => "Software: 'Windows Snipping Tool', resolución 96 DPI y estructura estándar de pantalla.";

    public TemplateComplianceResult ValidateCompliance(FileItem file)
    {
        var result = new TemplateComplianceResult();

        if (file.IsDirectory || !File.Exists(file.FullPath))
        {
            result.State = ComplianceState.None;
            result.SummaryMessage = "No aplicable a directorios";
            return result;
        }

        // GPS Check: screenshots never have GPS
        if (file.GpsDecimal != "—" || file.GpsDMS != "—")
        {
            result.State = ComplianceState.NonCompliant;
            result.SummaryMessage = "Contiene coordenadas GPS (una captura de pantalla nunca tiene GPS)";
            result.Details.Add("Coordenadas GPS detectadas.");
            return result;
        }

        try
        {
            using var img = new MagickImage(file.FullPath);
            var exif = img.GetExifProfile();

            if (exif != null)
            {
                var make = exif.GetValue(ExifTag.Make)?.Value?.ToString();
                var model = exif.GetValue(ExifTag.Model)?.Value?.ToString();
                var lens = exif.GetValue(ExifTag.LensModel)?.Value?.ToString();
                var fNumber = exif.GetValue(ExifTag.FNumber)?.Value;
                var iso = exif.GetValue(ExifTag.ISOSpeedRatings)?.Value;

                if (!string.IsNullOrEmpty(make) || !string.IsNullOrEmpty(model) || !string.IsNullOrEmpty(lens) || fNumber != null || iso != null)
                {
                    result.State = ComplianceState.NonCompliant;
                    result.SummaryMessage = "Contiene metadatos de hardware de cámara no propios de una captura";
                    result.Details.Add($"Cámara detectada: {make} {model}".Trim());
                    result.Details.Add("Datos ópticos (lente, apertura, ISO) presentes.");
                    return result;
                }

                var software = exif.GetValue(ExifTag.Software)?.Value?.ToString();
                if (string.IsNullOrEmpty(software) || !software.Contains("Snipping", StringComparison.OrdinalIgnoreCase))
                {
                    result.State = ComplianceState.Incomplete;
                    result.SummaryMessage = "Falta la firma de software de captura de pantalla";
                    result.Details.Add("Se recomienda firma 'Windows Snipping Tool'.");
                    return result;
                }
            }
            else
            {
                result.State = ComplianceState.Incomplete;
                result.SummaryMessage = "Falta la firma de software de captura de pantalla";
                result.Details.Add("Sin perfil EXIF con firma de captura.");
                return result;
            }

            result.State = ComplianceState.Compliant;
            result.SummaryMessage = "Cumple con el perfil de Captura de Pantalla";
            result.Details.Add("Sin datos de cámara ni GPS.");
            result.Details.Add("Firma de software de captura presente.");
        }
        catch (Exception ex)
        {
            result.State = ComplianceState.None;
            result.SummaryMessage = $"Error al evaluar archivo: {ex.Message}";
        }

        return result;
    }

    public void Apply(MagickImage image, FileItem file)
    {
        image.Density = new Density(96, 96);

        var exif = image.GetExifProfile() ?? new ExifProfile();

        // Remove all camera & GPS values
        exif.RemoveValue(ExifTag.Make);
        exif.RemoveValue(ExifTag.Model);
        exif.RemoveValue(ExifTag.LensModel);
        exif.RemoveValue(ExifTag.LensMake);
        exif.RemoveValue(ExifTag.FNumber);
        exif.RemoveValue(ExifTag.ExposureTime);
        exif.RemoveValue(ExifTag.ISOSpeedRatings);
        exif.RemoveValue(ExifTag.Flash);
        exif.RemoveValue(ExifTag.FocalLength);
        exif.RemoveValue(ExifTag.ExposureProgram);
        exif.RemoveValue(ExifTag.WhiteBalance);

        exif.RemoveValue(ExifTag.GPSLatitude);
        exif.RemoveValue(ExifTag.GPSLatitudeRef);
        exif.RemoveValue(ExifTag.GPSLongitude);
        exif.RemoveValue(ExifTag.GPSLongitudeRef);
        exif.RemoveValue(ExifTag.GPSAltitude);
        exif.RemoveValue(ExifTag.GPSAltitudeRef);
        exif.RemoveValue(ExifTag.GPSVersionID);

        // Inject Screenshot software
        exif.SetValue(ExifTag.Software, "Windows Snipping Tool");
        string nowStr = DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss");
        exif.SetValue(ExifTag.DateTime, nowStr);

        image.SetProfile(exif);
    }
}
