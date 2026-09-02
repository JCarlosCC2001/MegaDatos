using System;
using System.Collections.Generic;
using System.Linq;

namespace MegaDatos.Models.Devices;

public class PhoneDeviceRegistry
{
    private static readonly Lazy<PhoneDeviceRegistry> _instance = new(() => new PhoneDeviceRegistry());
    public static PhoneDeviceRegistry Instance => _instance.Value;

    private readonly List<PhoneDeviceProfile> _devices = new();

    public IReadOnlyList<PhoneDeviceProfile> Devices => _devices;

    public PhoneDeviceRegistry()
    {
        // 🍏 Apple iPhone
        Register(new PhoneDeviceProfile
        {
            Id = "iphone15pro",
            Brand = "Apple",
            ModelName = "iPhone 15 Pro",
            ExifMake = "Apple",
            ExifModel = "iPhone 15 Pro",
            Software = "17.5.1",
            LensMake = "Apple",
            LensModel = "iPhone 15 Pro back triple camera 6.78mm f/1.78",
            FNumber = 1.78,
            FocalLength = 6.78,
            FocalLength35mm = 24,
            DefaultIso = 64,
            Description = "Cámara principal de 48 MP con sensor Quad-Pixel y lente de 24 mm equivalente.",
            IconGlyph = "🍏"
        });

        Register(new PhoneDeviceProfile
        {
            Id = "iphone14",
            Brand = "Apple",
            ModelName = "iPhone 14",
            ExifMake = "Apple",
            ExifModel = "iPhone 14",
            Software = "16.6",
            LensMake = "Apple",
            LensModel = "iPhone 14 back dual camera 5.7mm f/1.5",
            FNumber = 1.5,
            FocalLength = 5.7,
            FocalLength35mm = 26,
            DefaultIso = 50,
            Description = "Cámara principal de 12 MP con apertura f/1.5 y Photonic Engine.",
            IconGlyph = "🍏"
        });

        // 🌌 Samsung Galaxy
        Register(new PhoneDeviceProfile
        {
            Id = "samsung_s24ultra",
            Brand = "Samsung",
            ModelName = "Galaxy S24 Ultra",
            ExifMake = "samsung",
            ExifModel = "SM-S928B",
            Software = "S928BXXU1AXCA",
            LensMake = "Samsung",
            LensModel = "Samsung ISOCELL HP2 200MP Wide Camera",
            FNumber = 1.7,
            FocalLength = 6.3,
            FocalLength35mm = 23,
            DefaultIso = 50,
            Description = "Cámara de 200 MP con sensor ISOCELL HP2 y procesamiento ProVisual Engine.",
            IconGlyph = "🌌"
        });

        Register(new PhoneDeviceProfile
        {
            Id = "samsung_s23",
            Brand = "Samsung",
            ModelName = "Galaxy S23",
            ExifMake = "samsung",
            ExifModel = "SM-S911B",
            Software = "S911BXXU3BWJM",
            LensMake = "Samsung",
            LensModel = "Samsung 50MP Wide Camera f/1.8",
            FNumber = 1.8,
            FocalLength = 5.4,
            FocalLength35mm = 24,
            DefaultIso = 50,
            Description = "Cámara de 50 MP Dual Pixel AF con estabilización óptica OIS.",
            IconGlyph = "🌌"
        });

        // 🟠 Xiaomi / Redmi
        Register(new PhoneDeviceProfile
        {
            Id = "xiaomi_14",
            Brand = "Xiaomi",
            ModelName = "Xiaomi 14 / Leica",
            ExifMake = "Xiaomi",
            ExifModel = "23127PN0CG",
            Software = "1.0.14.0.UNCMIXM",
            LensMake = "Leica",
            LensModel = "LEICA VARIO-SUMMILUX 1:1.6-2.2/14-75 ASPH.",
            FNumber = 1.6,
            FocalLength = 6.8,
            FocalLength35mm = 23,
            DefaultIso = 50,
            Description = "Óptica Leica Summilux de 50 MP con sensor Light Fusion 900.",
            IconGlyph = "🟠"
        });

        Register(new PhoneDeviceProfile
        {
            Id = "redmi_note13pro",
            Brand = "Xiaomi",
            ModelName = "Redmi Note 13 Pro",
            ExifMake = "Xiaomi",
            ExifModel = "2406APNFAG",
            Software = "MIUI 14 / HyperOS",
            LensMake = "Xiaomi",
            LensModel = "200MP ISOCELL HP3 f/1.65",
            FNumber = 1.65,
            FocalLength = 5.35,
            FocalLength35mm = 24,
            DefaultIso = 100,
            Description = "Cámara ultranítida de 200 MP con sensor Samsung ISOCELL HP3.",
            IconGlyph = "🟠"
        });

        // 🔵 Google Pixel
        Register(new PhoneDeviceProfile
        {
            Id = "pixel_8pro",
            Brand = "Google",
            ModelName = "Pixel 8 Pro",
            ExifMake = "Google",
            ExifModel = "Pixel 8 Pro",
            Software = "UQ1A.240205.004",
            LensMake = "Google",
            LensModel = "Pixel 8 Pro back camera 6.9mm f/1.68",
            FNumber = 1.68,
            FocalLength = 6.9,
            FocalLength35mm = 25,
            DefaultIso = 40,
            Description = "Cámara Octa PD de 50 MP con procesamiento computacional Google Tensor.",
            IconGlyph = "🔵"
        });

        // 🟣 Motorola
        Register(new PhoneDeviceProfile
        {
            Id = "motorola_edge50",
            Brand = "Motorola",
            ModelName = "Edge 50 Pro",
            ExifMake = "motorola",
            ExifModel = "motorola edge 50 pro",
            Software = "U1TR34.8-19-4",
            LensMake = "Motorola",
            LensModel = "50MP 1/1.3\" f/1.4 OIS",
            FNumber = 1.4,
            FocalLength = 5.9,
            FocalLength35mm = 24,
            DefaultIso = 100,
            Description = "Cámara de 50 MP con gran apertura f/1.4 y enfoque instantáneo.",
            IconGlyph = "🟣"
        });
    }

    public void Register(PhoneDeviceProfile device)
    {
        if (!_devices.Any(d => string.Equals(d.Id, device.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _devices.Add(device);
        }
    }

    public PhoneDeviceProfile? GetById(string id)
    {
        return _devices.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
