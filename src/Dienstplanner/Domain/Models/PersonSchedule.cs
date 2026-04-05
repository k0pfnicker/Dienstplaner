namespace Dienstplanner.Domain.Models;

public class PersonSchedule
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<DayAssignment> Assignments { get; init; } = Array.Empty<DayAssignment>();
}
