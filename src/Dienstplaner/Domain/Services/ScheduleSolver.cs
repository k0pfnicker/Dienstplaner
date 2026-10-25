using Dienstplaner.Domain.Models;

namespace Dienstplaner.Domain.Services;

public class ScheduleSolver
{
    public void GenerateSchedule(MonthSchedule schedule)
    {
        var mainStaff = schedule.Employees.Where(e => e.IsMainStaff).OrderBy(e => e.Priority).ToList();
        var reliefStaff = schedule.Employees.Where(e => !e.IsMainStaff).OrderBy(e => e.Priority).ToList();

        // 1. Analyse der gesetzten Schichten & ersten Arbeitstage pro Mitarbeiter
        var firstWorkingDay = new Dictionary<string, int>();
        var lockedShifts = new Dictionary<(string empId, int day), ShiftType>();
        var unavailableDays = new HashSet<(string empId, int day)>();

        foreach (var emp in schedule.Employees)
        {
            int firstDay = int.MaxValue;
            for (int d = 1; d <= schedule.DaysInMonth; d++)
            {
                var cell = schedule.GetCell(emp.Id, d);
                if (cell.IsLocked)
                {
                    if (cell.ShiftType is ShiftType.S or ShiftType.N or ShiftType.T)
                    {
                        lockedShifts[(emp.Id, d)] = cell.ShiftType;
                        if (d < firstDay) firstDay = d;
                    }
                    else if (cell.ShiftType is ShiftType.U or ShiftType.FT or ShiftType.K)
                    {
                        unavailableDays.Add((emp.Id, d));
                    }
                }
            }
            if (firstDay != int.MaxValue)
            {
                firstWorkingDay[emp.Id] = firstDay;
            }
        }

        // 2. Zustandstracker für Rhythmus & Schichtblöcke
        var shiftCounts = schedule.Employees.ToDictionary(e => e.Id, _ => 0);
        var consecutiveDays = schedule.Employees.ToDictionary(e => e.Id, _ => 0);
        var lastShift = schedule.Employees.ToDictionary(e => e.Id, _ => ShiftType.None);
        var daysOffCount = schedule.Employees.ToDictionary(e => e.Id, _ => 0);

        // Bereits bestehende locked Schichten zählen
        foreach (var ((empId, _), shift) in lockedShifts)
        {
            shiftCounts[empId]++;
        }

        // 3. Tag für Tag dynamisch planen
        for (int day = 1; day <= schedule.DaysInMonth; day++)
        {
            var date = new DateTime(schedule.Year, schedule.Month, day);
            bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            bool isHoliday = schedule.Holidays.ContainsKey(day);
            bool needsT = isWeekend || isHoliday;

            var assignedEmpForShift = new Dictionary<ShiftType, string>();
            var dayAssignedStaff = new HashSet<string>();

            // Vorab manuell gesperrte Schichten für heute übernehmen
            foreach (var emp in schedule.Employees)
            {
                if (unavailableDays.Contains((emp.Id, day)))
                {
                    dayAssignedStaff.Add(emp.Id);
                }
                else if (lockedShifts.TryGetValue((emp.Id, day), out var lShift))
                {
                    assignedEmpForShift[lShift] = emp.Id;
                    dayAssignedStaff.Add(emp.Id);
                }
            }

            // Benötigte Schichten ermitteln
            var requiredShifts = new List<ShiftType>();
            if (!assignedEmpForShift.ContainsKey(ShiftType.S)) requiredShifts.Add(ShiftType.S);
            if (!assignedEmpForShift.ContainsKey(ShiftType.N)) requiredShifts.Add(ShiftType.N);
            if (needsT && !assignedEmpForShift.ContainsKey(ShiftType.T)) requiredShifts.Add(ShiftType.T);

            // An Wochenenden/Feiertagen: Tagschicht T bevorzugt mit Aushilfen besetzen
            if (requiredShifts.Contains(ShiftType.T))
            {
                var candidate = PickCandidate(
                    ShiftType.T, day, schedule, dayAssignedStaff,
                    firstWorkingDay, consecutiveDays, shiftCounts, lastShift,
                    mainStaff, reliefStaff, preferRelief: true);

                if (candidate != null)
                {
                    schedule.SetCode(candidate.Id, day, "T");
                    dayAssignedStaff.Add(candidate.Id);
                    shiftCounts[candidate.Id]++;
                    assignedEmpForShift[ShiftType.T] = candidate.Id;
                    requiredShifts.Remove(ShiftType.T);
                }
            }

            // Für die Hauptschichten S und N: 
            // Wenn ein Mitarbeiter gerade in einem S-Block ist, besetzen wir S mit ihm.
            // Wenn ein Mitarbeiter in einem N-Block ist, besetzen wir N mit ihm.
            // Reihenfolge: Zuerst die Schicht besetzen, für die ein Mitarbeiter im aktiven Rhythmus steht.
            var orderedShifts = OrderShiftsByActiveRhythm(requiredShifts, schedule.Employees, lastShift, consecutiveDays);

            foreach (var shift in orderedShifts)
            {
                var candidate = PickCandidate(
                    shift, day, schedule, dayAssignedStaff,
                    firstWorkingDay, consecutiveDays, shiftCounts, lastShift,
                    mainStaff, reliefStaff, preferRelief: false);

                if (candidate != null)
                {
                    schedule.SetCode(candidate.Id, day, shift.ToString());
                    dayAssignedStaff.Add(candidate.Id);
                    shiftCounts[candidate.Id]++;
                    assignedEmpForShift[shift] = candidate.Id;
                }
            }

            // Update Zustandstracker für den Tag
            foreach (var emp in schedule.Employees)
            {
                var cell = schedule.GetCell(emp.Id, day);
                if (cell.ShiftType is ShiftType.S or ShiftType.N or ShiftType.T)
                {
                    consecutiveDays[emp.Id]++;
                    lastShift[emp.Id] = cell.ShiftType;
                    daysOffCount[emp.Id] = 0;
                }
                else
                {
                    consecutiveDays[emp.Id] = 0;
                    lastShift[emp.Id] = ShiftType.None;
                    daysOffCount[emp.Id]++;
                }
            }
        }
    }

    private List<ShiftType> OrderShiftsByActiveRhythm(
        List<ShiftType> required, 
        IEnumerable<Employee> employees, 
        Dictionary<string, ShiftType> lastShift, 
        Dictionary<string, int> consecutiveDays)
    {
        // Wenn ein Stammmitarbeiter bereits in Schicht S läuft (Streak 1..4), Schicht S zuerst matchen
        bool sActive = employees.Any(e => e.IsMainStaff && lastShift[e.Id] == ShiftType.S && consecutiveDays[e.Id] is >= 1 and < 5);
        bool nActive = employees.Any(e => e.IsMainStaff && lastShift[e.Id] == ShiftType.N && consecutiveDays[e.Id] is >= 1 and < 5);

        if (sActive && !nActive)
            return required.OrderByDescending(s => s == ShiftType.S).ToList();
        if (nActive && !sActive)
            return required.OrderByDescending(s => s == ShiftType.N).ToList();

        return required;
    }

    private Employee? PickCandidate(
        ShiftType shiftToFill,
        int day,
        MonthSchedule schedule,
        HashSet<string> dayAssignedStaff,
        Dictionary<string, int> firstWorkingDay,
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

            // 1. DYNAMISCHE VORGABE: Erster Arbeitstag des Mitarbeiters
            // Wenn für diesen Mitarbeiter ein erster Arbeitstag weiter hinten im Monat festgelegt wurde (z.B. Tag 7),
            // dann wird er vor diesem Tag NICHT verplant!
            if (firstWorkingDay.TryGetValue(emp.Id, out int fDay) && day < fDay)
            {
                // Er hat noch keinen Dienstbeginn in diesem Monat
                continue;
            }

            // 2. HARTE RUHEZEIT-REGEL: Nach Nachtschicht (N) kein Frühdienst direkt (S oder T)
            if (day > 1)
            {
                var prevCell = schedule.GetCell(emp.Id, day - 1);
                if (prevCell.ShiftType == ShiftType.N && (shiftToFill is ShiftType.S or ShiftType.T))
                {
                    continue; // Mindestruhezeit verletzt!
                }
            }

            // 3. VORAUSSCHAUENDE REGEL (Look-Ahead):
            // Wenn morgen (Tag + 1) für diese Person fest S oder T eingetragen ist,
            // darf sie heute KEINESFALLS N machen!
            if (day < schedule.DaysInMonth)
            {
                var nextCell = schedule.GetCell(emp.Id, day + 1);
                if (nextCell.IsLocked && (nextCell.ShiftType is ShiftType.S or ShiftType.T) && shiftToFill == ShiftType.N)
                {
                    continue; // Verhindert Verletzung am nächsten Tag
                }
            }

            // 4. Maximal 5 bis 6 Tage am Stück
            if (consecutiveDays[emp.Id] >= 6)
            {
                continue;
            }

            // 5. Monats-Obergrenze für Stammkräfte (~23 Schichten = ~185h)
            if (emp.IsMainStaff && shiftCounts[emp.Id] >= 24)
            {
                continue;
            }

            // --- BEWERTUNG (SCORING) FÜR DYNAMISCHE BLOCK-KONTINUITÄT ---
            double score = 1000.0;

            // Prio-Bonus für Stammkräfte
            if (emp.IsMainStaff)
            {
                score += (10 - emp.Priority) * 40;
            }
            else
            {
                score += preferRelief ? 300 : 0;
            }

            // Dynamischer Block-Rhythmus:
            // Wenn der Mitarbeiter gestern dieselbe Schicht hatte, soll er sie heute weiterführen!
            if (lastShift[emp.Id] == shiftToFill)
            {
                // Hoher Bonus für Fortführung des Schichtblocks (z.B. 4 Tage S am Stück)
                score += 350;

                // Optimale Blocklänge ist 3 bis 5 Tage
                if (consecutiveDays[emp.Id] is >= 1 and <= 4)
                {
                    score += 150;
                }
                else if (consecutiveDays[emp.Id] >= 5)
                {
                    score -= 200; // Block ist voll, Zeit für Pause
                }
            }
            else if (lastShift[emp.Id] != ShiftType.None)
            {
                // Schichtwechsel mitten im Block ohne Ruhetag vermeiden (z.B. gestern S, heute N)
                score -= 250;
            }

            // Wenn der Mitarbeiter gerade aus Ruhetagen kommt (lastShift == None),
            // bevorzugt die Schicht nehmen, die im Monatsverlauf noch weniger gemacht wurde
            if (lastShift[emp.Id] == ShiftType.None && consecutiveDays[emp.Id] == 0)
            {
                score += 80;
            }

            // Ausgleich der Gesamt-Schichtanzahl
            score -= shiftCounts[emp.Id] * 20;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = emp;
            }
        }

        return bestCandidate;
    }
}
