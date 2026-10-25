namespace Dienstplaner.Domain.Models;

public class DayCoverage
{
    public int Day { get; init; }
    public DateTime Date { get; init; }
    public bool IsWeekend => Date.DayOfWeek == DayOfWeek.Saturday || Date.DayOfWeek == DayOfWeek.Sunday;
    public bool IsHoliday { get; init; }
    public string HolidayName { get; init; } = string.Empty;

    public bool NeedsDayShift => IsWeekend || IsHoliday;

    public string? WorkerS { get; set; }
    public string? WorkerN { get; set; }
    public string? WorkerT { get; set; }

    public bool HasS => !string.IsNullOrEmpty(WorkerS);
    public bool HasN => !string.IsNullOrEmpty(WorkerN);
    public bool HasT => !string.IsNullOrEmpty(WorkerT);

    public bool IsFullyCovered => HasS && HasN && (!NeedsDayShift || HasT);

    public string StatusText
    {
        get
        {
            if (IsFullyCovered) return "Voll besetzt";
            var missing = new List<string>();
            if (!HasS) missing.Add("S fehlt");
            if (!HasN) missing.Add("N fehlt");
            if (NeedsDayShift && !HasT) missing.Add("T fehlt");
            return string.Join(", ", missing);
        }
    }

    public string StatusColor => IsFullyCovered ? "#2E7D32" : "#D32F2F";
    public string StatusBg => IsFullyCovered ? "#E8F5E9" : "#FFEBEE";

    public string WeekdayShort => Date.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
}
