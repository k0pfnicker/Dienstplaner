using System.IO;
using System.Text.Json;
using Dienstplaner.Domain.Models;

namespace Dienstplaner.Infrastructure;

public class ScheduleRepository
{
    private readonly string _storageDir;

    public ScheduleRepository()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _storageDir = Path.Combine(localAppData, "Dienstplaner", "schedules");
        Directory.CreateDirectory(_storageDir);
    }

    private string GetFilePath(int year, int month)
    {
        return Path.Combine(_storageDir, $"schedule_{year}_{month:D2}.json");
    }

    public void Save(MonthSchedule schedule)
    {
        var dto = new ScheduleDto
        {
            Year = schedule.Year,
            Month = schedule.Month,
            Cells = schedule.Cells.Values
                .Where(c => !string.IsNullOrEmpty(c.Code) || c.IsLocked)
                .Select(c => new CellDto
                {
                    EmployeeId = c.EmployeeId,
                    Day = c.Day,
                    Code = c.Code,
                    IsLocked = c.IsLocked
                }).ToList()
        };

        string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetFilePath(schedule.Year, schedule.Month), json);
    }

    public MonthSchedule? Load(int year, int month)
    {
        string path = GetFilePath(year, month);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<ScheduleDto>(json);
            if (dto == null) return null;

            var schedule = new MonthSchedule(dto.Year, dto.Month);
            foreach (var cellDto in dto.Cells)
            {
                schedule.SetCode(cellDto.EmployeeId, cellDto.Day, cellDto.Code, cellDto.IsLocked);
            }
            return schedule;
        }
        catch
        {
            return null;
        }
    }

    public Dictionary<string, ShiftType> GetLastDayShifts(int year, int month)
    {
        var result = new Dictionary<string, ShiftType>();
        var schedule = Load(year, month);
        if (schedule == null) return result;

        int lastDay = schedule.DaysInMonth;
        foreach (var emp in schedule.Employees)
        {
            var cell = schedule.GetCell(emp.Id, lastDay);
            if (cell.ShiftType is ShiftType.S or ShiftType.N or ShiftType.T)
            {
                result[emp.Id] = cell.ShiftType;
            }
        }

        return result;
    }

    private class ScheduleDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<CellDto> Cells { get; set; } = new();
    }

    private class CellDto
    {
        public string EmployeeId { get; set; } = string.Empty;
        public int Day { get; set; }
        public string Code { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
    }
}
