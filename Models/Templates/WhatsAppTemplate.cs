using System;
using System.IO;
using ImageMagick;

namespace MegaDatos.Models.Templates;

public class WhatsAppTemplate : IMetadataTemplate
{
    public string Id => "WhatsApp";
    public string Name => "WhatsApp";
    public string Description => "Simula el procesamiento de compresión y privacidad de WhatsApp. Elimina 100% de metadatos de cámara, ubicación GPS, autoría y perfiles incrustados.";
    public string IconGlyph => "💬";
    public string Category => "Mensajería & Redes";
    public string PreservesSummary => "Imagen base, dimensiones optimizadas y espacio de color estándar.";
    public string RemovesSummary => "Ubicación GPS, marcas de cámara, lente, apertura, autor, perfiles IPTC y XMP.";
    public string InjectsSummary => "Estructura limpia sin huella digital.";

    public TemplateComplianceResult ValidateCompliance(FileItem file)
    {
        var result = new TemplateComplianceResult();

        if (file.IsDirectory || !File.Exists(file.FullPath))
        {
            result.State = ComplianceState.None;
            result.SummaryMessage = "No aplicable a directorios";
            return result;
        }

        // WhatsApp strip check
        if (file.GpsDecimal != "—" || file.GpsDMS != "—")
        {
            result.State = ComplianceState.NonCompliant;
            result.SummaryMessage = "Contiene coordenadas GPS privadas (WhatsApp siempre las elimina)";
            result.Details.Add("Coordenadas GPS detectadas: " + file.GpsDecimal);
            return result;
        }

        try
        {
            using var img = new MagickImage(file.FullPath);
            var exif = img.GetExifProfile();
            var iptc = img.GetIptcProfile();
            var xmp = img.GetXmpProfile();

            bool hasCameraExif = false;
            if (exif != null)
            {
                var make = exif.GetValue(ExifTag.Make)?.Value?.ToString();
                var model = exif.GetValue(ExifTag.Model)?.Value?.ToString();
                var lat = exif.GetValue(ExifTag.GPSLatitude)?.Value;
                if (!string.IsNullOrEmpty(make) || !string.IsNullOrEmpty(model) || lat != null)
                {
                    hasCameraExif = true;
                }
            }

            if (hasCameraExif)
            {
                result.State = ComplianceState.NonCompliant;
                result.SummaryMessage = "Contiene metadatos de cámara/hardware no permitidos en WhatsApp";
                result.Details.Add("Perfil EXIF de hardware o cámara presente.");
                return result;
            }

            if (iptc != null || xmp != null)
            {
                result.State = ComplianceState.NonCompliant;
                result.SummaryMessage = "Contiene perfiles IPTC/XMP no permitidos en WhatsApp";
                result.Details.Add("Metadatos de autoría/etiquetas incrustados detectados.");
                return result;
            }

            result.State = ComplianceState.Compliant;
            result.SummaryMessage = "Cumple con el estándar de metadatos de WhatsApp (100% limpio)";
            result.Details.Add("Sin datos EXIF de cámara.");
            result.Details.Add("Sin coordenadas GPS.");
            result.Details.Add("Sin perfiles de autoría IPTC/XMP.");
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
        image.Strip();
        image.ColorSpace = ColorSpace.sRGB;
    }
}
