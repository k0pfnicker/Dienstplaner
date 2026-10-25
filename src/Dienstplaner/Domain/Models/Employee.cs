namespace Dienstplaner.Domain.Models;

public class Employee
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsMainStaff { get; set; } = false;
    public int Priority { get; set; } = 99;
    public double TargetHours { get; set; } = 170.0;

    public string DisplayName => string.IsNullOrWhiteSpace(Id) ? Name : $"{Name} ({Id})";
    public string RoleDisplay => IsMainStaff ? $"Stamm (Prio {Priority})" : "Aushilfe";

    public static List<Employee> CreateDefaultTeam()
    {
        return new List<Employee>
        {
            new Employee { Id = "1023", Name = "Demir, Osman", IsMainStaff = true, Priority = 1, TargetHours = 174.0 },
            new Employee { Id = "194", Name = "Sitter, Martin", IsMainStaff = true, Priority = 2, TargetHours = 174.0 },
            new Employee { Id = "384", Name = "Steffens, Anton", IsMainStaff = true, Priority = 3, TargetHours = 174.0 },
            new Employee { Id = "140", Name = "Pfaffenzeller, Derek", IsMainStaff = false, Priority = 4, TargetHours = 0.0 },
            new Employee { Id = "168", Name = "Scheuerer, Martin", IsMainStaff = false, Priority = 5, TargetHours = 0.0 },
            new Employee { Id = "312", Name = "Saeed, Sawat", IsMainStaff = false, Priority = 6, TargetHours = 0.0 },
            new Employee { Id = "240", Name = "Hoffmanns, Christian", IsMainStaff = false, Priority = 7, TargetHours = 0.0 }
        };
    }
}
