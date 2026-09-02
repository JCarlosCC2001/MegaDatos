using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using ImageMagick;

namespace MegaDatos.Models.Templates;

public class TimeStampTemplate : IMetadataTemplate
{
    public string Id => "TimeStamp";
    public string Name => "Timestamp Camera (Móvil)";
    public string Description => "Simula fotografías capturadas con la aplicación móvil 'Timestamp Camera'. Incrusta la firma 'In Timestamp Camera', datos de cámara móvil, sincronización de marcas de tiempo y coordenadas GPS.";
    public string IconGlyph => "⏱️";
    public string Category => "Cámara Móvil & Peritaje";
    public string PreservesSummary => "Parámetros ópticos de cámara (Make, Model, apertura, ISO, distancia focal) y coordenadas GPS.";
    public string RemovesSummary => "Perfiles pesados de edición de escritorio (XMP, IPTC de software externo).";
    public string InjectsSummary => "Software: 'In Timestamp Camera', fechas sincronizadas (DateTimeOriginal, DateTimeDigitized, DateTime) y estructura EXIF móvil.";

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

            if (exif == null)
            {
                result.State = ComplianceState.Incomplete;
                result.SummaryMessage = "Falta el bloque de metadatos EXIF con las marcas de tiempo y GPS";
                result.Details.Add("No se encontró perfil EXIF en el archivo.");
                return result;
            }

            string? software = exif.GetValue(ExifTag.Software)?.Value?.ToString();
            var dtOrig = exif.GetValue(ExifTag.DateTimeOriginal)?.Value?.ToString();
            var dtDigitized = exif.GetValue(ExifTag.DateTimeDigitized)?.Value?.ToString();
            var latVal = exif.GetValue(ExifTag.GPSLatitude)?.Value;
            var lonVal = exif.GetValue(ExifTag.GPSLongitude)?.Value;

            // 1. Check software signature
            bool hasSoftware = !string.IsNullOrEmpty(software) && 
                               (software.Contains("Timestamp Camera", StringComparison.OrdinalIgnoreCase) || 
                                software.Contains("In Timestamp", StringComparison.OrdinalIgnoreCase));

            // 2. Check desktop profiles that shouldn't be in a mobile raw timestamp capture
            var xmp = img.GetXmpProfile();
            var iptc = img.GetIptcProfile();
            if (xmp != null || iptc != null)
            {
                result.State = ComplianceState.NonCompliant;
                result.SummaryMessage = "Contiene perfiles de edición de escritorio (XMP/IPTC) no propios de Timestamp Camera";
                result.Details.Add("Detectados perfiles de software de retoque o autoría.");
                return result;
            }

            // 3. Check for missing required fields
            if (!hasSoftware)
            {
                result.State = ComplianceState.Incomplete;
                result.SummaryMessage = "Falta la firma de software de Timestamp Camera";
                result.Details.Add($"Software actual: {(string.IsNullOrEmpty(software) ? "Ninguno" : software)}");
                result.Details.Add("Se requiere 'In Timestamp Camera'.");
                return result;
            }

            if (string.IsNullOrEmpty(dtOrig))
            {
                result.State = ComplianceState.Incomplete;
                result.SummaryMessage = "Falta la fecha de captura original (DateTimeOriginal)";
                result.Details.Add("Campo DateTimeOriginal requerido por Timestamp Camera.");
                return result;
            }

            if (latVal == null || lonVal == null || file.GpsDecimal == "—")
            {
                result.State = ComplianceState.Incomplete;
                result.SummaryMessage = "Faltan las coordenadas GPS requeridas por el rotulado de Timestamp Camera";
                result.Details.Add("No se detectaron coordenadas GPS en el perfil EXIF.");
                return result;
            }

            result.State = ComplianceState.Compliant;
            result.SummaryMessage = "Cumple al 100% con el estándar de Timestamp Camera móvil";
            result.Details.Add($"Firma de software: {software}");
            result.Details.Add($"Fecha sincronizada: {dtOrig}");
            result.Details.Add($"Coordenadas GPS: {file.GpsDecimal}");
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
        // Remove desktop profiles
        image.RemoveProfile("xmp");
        image.RemoveProfile("iptc");

        var exif = image.GetExifProfile() ?? new ExifProfile();

        // 1. Inject Timestamp Camera software signature
        exif.SetValue(ExifTag.Software, "In Timestamp Camera");

        // 2. Determine timestamp: from filename (e.g. TimePhoto_YYYYMMDD_HHMMSS) or file date
        DateTime timestamp = ExtractDateFromFilenameOrFile(file);
        string dtStr = timestamp.ToString("yyyy:MM:dd HH:mm:ss");

        exif.SetValue(ExifTag.DateTimeOriginal, dtStr);
        exif.SetValue(ExifTag.DateTimeDigitized, dtStr);
        exif.SetValue(ExifTag.DateTime, dtStr);

        // 3. Ensure Mobile Camera Make/Model exists
        var makeVal = exif.GetValue(ExifTag.Make)?.Value?.ToString();
        var modelVal = exif.GetValue(ExifTag.Model)?.Value?.ToString();
        if (string.IsNullOrEmpty(makeVal))
        {
            exif.SetValue(ExifTag.Make, "Xiaomi");
        }
        if (string.IsNullOrEmpty(modelVal))
        {
            exif.SetValue(ExifTag.Model, "Mobile Camera");
        }

        image.SetProfile(exif);
    }

    private static DateTime ExtractDateFromFilenameOrFile(FileItem file)
    {
        // Try pattern TimePhoto_YYYYMMDD_HHMMSS
        var match = Regex.Match(file.Name, @"TimePhoto_(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int year) &&
                int.TryParse(match.Groups[2].Value, out int month) &&
                int.TryParse(match.Groups[3].Value, out int day) &&
                int.TryParse(match.Groups[4].Value, out int hour) &&
                int.TryParse(match.Groups[5].Value, out int min) &&
                int.TryParse(match.Groups[6].Value, out int sec))
            {
                try
                {
                    return new DateTime(year, month, day, hour, min, sec);
                }
                catch { }
            }
        }

        if (File.Exists(file.FullPath))
        {
            return File.GetCreationTime(file.FullPath);
        }

        return file.DateModified;
    }
}
