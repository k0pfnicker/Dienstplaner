namespace Dienstplanner.Domain.Models;

public record DayAssignment(
    DateTime Date,
    string Code,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    ShiftType Type,
    bool IsHoliday,
    bool IsPaddingDay = false
)
{
    public string StartTimeStr => IsPaddingDay ? "---" : (StartTime.HasValue ? StartTime.Value.ToString(@"hh\:mm") : string.Empty);
    public string EndTimeStr => IsPaddingDay ? "---" : (EndTime.HasValue ? EndTime.Value.ToString(@"hh\:mm") : string.Empty);
    public string TypeDisplay 
    {
        get 
        {
            if (IsPaddingDay) return "---";
            if (Type == ShiftType.FreiAntrag) return "Frei (Antrag)";
            return Type.ToString();
        }
    }
    public string CodeDisplay => IsPaddingDay ? "---" : Code;
    public string IsHolidayDisplay => IsPaddingDay ? "---" : (IsHoliday ? "Ja" : "");
    public string DayOfMonthStr => IsPaddingDay ? "-" : Date.Day.ToString();
    public bool IsToday => Date.Date == DateTime.Today;
    
    public TimeSpan Duration
    {
        get
        {
            if (IsPaddingDay || !StartTime.HasValue || !EndTime.HasValue) return TimeSpan.Zero;
            var duration = EndTime.Value - StartTime.Value;
            if (duration.TotalHours < 0) duration = duration.Add(TimeSpan.FromHours(24));
            return duration;
        }
    }

    public string DurationStr
    {
        get
        {
            if (IsPaddingDay || !StartTime.HasValue || !EndTime.HasValue) return "---";
            return Duration.ToString(@"hh\:mm");
        }
    }
}
