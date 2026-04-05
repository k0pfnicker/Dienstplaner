using System.Linq;

namespace Dienstplanner.Domain.Models;

public class PersonSchedule
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<DayAssignment> Assignments { get; init; } = Array.Empty<DayAssignment>();

    public TimeSpan TotalWorkDuration => new TimeSpan(Assignments.Where(a => !a.IsPaddingDay && a.Type == ShiftType.Arbeit).Sum(a => a.Duration.Ticks));
    public double TotalWorkHours => TotalWorkDuration.TotalHours;
    public int KrankDays => Assignments.Count(a => !a.IsPaddingDay && a.Type == ShiftType.Krank);
    public int UrlaubDays => Assignments.Count(a => !a.IsPaddingDay && a.Type == ShiftType.Urlaub);
    public int FeiertagDays => Assignments.Count(a => !a.IsPaddingDay && a.Type == ShiftType.Feiertag);
    public int FreiDays => Assignments.Count(a => !a.IsPaddingDay && (a.Type == ShiftType.Frei || a.Type == ShiftType.FreiAntrag));
}
