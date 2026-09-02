using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MegaDatos.Models;

public partial class MetadataEntry : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _originalValue = string.Empty;

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(ValueAsDate));
                if (IsGpsCoordinate)
                {
                    SyncFromValue();
                }
            }
        }
    }

    [ObservableProperty]
    private bool _isEditable;

    [ObservableProperty]
    private string _group = string.Empty;

    [ObservableProperty]
    private string _editorType = "Text"; // "Text", "Date", "Rating", "Choice"

    [ObservableProperty]
    private List<string>? _choices;

    public bool IsTextEditor => EditorType == "Text" && !IsGpsCoordinate;
    public bool IsDateEditor => EditorType == "Date";
    public bool IsRatingEditor => EditorType == "Rating";
    public bool IsChoiceEditor => EditorType == "Choice";

    // GPS Specific Helper Fields
    public bool IsGpsCoordinate => Key == "Latitud" || Key == "Longitud";

    public bool IsGpsDecimalEditor => IsGpsCoordinate && GpsFormat == "Decimal";
    public bool IsGpsDmsEditor => IsGpsCoordinate && GpsFormat == "DMS";

    private string _gpsFormat = "Decimal";
    public string GpsFormat
    {
        get => _gpsFormat;
        set
        {
            if (SetProperty(ref _gpsFormat, value))
            {
                UpdateValueFromSubProperties();
                OnPropertyChanged(nameof(IsGpsDecimalEditor));
                OnPropertyChanged(nameof(IsGpsDmsEditor));
            }
        }
    }

    private string _decimalDegreesValue = "0";
    public string DecimalDegreesValue
    {
        get => _decimalDegreesValue;
        set
        {
            if (SetProperty(ref _decimalDegreesValue, value))
            {
                UpdateValueFromSubProperties();
            }
        }
    }

    private string _dmsDegrees = "0";
    public string DmsDegrees
    {
        get => _dmsDegrees;
        set
        {
            if (SetProperty(ref _dmsDegrees, value))
            {
                UpdateValueFromSubProperties();
            }
        }
    }

    private string _dmsMinutes = "0";
    public string DmsMinutes
    {
        get => _dmsMinutes;
        set
        {
            if (SetProperty(ref _dmsMinutes, value))
            {
                UpdateValueFromSubProperties();
            }
        }
    }

    private string _dmsSeconds = "0";
    public string DmsSeconds
    {
        get => _dmsSeconds;
        set
        {
            if (SetProperty(ref _dmsSeconds, value))
            {
                UpdateValueFromSubProperties();
            }
        }
    }

    private string _hemisphere = string.Empty;
    public string Hemisphere
    {
        get => _hemisphere;
        set
        {
            if (SetProperty(ref _hemisphere, value))
            {
                UpdateValueFromSubProperties();
            }
        }
    }

    public List<string> HemisphereChoices => Key == "Latitud"
        ? new List<string> { "N", "S" }
        : new List<string> { "E", "W" };

    public DateTime? ValueAsDate
    {
        get
        {
            if (DateTime.TryParse(Value, out var date))
                return date;
            return null;
        }
        set
        {
            if (value.HasValue)
            {
                Value = value.Value.ToString("dd/MM/yyyy HH:mm");
            }
            OnPropertyChanged(nameof(ValueAsDate));
        }
    }

    private bool _isSyncing;

    public void SyncFromValue()
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            if (string.IsNullOrWhiteSpace(Value)) return;

            string upper = Value.ToUpper();
            string hem = Key == "Latitud"
                ? (upper.Contains("S") ? "S" : "N")
                : (upper.Contains("W") ? "W" : "E");

            _hemisphere = hem;
            OnPropertyChanged(nameof(Hemisphere));

            string clean = Value.Replace("°", "").Replace("'", "").Replace("\"", "").Trim();
            if (clean.EndsWith("S") || clean.EndsWith("N") || clean.EndsWith("E") || clean.EndsWith("W"))
            {
                clean = clean.Substring(0, clean.Length - 1).Trim();
            }

            if (double.TryParse(clean, out var decVal))
            {
                _decimalDegreesValue = Math.Abs(decVal).ToString();
                OnPropertyChanged(nameof(DecimalDegreesValue));

                double absVal = Math.Abs(decVal);
                int deg = (int)Math.Truncate(absVal);
                double minRemainder = (absVal - deg) * 60;
                int min = (int)Math.Truncate(minRemainder);
                double sec = Math.Round((minRemainder - min) * 60, 2);

                _dmsDegrees = deg.ToString();
                _dmsMinutes = min.ToString();
                _dmsSeconds = sec.ToString();

                OnPropertyChanged(nameof(DmsDegrees));
                OnPropertyChanged(nameof(DmsMinutes));
                OnPropertyChanged(nameof(DmsSeconds));
            }
            else
            {
                var parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 &&
                    double.TryParse(parts[0], out var deg) &&
                    double.TryParse(parts[1], out var min) &&
                    double.TryParse(parts[2], out var sec))
                {
                    _dmsDegrees = ((int)deg).ToString();
                    _dmsMinutes = ((int)min).ToString();
                    _dmsSeconds = Math.Round(sec, 2).ToString();
                    _decimalDegreesValue = (deg + (min / 60.0) + (sec / 3600.0)).ToString();

                    OnPropertyChanged(nameof(DecimalDegreesValue));
                    OnPropertyChanged(nameof(DmsDegrees));
                    OnPropertyChanged(nameof(DmsMinutes));
                    OnPropertyChanged(nameof(DmsSeconds));
                }
            }
        }
        catch { }
        finally
        {
            _isSyncing = false;
        }
    }

    public void UpdateValueFromSubProperties()
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            if (GpsFormat == "Decimal")
            {
                if (double.TryParse(_decimalDegreesValue, out double dec))
                {
                    int deg = (int)Math.Truncate(dec);
                    double minRemainder = (dec - deg) * 60;
                    int min = (int)Math.Truncate(minRemainder);
                    double sec = Math.Round((minRemainder - min) * 60, 2);

                    _dmsDegrees = deg.ToString();
                    _dmsMinutes = min.ToString();
                    _dmsSeconds = sec.ToString();

                    OnPropertyChanged(nameof(DmsDegrees));
                    OnPropertyChanged(nameof(DmsMinutes));
                    OnPropertyChanged(nameof(DmsSeconds));

                    _value = $"{dec:F5}° {Hemisphere}";
                    OnPropertyChanged(nameof(Value));
                }
            }
            else // DMS format
            {
                if (int.TryParse(_dmsDegrees, out int deg) && int.TryParse(_dmsMinutes, out int min) && double.TryParse(_dmsSeconds, out double sec))
                {
                    double dec = deg + (min / 60.0) + (sec / 3600.0);
                    _decimalDegreesValue = dec.ToString();
                    OnPropertyChanged(nameof(DecimalDegreesValue));

                    _value = $"{deg}° {min}' {sec:F2}\" {Hemisphere}";
                    OnPropertyChanged(nameof(Value));
                }
            }
        }
        catch { }
        finally
        {
            _isSyncing = false;
        }
    }
}
