using System.Collections.ObjectModel;
using Dienstplaner.Domain.Services;

namespace Dienstplaner.Domain.Models;

public class MonthSchedule
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    public ObservableCollection<Employee> Employees { get; } = new();
    public Dictionary<int, string> Holidays { get; set; } = new();
    public Dictionary<(string employeeId, int day), DayCell> Cells { get; } = new();

    public MonthSchedule(int year, int month, IEnumerable<Employee>? employees = null)
    {
        Year = year;
        Month = month;
        Holidays = BavariaHolidayCalculator.GetHolidaysForMonth(year, month);

        var team = employees?.ToList() ?? Employee.CreateDefaultTeam();
        foreach (var emp in team)
        {
            Employees.Add(emp);
        }

        InitializeCells();
    }

    public void InitializeCells()
    {
        Cells.Clear();
        for (int day = 1; day <= DaysInMonth; day++)
        {
            var date = new DateTime(Year, Month, day);
            bool isHoliday = Holidays.ContainsKey(day);
            string holidayName = isHoliday ? Holidays[day] : string.Empty;

            foreach (var emp in Employees)
            {
                var cell = new DayCell
                {
                    EmployeeId = emp.Id,
                    Day = day,
                    Date = date,
                    IsHoliday = isHoliday,
                    HolidayName = holidayName,
                    Code = string.Empty
                };
                Cells[(emp.Id, day)] = cell;
            }
        }
    }

    public DayCell GetCell(string employeeId, int day)
    {
        if (Cells.TryGetValue((employeeId, day), out var cell))
        {
            return cell;
        }

        var date = new DateTime(Year, Month, day);
        bool isHoliday = Holidays.ContainsKey(day);
        cell = new DayCell
        {
            EmployeeId = employeeId,
            Day = day,
            Date = date,
            IsHoliday = isHoliday,
            HolidayName = isHoliday ? Holidays[day] : "",
            Code = string.Empty
        };
        Cells[(employeeId, day)] = cell;
        return cell;
    }

    public void SetCode(string employeeId, int day, string code, bool isLocked = false)
    {
        var cell = GetCell(employeeId, day);
        cell.Code = code;
        if (isLocked)
        {
            cell.IsLocked = true;
        }
    }

    public DayCoverage GetCoverage(int day)
    {
        var date = new DateTime(Year, Month, day);
        bool isHoliday = Holidays.ContainsKey(day);
        string holidayName = isHoliday ? Holidays[day] : "";

        var cov = new DayCoverage
        {
            Day = day,
            Date = date,
            IsHoliday = isHoliday,
            HolidayName = holidayName
        };

        foreach (var emp in Employees)
        {
            var cell = GetCell(emp.Id, day);
            if (cell.ShiftType == ShiftType.S && string.IsNullOrEmpty(cov.WorkerS))
                cov.WorkerS = emp.Name;
            else if (cell.ShiftType == ShiftType.N && string.IsNullOrEmpty(cov.WorkerN))
                cov.WorkerN = emp.Name;
            else if (cell.ShiftType == ShiftType.T && string.IsNullOrEmpty(cov.WorkerT))
                cov.WorkerT = emp.Name;
        }

        return cov;
    }

    public List<DayCoverage> GetAllCoverages()
    {
        var list = new List<DayCoverage>();
        for (int d = 1; d <= DaysInMonth; d++)
        {
            list.Add(GetCoverage(d));
        }
        return list;
    }

    public EmployeeMonthlyStats CalculateStats(Employee employee)
    {
        var stats = new EmployeeMonthlyStats { Employee = employee };
        TimeSpan total = TimeSpan.Zero;

        for (int day = 1; day <= DaysInMonth; day++)
        {
            var cell = GetCell(employee.Id, day);
            switch (cell.ShiftType)
            {
                case ShiftType.S:
                    stats.CountS++;
                    stats.TotalShifts++;
                    total += cell.Duration;
                    break;
                case ShiftType.N:
                    stats.CountN++;
                    stats.TotalShifts++;
                    total += cell.Duration;
                    break;
                case ShiftType.T:
                    stats.CountT++;
                    stats.TotalShifts++;
                    total += cell.Duration;
                    break;
                case ShiftType.U:
                    stats.CountUrlaub++;
                    break;
                case ShiftType.FT:
                    stats.CountFrei++;
                    break;
                case ShiftType.K:
                    stats.CountKrank++;
                    break;
            }
        }

        stats.TotalWorkedDuration = total;
        return stats;
    }

    public List<EmployeeMonthlyStats> GetAllStats()
    {
        return Employees.Select(CalculateStats).ToList();
    }
}
