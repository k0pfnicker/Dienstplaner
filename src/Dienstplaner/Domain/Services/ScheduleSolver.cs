using Dienstplaner.Domain.Models;

namespace Dienstplaner.Domain.Services;

public class ScheduleSolver
{
    private const int TargetMinutesPerMainStaff = 10380; // 173 Stunden * 60 Minuten

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

        // 2. Zustandstracker für Rhythmus, Schichtanzahl & Arbeitsminuten
        var shiftCounts = schedule.Employees.ToDictionary(e => e.Id, _ => 0);
        var workedMinutes = schedule.Employees.ToDictionary(e => e.Id, _ => 0);
        var consecutiveDays = schedule.Employees.ToDictionary(e => e.Id, _ => 0);
        var lastShift = schedule.Employees.ToDictionary(e => e.Id, _ => ShiftType.None);
        var daysOffCount = schedule.Employees.ToDictionary(e => e.Id, _ => 0);

        // Bereits bestehende locked Schichten zählen
        foreach (var ((empId, _), shift) in lockedShifts)
        {
            shiftCounts[empId]++;
            workedMinutes[empId] += GetShiftMinutes(shift);
        }

        // Monatsübergreifende Kontinuität: Schichten vom letzten Tag des Vormonats initialisieren
        foreach (var (empId, pShift) in schedule.PreviousMonthLastDayShifts)
        {
            if (pShift is ShiftType.S or ShiftType.N or ShiftType.T)
            {
                lastShift[empId] = pShift;
                consecutiveDays[empId] = 1;
            }
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

            // Reihenfolge: Schicht S IMMER zuerst besetzen (da nur Stammkräfte S machen dürfen!).
            // Danach N und T nach Rhythmus.
            var orderedShifts = requiredShifts
                .OrderByDescending(s => s == ShiftType.S)
                .ThenByDescending(s => s == ShiftType.N)
                .ThenByDescending(s => s == ShiftType.T)
                .ToList();

            foreach (var shift in orderedShifts)
            {
                var candidate = PickCandidate(
                    shift, day, schedule, dayAssignedStaff,
                    firstWorkingDay, consecutiveDays, shiftCounts, workedMinutes, lastShift,
                    mainStaff, reliefStaff, isWeekend, isHoliday);

                if (candidate != null)
                {
                    schedule.SetCode(candidate.Id, day, shift.ToString());
                    dayAssignedStaff.Add(candidate.Id);
                    shiftCounts[candidate.Id]++;
                    workedMinutes[candidate.Id] += GetShiftMinutes(shift);
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

        // 4. Zielstunden-Sicherung (173h Minimum für Prio 1-3)
        // Stellt sicher, dass Prio 1-3 die 173 Std Minimum erreichen, solange kein Urlaub/Sperre dies verhindert.
        EnforceMinimumHoursForMainStaff(schedule, mainStaff, reliefStaff, firstWorkingDay);
    }

    private Employee? PickCandidate(
        ShiftType shiftToFill,
        int day,
        MonthSchedule schedule,
        HashSet<string> dayAssignedStaff,
        Dictionary<string, int> firstWorkingDay,
        Dictionary<string, int> consecutiveDays,
        Dictionary<string, int> shiftCounts,
        Dictionary<string, int> workedMinutes,
        Dictionary<string, ShiftType> lastShift,
        List<Employee> mainStaff,
        List<Employee> reliefStaff,
        bool isWeekend,
        bool isHoliday)
    {
        // Pool: Prio 1-3 Stammkräfte zuerst, dann Aushilfen
        var pool = mainStaff.Concat(reliefStaff).ToList();

        Employee? bestCandidate = null;
        double bestScore = double.MinValue;

        foreach (var emp in pool)
        {
            if (dayAssignedStaff.Contains(emp.Id)) continue;

            var cell = schedule.GetCell(emp.Id, day);
            if (cell.IsLocked) continue;

            // 1. DYNAMISCHE VORGABE: Erster Arbeitstag des Mitarbeiters
            if (firstWorkingDay.TryGetValue(emp.Id, out int fDay) && day < fDay)
            {
                continue;
            }

            // 2. REGEL: Aushilfen dürfen bevorzugt NUR T oder N (Spät) machen!
            // Schicht S (00:00-08:15) ist Stammkräften vorbehalten; Aushilfen springen nur ein, 
            // wenn keine Stammkraft verfügbar ist (z.B. bei Urlaub/Ruhezeit).
            if (!emp.IsMainStaff && shiftToFill == ShiftType.S)
            {
                bool anyMainAvailableForS = mainStaff.Any(m => 
                    !dayAssignedStaff.Contains(m.Id) &&
                    !schedule.GetCell(m.Id, day).IsLocked &&
                    (!firstWorkingDay.TryGetValue(m.Id, out int fd) || day >= fd) &&
                    consecutiveDays[m.Id] < 6 &&
                    !(workedMinutes[m.Id] >= TargetMinutesPerMainStaff && shiftCounts[m.Id] >= 23) &&
                    !(day > 1 && schedule.GetCell(m.Id, day - 1).ShiftType == ShiftType.N) &&
                    !(day == 1 && schedule.PreviousMonthLastDayShifts.TryGetValue(m.Id, out var pm) && pm == ShiftType.N)
                );

                if (anyMainAvailableForS)
                {
                    continue;
                }
            }

            // 3. REGEL: Saeed kann NUR am Wochenende oder an Feiertagen arbeiten!
            // "nur der saaed muss wochenende weil anders ned geht bei ihm"
            if (!emp.IsMainStaff && emp.Name.Contains("Saeed", StringComparison.OrdinalIgnoreCase))
            {
                if (!isWeekend && !isHoliday)
                {
                    continue;
                }
            }

            // 4. REGEL: Hoffmanns reicht 1 Schicht egal wo
            // "HOffmanns reicht 1 Schicht egal wo."
            bool isHoffmannsCapped = !emp.IsMainStaff && emp.Name.Contains("Hoffmanns", StringComparison.OrdinalIgnoreCase) && shiftCounts[emp.Id] >= 1;

            // 5. REGEL: Scheuerer auch weniger Schichten (1-2)
            // "Scheurer auch weniger schichten 1-2"
            bool isScheuererCapped = !emp.IsMainStaff && emp.Name.Contains("Scheuerer", StringComparison.OrdinalIgnoreCase) && shiftCounts[emp.Id] >= 2;

            // 6. HARTE RUHEZEIT-REGEL: Nach Nachtschicht (N) kein Frühdienst direkt (S oder T)
            if (day > 1)
            {
                var prevCell = schedule.GetCell(emp.Id, day - 1);
                if (prevCell.ShiftType == ShiftType.N && (shiftToFill is ShiftType.S or ShiftType.T))
                {
                    continue;
                }
            }
            else if (day == 1)
            {
                if (schedule.PreviousMonthLastDayShifts.TryGetValue(emp.Id, out var prevMonthShift))
                {
                    if (prevMonthShift == ShiftType.N && (shiftToFill is ShiftType.S or ShiftType.T))
                    {
                        continue;
                    }
                }
            }

            // 7. VORAUSSCHAUENDE REGEL (Look-Ahead):
            // Wenn morgen (Tag + 1) für diese Person fest S oder T eingetragen ist, darf sie heute nicht N machen!
            if (day < schedule.DaysInMonth)
            {
                var nextCell = schedule.GetCell(emp.Id, day + 1);
                if (nextCell.IsLocked && (nextCell.ShiftType is ShiftType.S or ShiftType.T) && shiftToFill == ShiftType.N)
                {
                    continue;
                }
            }

            // 8. Maximal 5 bis 6 Tage am Stück
            if (consecutiveDays[emp.Id] >= 6)
            {
                continue;
            }

            // --- BEWERTUNG (SCORING) ---
            double score = 1000.0;

            // Soft Caps für Aushilfen (Hoffmanns <= 1, Scheuerer <= 2)
            if (isHoffmannsCapped)
            {
                score -= 8000.0; // Nur verplanen, wenn sonst keine andere Aushilfe / Stammkraft verfügbar ist
            }
            if (isScheuererCapped)
            {
                score -= 8000.0; // Nur verplanen, wenn sonst keine andere Aushilfe / Stammkraft verfügbar ist
            }

            if (emp.IsMainStaff)
            {
                int remainingMin = TargetMinutesPerMainStaff - workedMinutes[emp.Id];
                if (remainingMin > 0)
                {
                    // Hohe Priorität, um die 173 Stunden Minimum zu sichern
                    score += 2000.0 + (remainingMin / 60.0) * 30.0;
                }
                else
                {
                    // 173h bereits erreicht
                    score += 200.0;
                }

                // Obergrenze Stammkräfte: Wenn bereits >= 23 Schichten, nachrangig verplanen
                if (workedMinutes[emp.Id] >= TargetMinutesPerMainStaff && shiftCounts[emp.Id] >= 23)
                {
                    score -= 10000.0;
                }

                // Prio 1 (Demir) > Prio 2 (Sitter) > Prio 3 (Steffens)
                score += (10 - emp.Priority) * 80.0;
            }
            else
            {
                // Aushilfen
                score += 50.0;
                score -= shiftCounts[emp.Id] * 80.0;
            }

            // Dynamischer Block-Rhythmus:
            if (lastShift[emp.Id] == shiftToFill)
            {
                score += 600.0;

                // Wer bereits im N-Block ist, kann heute ohnehin weder S noch T machen.
                // Ihn auf N zu belassen verhindert, dass er den Tag verliert und eine Aushilfe gerufen werden muss!
                if (shiftToFill == ShiftType.N)
                {
                    score += 1200.0;
                }

                // Optimale Blocklänge 3 bis 5 Tage
                if (consecutiveDays[emp.Id] is >= 1 and <= 4)
                {
                    score += 300.0;
                }
                else if (consecutiveDays[emp.Id] >= 5)
                {
                    score -= 400.0; // Block voll, Zeit für Pause
                }
            }
            else if (lastShift[emp.Id] != ShiftType.None)
            {
                score -= 300.0;
            }

            if (lastShift[emp.Id] == ShiftType.None && consecutiveDays[emp.Id] == 0)
            {
                score += 100.0;
            }

            score -= shiftCounts[emp.Id] * 20.0;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = emp;
            }
        }

        return bestCandidate;
    }

    private void EnforceMinimumHoursForMainStaff(
        MonthSchedule schedule, 
        List<Employee> mainStaff, 
        List<Employee> reliefStaff,
        Dictionary<string, int> firstWorkingDay)
    {
        // Bis zu 5 Ausgleichsdurchläufe, um Prio 1-3 auf mindestens 173 Stunden zu heben
        for (int pass = 0; pass < 5; pass++)
        {
            bool anyChange = false;

            foreach (var mainEmp in mainStaff.OrderBy(e => e.Priority))
            {
                var stats = schedule.CalculateStats(mainEmp);
                int currentMinutes = (int)stats.TotalWorkedDuration.TotalMinutes;
                if (currentMinutes >= TargetMinutesPerMainStaff) continue;

                // Prüfen, ob der Mitarbeiter durch längere Abwesenheit (Urlaub/Krank) blockiert ist
                int lockedAbsenceDays = 0;
                for (int d = 1; d <= schedule.DaysInMonth; d++)
                {
                    var c = schedule.GetCell(mainEmp.Id, d);
                    if (c.IsLocked && c.ShiftType is ShiftType.U or ShiftType.K or ShiftType.FT)
                    {
                        lockedAbsenceDays++;
                    }
                }
                if (lockedAbsenceDays > 8) continue; // Bei >8 Urlaubstagen sind 173h rechnerisch nicht zwingend

                for (int day = 1; day <= schedule.DaysInMonth; day++)
                {
                    if (currentMinutes >= TargetMinutesPerMainStaff) break;

                    var myCell = schedule.GetCell(mainEmp.Id, day);
                    if (myCell.IsLocked || myCell.ShiftType != ShiftType.None && myCell.ShiftType != ShiftType.FT)
                    {
                        continue;
                    }

                    // Potenzielle Spender an Tag `day` suchen
                    var potentialDonors = reliefStaff
                        .Concat(mainStaff.Where(m => m.Id != mainEmp.Id))
                        .ToList();

                    foreach (var donor in potentialDonors)
                    {
                        var donorCell = schedule.GetCell(donor.Id, day);
                        if (donorCell.IsLocked) continue;
                        if (donorCell.ShiftType is not (ShiftType.S or ShiftType.N or ShiftType.T)) continue;

                        var shift = donorCell.ShiftType;

                        // Wenn Spender Stammkraft ist: Nur abgeben, wenn Spender danach immer noch >= 173h hat!
                        if (donor.IsMainStaff)
                        {
                            var donorStats = schedule.CalculateStats(donor);
                            int donorMinutes = (int)donorStats.TotalWorkedDuration.TotalMinutes;
                            int shiftMin = GetShiftMinutes(shift);
                            if (donorMinutes - shiftMin < TargetMinutesPerMainStaff)
                            {
                                continue;
                            }
                        }

                        // Kann mainEmp diesen Dienst an Tag `day` legal übernehmen?
                        if (CanTakeShift(schedule, mainEmp.Id, day, shift, firstWorkingDay))
                        {
                            schedule.SetCode(mainEmp.Id, day, shift.ToString());
                            schedule.SetCode(donor.Id, day, string.Empty);

                            anyChange = true;
                            currentMinutes += GetShiftMinutes(shift);
                            break;
                        }
                    }
                }
            }

            if (!anyChange) break;
        }
    }

    private static bool CanTakeShift(
        MonthSchedule schedule, 
        string empId, 
        int day, 
        ShiftType shift,
        Dictionary<string, int> firstWorkingDay)
    {
        var cell = schedule.GetCell(empId, day);
        if (cell.IsLocked) return false;

        // Erster geplanter Arbeitstag darf nicht unterlaufen werden
        if (firstWorkingDay.TryGetValue(empId, out int fDay) && day < fDay)
        {
            return false;
        }

        // 1. Vortag prüfen (kein S/T nach N)
        if (day > 1)
        {
            var prevCell = schedule.GetCell(empId, day - 1);
            if (prevCell.ShiftType == ShiftType.N && (shift is ShiftType.S or ShiftType.T))
            {
                return false;
            }
        }
        else
        {
            if (schedule.PreviousMonthLastDayShifts.TryGetValue(empId, out var prevShift) &&
                prevShift == ShiftType.N && (shift is ShiftType.S or ShiftType.T))
            {
                return false;
            }
        }

        // 2. Folgetag prüfen (kein N vor S/T)
        if (day < schedule.DaysInMonth)
        {
            var nextCell = schedule.GetCell(empId, day + 1);
            if (shift == ShiftType.N && (nextCell.ShiftType is ShiftType.S or ShiftType.T))
            {
                return false;
            }
        }

        // 3. Maximale zusammenhängende Arbeitstage (max. 6 Tage)
        int streakBefore = 0;
        for (int d = day - 1; d >= 1; d--)
        {
            var c = schedule.GetCell(empId, d);
            if (c.ShiftType is ShiftType.S or ShiftType.N or ShiftType.T)
                streakBefore++;
            else
                break;
        }

        int streakAfter = 0;
        for (int d = day + 1; d <= schedule.DaysInMonth; d++)
        {
            var c = schedule.GetCell(empId, d);
            if (c.ShiftType is ShiftType.S or ShiftType.N or ShiftType.T)
                streakAfter++;
            else
                break;
        }

        if (streakBefore + 1 + streakAfter > 6)
        {
            return false;
        }

        return true;
    }

    private static int GetShiftMinutes(ShiftType type) => type switch
    {
        ShiftType.S => 495, // 8h 15m
        ShiftType.N => 480, // 8h 00m
        ShiftType.T => 525, // 8h 45m
        _ => 0
    };
}
