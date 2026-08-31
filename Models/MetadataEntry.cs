using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MegaDatos.Models;

public partial class MetadataEntry : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

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

    private double _decimalDegreesValue;
    public double DecimalDegreesValue
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

    private int _dmsDegrees;
    public int DmsDegrees
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

    private int _dmsMinutes;
    public int DmsMinutes
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

    private double _dmsSeconds;
    public double DmsSeconds
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
                _decimalDegreesValue = Math.Abs(decVal);
                OnPropertyChanged(nameof(DecimalDegreesValue));

                double absVal = Math.Abs(decVal);
                _dmsDegrees = (int)Math.Truncate(absVal);
                double minRemainder = (absVal - _dmsDegrees) * 60;
                _dmsMinutes = (int)Math.Truncate(minRemainder);
                _dmsSeconds = Math.Round((minRemainder - _dmsMinutes) * 60, 2);

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
                    _dmsDegrees = (int)deg;
                    _dmsMinutes = (int)min;
                    _dmsSeconds = Math.Round(sec, 2);
                    _decimalDegreesValue = deg + (min / 60.0) + (sec / 3600.0);

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
                _dmsDegrees = (int)Math.Truncate(_decimalDegreesValue);
                double minRemainder = (_decimalDegreesValue - _dmsDegrees) * 60;
                _dmsMinutes = (int)Math.Truncate(minRemainder);
                _dmsSeconds = Math.Round((minRemainder - _dmsMinutes) * 60, 2);

                OnPropertyChanged(nameof(DmsDegrees));
                OnPropertyChanged(nameof(DmsMinutes));
                OnPropertyChanged(nameof(DmsSeconds));

                _value = $"{_decimalDegreesValue:F5}° {Hemisphere}";
                OnPropertyChanged(nameof(Value));
            }
            else // DMS format
            {
                _decimalDegreesValue = _dmsDegrees + (_dmsMinutes / 60.0) + (_dmsSeconds / 3600.0);
                OnPropertyChanged(nameof(DecimalDegreesValue));

                _value = $"{_dmsDegrees}° {_dmsMinutes}' {_dmsSeconds:F2}\" {Hemisphere}";
                OnPropertyChanged(nameof(Value));
            }
        }
        catch { }
        finally
        {
            _isSyncing = false;
        }
    }
}
