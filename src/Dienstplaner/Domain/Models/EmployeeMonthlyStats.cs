namespace Dienstplaner.Domain.Models;

public class EmployeeMonthlyStats
{
    public Employee Employee { get; init; } = new();
    public TimeSpan TotalWorkedDuration { get; set; } = TimeSpan.Zero;
    public double TotalWorkedHours => TotalWorkedDuration.TotalHours;
    public string FormattedDuration => $"{(int)TotalWorkedDuration.TotalHours}:{TotalWorkedDuration.Minutes:D2}";
    
    public int TotalShifts { get; set; }
    public int CountS { get; set; }
    public int CountN { get; set; }
    public int CountT { get; set; }
    public int CountUrlaub { get; set; }
    public int CountFrei { get; set; }
    public int CountKrank { get; set; }

    public double DifferenceHours => TotalWorkedHours - Employee.TargetHours;
    public string DifferenceHoursStr => DifferenceHours >= 0 ? $"+{DifferenceHours:F1}h" : $"{DifferenceHours:F1}h";
}
