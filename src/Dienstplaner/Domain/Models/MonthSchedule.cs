namespace Dienstplaner.Domain.Models;

public class MonthSchedule
{
    public int Month { get; init; }
    public int Year { get; init; }
    public IReadOnlyList<Holiday> Holidays { get; init; } = Array.Empty<Holiday>();
    public IReadOnlyList<PersonSchedule> PersonSchedules { get; init; } = Array.Empty<PersonSchedule>();
}
