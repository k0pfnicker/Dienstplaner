namespace Dienstplanner.Domain.Models;

public class ImportResult
{
    public MonthSchedule? Schedule { get; init; }
    public IReadOnlyList<string> DebugLogs { get; init; } = Array.Empty<string>();
    public bool IsSuccess => Schedule != null;
}
