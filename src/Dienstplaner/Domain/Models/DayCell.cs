using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dienstplaner.Domain.Models;

public class DayCell : INotifyPropertyChanged
{
    private string _code = string.Empty;
    private bool _isLocked = false;

    public string EmployeeId { get; init; } = string.Empty;
    public int Day { get; init; }
    public DateTime Date { get; init; }
    public bool IsWeekend => Date.DayOfWeek == DayOfWeek.Saturday || Date.DayOfWeek == DayOfWeek.Sunday;
    public bool IsHoliday { get; set; }
    public string HolidayName { get; set; } = string.Empty;

    public string Code
    {
        get => _code;
        set
        {
            var clean = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (_code != clean)
            {
                _code = clean;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShiftType));
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DurationStr));
                OnPropertyChanged(nameof(DisplayCode));
                OnPropertyChanged(nameof(TextColor));
                OnPropertyChanged(nameof(BgColor));
                OnPropertyChanged(nameof(TimeRangeStr));
            }
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked != value)
            {
                _isLocked = value;
                OnPropertyChanged();
            }
        }
    }

    public ShiftType ShiftType => ShiftDefinition.ParseCode(Code);

    public TimeSpan Duration => ShiftDefinition.GetDuration(ShiftType);

    public string DurationStr => Duration > TimeSpan.Zero ? Duration.ToString(@"hh\:mm") : "";

    public string DisplayCode => string.IsNullOrWhiteSpace(Code) ? "-" : Code;

    public string TimeRangeStr => ShiftDefinition.GetTimeRangeStr(ShiftType);

    public string TextColor => ShiftDefinition.GetColor(ShiftType);

    public string BgColor
    {
        get
        {
            if (ShiftType != ShiftType.None && ShiftType != ShiftType.Ignored)
            {
                return ShiftDefinition.GetBgColor(ShiftType);
            }
            if (IsHoliday) return "#FFF3E0";
            if (IsWeekend) return "#F5F5F5";
            return "White";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
