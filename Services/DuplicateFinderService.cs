using System;
using System.IO;
using System.Security.Cryptography;
using ImageMagick;

namespace MegaDatos.Services;

public static class DuplicateFinderService
{
    // Compute Difference Hash (dHash)
    public static ulong ComputeDHash(string imagePath)
    {
        try
        {
            using var image = new MagickImage(imagePath);
            // Redimensionar a 9x8 para tener 8 comparaciones por fila (8 filas * 8 comparaciones = 64 bits)
            image.Resize(new MagickGeometry(9, 8) { IgnoreAspectRatio = true });
            image.Grayscale();

            using var pixels = image.GetPixels();
            ulong hash = 0;

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var leftPixel = pixels.GetPixel(x, y).ToColor();
                    var rightPixel = pixels.GetPixel(x + 1, y).ToColor();

                    // Comparar luminosidad
                    if (leftPixel != null && rightPixel != null && leftPixel.R > rightPixel.R)
                    {
                        hash |= (1UL << ((y * 8) + x));
                    }
                }
            }

            return hash;
        }
        catch
        {
            return 0; // Error / No es imagen soportada
        }
    }

    public static int HammingDistance(ulong hash1, ulong hash2)
    {
        ulong x = hash1 ^ hash2;
        int setBits = 0;
        while (x > 0)
        {
            setBits += (int)(x & 1);
            x >>= 1;
        }
        return setBits;
    }
}
