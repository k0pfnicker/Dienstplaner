using Dienstplaner.Domain.Models;

namespace Dienstplaner.Domain.Services;

public class ScheduleSolver
{
    public void GenerateSchedule(MonthSchedule schedule)
    {
        var mainStaff = schedule.Employees.Where(e => e.IsMainStaff).OrderBy(e => e.Priority).ToList();
        var reliefStaff = schedule.Employees.Where(e => !e.IsMainStaff).OrderBy(e => e.Priority).ToList();

        // Stunden und Schicht-Tracker
        var shiftCounts = schedule.Employees.ToDictionary(e => e.Id, _ => 0);
        var consecutiveDays = schedule.Employees.ToDictionary(e => e.Id, _ => 0);
        var lastShift = schedule.Employees.ToDictionary(e => e.Id, _ => ShiftType.None);

        // Vorab bestehende feste Schichten (locked / U / FT) erfassen
        for (int day = 1; day <= schedule.DaysInMonth; day++)
        {
            foreach (var emp in schedule.Employees)
            {
                var cell = schedule.GetCell(emp.Id, day);
                if (cell.IsLocked && (cell.ShiftType == ShiftType.S || cell.ShiftType == ShiftType.N || cell.ShiftType == ShiftType.T))
                {
                    shiftCounts[emp.Id]++;
                }
            }
        }

        // Tag für Tag planen
        for (int day = 1; day <= schedule.DaysInMonth; day++)
        {
            var date = new DateTime(schedule.Year, schedule.Month, day);
            bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            bool isHoliday = schedule.Holidays.ContainsKey(day);
            bool needsT = isWeekend || isHoliday;

            // 1. Welche Schichten sind bereits manuell vergeben?
            var assignedEmpForShift = new Dictionary<ShiftType, string>();
            var dayAssignedStaff = new HashSet<string>();

            foreach (var emp in schedule.Employees)
            {
                var cell = schedule.GetCell(emp.Id, day);
                if (cell.IsLocked)
                {
                    if (cell.ShiftType == ShiftType.S || cell.ShiftType == ShiftType.N || cell.ShiftType == ShiftType.T)
                    {
                        assignedEmpForShift[cell.ShiftType] = emp.Id;
                        dayAssignedStaff.Add(emp.Id);
                    }
                    else if (cell.ShiftType == ShiftType.U || cell.ShiftType == ShiftType.FT || cell.ShiftType == ShiftType.K)
                    {
                        // Dieser Mitarbeiter steht heute definitiv nicht zur Verfügung
                        dayAssignedStaff.Add(emp.Id);
                    }
                }
            }

            // 2. Schichten, die heute besetzt werden müssen
            var requiredShifts = new List<ShiftType>();
            if (!assignedEmpForShift.ContainsKey(ShiftType.S)) requiredShifts.Add(ShiftType.S);
            if (!assignedEmpForShift.ContainsKey(ShiftType.N)) requiredShifts.Add(ShiftType.N);
            if (needsT && !assignedEmpForShift.ContainsKey(ShiftType.T)) requiredShifts.Add(ShiftType.T);

            // Zuerst Tagdienst T besetzen (an Wochenenden gerne Aushilfen oder Stammkraft im Rhythmus)
            if (requiredShifts.Contains(ShiftType.T))
            {
                var chosen = PickBestCandidate(ShiftType.T, day, schedule, dayAssignedStaff, consecutiveDays, shiftCounts, lastShift, mainStaff, reliefStaff, preferRelief: true);
                if (chosen != null)
                {
                    schedule.SetCode(chosen.Id, day, "T");
                    dayAssignedStaff.Add(chosen.Id);
                    shiftCounts[chosen.Id]++;
                    assignedEmpForShift[ShiftType.T] = chosen.Id;
                    requiredShifts.Remove(ShiftType.T);
                }
            }

            // Danach Nachtdienst N besetzen
            if (requiredShifts.Contains(ShiftType.N))
            {
                var chosen = PickBestCandidate(ShiftType.N, day, schedule, dayAssignedStaff, consecutiveDays, shiftCounts, lastShift, mainStaff, reliefStaff, preferRelief: false);
                if (chosen != null)
                {
                    schedule.SetCode(chosen.Id, day, "N");
                    dayAssignedStaff.Add(chosen.Id);
                    shiftCounts[chosen.Id]++;
                    assignedEmpForShift[ShiftType.N] = chosen.Id;
                    requiredShifts.Remove(ShiftType.N);
                }
            }

            // Zuletzt Früh/Spätdienst S besetzen
            if (requiredShifts.Contains(ShiftType.S))
            {
                var chosen = PickBestCandidate(ShiftType.S, day, schedule, dayAssignedStaff, consecutiveDays, shiftCounts, lastShift, mainStaff, reliefStaff, preferRelief: false);
                if (chosen != null)
                {
                    schedule.SetCode(chosen.Id, day, "S");
                    dayAssignedStaff.Add(chosen.Id);
                    shiftCounts[chosen.Id]++;
                    assignedEmpForShift[ShiftType.S] = chosen.Id;
                    requiredShifts.Remove(ShiftType.S);
                }
            }

            // Update Trackers für den Tag
            foreach (var emp in schedule.Employees)
            {
                var cell = schedule.GetCell(emp.Id, day);
                if (cell.ShiftType == ShiftType.S || cell.ShiftType == ShiftType.N || cell.ShiftType == ShiftType.T)
                {
                    consecutiveDays[emp.Id]++;
                    lastShift[emp.Id] = cell.ShiftType;
                }
                else
                {
                    consecutiveDays[emp.Id] = 0;
                    lastShift[emp.Id] = ShiftType.None;
                }
            }
        }
    }

    private Employee? PickBestCandidate(
        ShiftType shiftToFill,
        int day,
        MonthSchedule schedule,
        HashSet<string> dayAssignedStaff,
        Dictionary<string, int> consecutiveDays,
        Dictionary<string, int> shiftCounts,
        Dictionary<string, ShiftType> lastShift,
        List<Employee> mainStaff,
        List<Employee> reliefStaff,
        bool preferRelief)
    {
        var pool = preferRelief 
            ? reliefStaff.Concat(mainStaff).ToList() 
            : mainStaff.Concat(reliefStaff).ToList();

        Employee? bestCandidate = null;
        double bestScore = double.MinValue;

        foreach (var emp in pool)
        {
            if (dayAssignedStaff.Contains(emp.Id)) continue;

            var cell = schedule.GetCell(emp.Id, day);
            if (cell.IsLocked) continue;

            // HARTE REGEL: Nach Nachtschicht (N) kein Frühdienst direkt (S oder T)
            if (day > 1)
            {
                var prevCell = schedule.GetCell(emp.Id, day - 1);
                if (prevCell.ShiftType == ShiftType.N && (shiftToFill == ShiftType.S || shiftToFill == ShiftType.T))
                {
                    continue; // Ausgeschlossen! Mindestruhezeit verletzt.
                }
            }

            // Maximal 6 Tage am Stück
            if (consecutiveDays[emp.Id] >= 6)
            {
                continue;
            }

            // Zielstunden für Stammkräfte (~21-22 Schichten) nicht endlos überziehen
            if (emp.IsMainStaff && shiftCounts[emp.Id] >= 24)
            {
                continue;
            }

            // Scoring-Funktion
            double score = 1000.0;

            // Prioritätsbonus (Osman = 1, Sitter = 2, Steffen = 3)
            if (emp.IsMainStaff)
            {
                score += (10 - emp.Priority) * 50;
            }
            else
            {
                score += preferRelief ? 200 : 0;
            }

            // Weniger gearbeitet = höhere Chance (Ausgleich)
            score -= shiftCounts[emp.Id] * 15;

            // Schicht-Kontinuität (gleiche Schicht wie gestern wird bevorzugt)
            if (lastShift[emp.Id] == shiftToFill)
            {
                score += 40;
            }

            // Gesunde Streak-Länge (3-5 Tage sind optimal)
            if (consecutiveDays[emp.Id] >= 1 && consecutiveDays[emp.Id] <= 4)
            {
                score += 25;
            }
            else if (consecutiveDays[emp.Id] == 5)
            {
                score -= 50; // Bald Pause fällig
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = emp;
            }
        }

        return bestCandidate;
    }
}
