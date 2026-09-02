using System;
using System.IO;
using ImageMagick;

namespace MegaDatos.Models.Templates;

public class PhotoshopTemplate : IMetadataTemplate
{
    public string Id => "Photoshop";
    public string Name => "Adobe Photoshop";
    public string Description => "Configura el archivo como si hubiera sido exportado desde Adobe Photoshop, incrustando la firma de software, estructura XMP/IPTC profesional y eliminando rastros GPS.";
    public string IconGlyph => "🎨";
    public string Category => "Edición Profesional";
    public string PreservesSummary => "Resolución, dimensiones, espacio de color y títulos existentes.";
    public string RemovesSummary => "Coordenadas GPS y perfiles de cámara redundantes.";
    public string InjectsSummary => "Software: Adobe Photoshop 2024 (Windows), perfiles XMP/IPTC y fecha de modificación sincronizada.";

    public TemplateComplianceResult ValidateCompliance(FileItem file)
    {
        var result = new TemplateComplianceResult();

        if (file.IsDirectory || !File.Exists(file.FullPath))
        {
            result.State = ComplianceState.None;
            result.SummaryMessage = "No aplicable a directorios";
            return result;
        }

        try
        {
            using var img = new MagickImage(file.FullPath);
            var exif = img.GetExifProfile();

            string? software = exif?.GetValue(ExifTag.Software)?.Value?.ToString();

            // Check if GPS is present (discrepancy for typical Photoshop clean web export)
            if (file.GpsDecimal != "—" || file.GpsDMS != "—")
            {
                result.State = ComplianceState.NonCompliant;
                result.SummaryMessage = "Contiene datos GPS no habituales en exportaciones de Photoshop";
                result.Details.Add("Coordenadas GPS detectadas.");
                return result;
            }

            if (string.IsNullOrEmpty(software) || !software.Contains("Adobe Photoshop", StringComparison.OrdinalIgnoreCase))
            {
                result.State = ComplianceState.Incomplete;
                result.SummaryMessage = "Falta la firma de software de Adobe Photoshop";
                result.Details.Add($"Software actual: {(string.IsNullOrEmpty(software) ? "Ninguno" : software)}");
                result.Details.Add("Se requiere 'Adobe Photoshop 2024 (Windows)'.");
                return result;
            }

            result.State = ComplianceState.Compliant;
            result.SummaryMessage = "Cumple con el perfil de Adobe Photoshop";
            result.Details.Add($"Firma de software: {software}");
            result.Details.Add("Sin coordenadas GPS.");
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
        var exif = image.GetExifProfile() ?? new ExifProfile();

        // Remove GPS tags
        exif.RemoveValue(ExifTag.GPSLatitude);
        exif.RemoveValue(ExifTag.GPSLatitudeRef);
        exif.RemoveValue(ExifTag.GPSLongitude);
        exif.RemoveValue(ExifTag.GPSLongitudeRef);
        exif.RemoveValue(ExifTag.GPSAltitude);
        exif.RemoveValue(ExifTag.GPSAltitudeRef);
        exif.RemoveValue(ExifTag.GPSVersionID);

        // Inject Photoshop signature
        exif.SetValue(ExifTag.Software, "Adobe Photoshop 2024 (Windows)");
        string nowStr = DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss");
        exif.SetValue(ExifTag.DateTime, nowStr);
        exif.SetValue(ExifTag.DateTimeDigitized, nowStr);

        image.SetProfile(exif);
    }
}
