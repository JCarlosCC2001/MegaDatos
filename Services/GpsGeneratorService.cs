using System;
using System.Globalization;
using System.Text.RegularExpressions;
using ImageMagick;

namespace MegaDatos.Services;

public static class GpsGeneratorService
{
    private static readonly Random _random = new();

    /// <summary>
    /// Genera una coordenada (lat, lon) aleatoria uniformemente distribuida dentro de un círculo de radio dado (en metros).
    /// </summary>
    public static (double Lat, double Lon) GenerateRandomCoordinate(double centerLat, double centerLon, double radiusInMeters)
    {
        if (radiusInMeters <= 0)
        {
            return (centerLat, centerLon);
        }

        // Distribución uniforme en disco circular: r = R * sqrt(u), theta = 2 * PI * v
        double u = _random.NextDouble();
        double v = _random.NextDouble();

        double distance = radiusInMeters * Math.Sqrt(u);
        double angle = 2 * Math.PI * v;

        // Desplazamiento aproximado en metros (1 grado latitud ~ 111,139 metros)
        double deltaLat = (distance * Math.Cos(angle)) / 111139.0;
        double centerLatRad = centerLat * (Math.PI / 180.0);
        double cosLat = Math.Cos(centerLatRad);
        if (Math.Abs(cosLat) < 1e-6) cosLat = 1e-6;
        double deltaLon = (distance * Math.Sin(angle)) / (111139.0 * cosLat);

        double newLat = Math.Clamp(centerLat + deltaLat, -90.0, 90.0);
        double newLon = Math.Clamp(centerLon + deltaLon, -180.0, 180.0);

        return (Math.Round(newLat, 6), Math.Round(newLon, 6));
    }

    /// <summary>
    /// Calcula la distancia de Haversine en metros entre dos puntos geográficos.
    /// </summary>
    public static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double r = 6371000.0; // Radio de la tierra en metros
        double dLat = (lat2 - lat1) * (Math.PI / 180.0);
        double dLon = (lon2 - lon1) * (Math.PI / 180.0);
        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(lat1 * (Math.PI / 180.0)) * Math.Cos(lat2 * (Math.PI / 180.0)) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return r * c;
    }

    /// <summary>
    /// Intenta parsear una cadena de coordenadas como "-12.065130, -75.204860" o "12.065 S, 75.204 W".
    /// </summary>
    public static (double Lat, double Lon)? ParseCoordinates(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        input = input.Trim();

        // Formato Decimal simple: lat, lon
        var parts = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            if (double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
            {
                if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
                {
                    return (lat, lon);
                }
            }
        }

        // Intento con Regex para capturar dos números decimales
        var match = Regex.Match(input, @"([-+]?\d{1,3}(?:\.\d+)?)\s*[,;\s]\s*([-+]?\d{1,3}(?:\.\d+)?)");
        if (match.Success)
        {
            if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
            {
                if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
                {
                    return (lat, lon);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Convierte coordenadas decimales a formato DMS (Grados, Minutos, Segundos).
    /// </summary>
    public static string ToDms(double lat, double lon)
    {
        string latRef = lat >= 0 ? "N" : "S";
        double absLat = Math.Abs(lat);
        int latDeg = (int)absLat;
        double latMinTotal = (absLat - latDeg) * 60;
        int latMin = (int)latMinTotal;
        double latSec = (latMinTotal - latMin) * 60;

        string lonRef = lon >= 0 ? "E" : "W";
        double absLon = Math.Abs(lon);
        int lonDeg = (int)absLon;
        double lonMinTotal = (absLon - lonDeg) * 60;
        int lonMin = (int)lonMinTotal;
        double lonSec = (lonMinTotal - lonMin) * 60;

        return $"{latDeg}° {latMin}' {latSec.ToString("F2", CultureInfo.InvariantCulture)}\" {latRef}, {lonDeg}° {lonMin}' {lonSec.ToString("F2", CultureInfo.InvariantCulture)}\" {lonRef}";
    }

    /// <summary>
    /// Inyecta coordenadas GPS en el perfil EXIF de una imagen.
    /// </summary>
    public static void ApplyGpsToImage(MagickImage image, double lat, double lon, double? altitude = null)
    {
        var exif = image.GetExifProfile() ?? new ExifProfile();

        // Latitud
        string latRef = lat >= 0 ? "N" : "S";
        double absLat = Math.Abs(lat);
        uint latDeg = (uint)Math.Floor(absLat);
        double latMinTotal = (absLat - latDeg) * 60.0;
        uint latMin = (uint)Math.Floor(latMinTotal);
        uint latSecNumerator = (uint)Math.Round((latMinTotal - latMin) * 60.0 * 100.0);

        exif.SetValue(ExifTag.GPSLatitudeRef, latRef);
        exif.SetValue(ExifTag.GPSLatitude, new[]
        {
            new Rational(latDeg, 1),
            new Rational(latMin, 1),
            new Rational(latSecNumerator, 100)
        });

        // Longitud
        string lonRef = lon >= 0 ? "E" : "W";
        double absLon = Math.Abs(lon);
        uint lonDeg = (uint)Math.Floor(absLon);
        double lonMinTotal = (absLon - lonDeg) * 60.0;
        uint lonMin = (uint)Math.Floor(lonMinTotal);
        uint lonSecNumerator = (uint)Math.Round((lonMinTotal - lonMin) * 60.0 * 100.0);

        exif.SetValue(ExifTag.GPSLongitudeRef, lonRef);
        exif.SetValue(ExifTag.GPSLongitude, new[]
        {
            new Rational(lonDeg, 1),
            new Rational(lonMin, 1),
            new Rational(lonSecNumerator, 100)
        });

        // A petición del usuario, SOLO se aplican los datos de Latitud y Longitud con sus referencias.
        // No se aplica GPSAltitude, GPSAltitudeRef ni GPSVersionID en este punto.

        image.SetProfile(exif);
    }
}
