using System.IO;
using Xunit;
using Dienstplaner.Domain.Models;
using Dienstplaner.Domain.Services;
using Dienstplaner.Infrastructure;

namespace Dienstplaner.Tests;

public class DienstplanerCoreTests
{
    [Fact]
    public void ShiftDefinition_TimesAndDurations_AreExact()
    {
        // Assert S: 00:00 - 08:15 (8:15)
        var sDuration = ShiftDefinition.GetDuration(ShiftType.S);
        Assert.Equal(TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(15)), sDuration);

        // Assert N: 16:00 - 00:00 (8:00)
        var nDuration = ShiftDefinition.GetDuration(ShiftType.N);
        Assert.Equal(TimeSpan.FromHours(8), nDuration);

        // Assert T: 07:15 - 16:00 (8:45)
        var tDuration = ShiftDefinition.GetDuration(ShiftType.T);
        Assert.Equal(TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(45)), tDuration);

        // Assert Ignored codes
        Assert.Equal(ShiftType.Ignored, ShiftDefinition.ParseCode("STR"));
        Assert.Equal(ShiftType.Ignored, ShiftDefinition.ParseCode("SO"));
        Assert.Equal(ShiftType.Ignored, ShiftDefinition.ParseCode("TS"));
    }

    [Fact]
    public void BavariaHolidayCalculator_IdentifiesOctober3rd()
    {
        var holidays = BavariaHolidayCalculator.GetHolidaysForMonth(2026, 10);
        Assert.True(holidays.ContainsKey(3));
        Assert.Equal("Tag der Deutschen Einheit", holidays[3]);
    }

    [Fact]
    public void ScheduleSolver_FillsAllRequiredShifts_AndEnforcesRestRule()
    {
        var schedule = new MonthSchedule(2026, 10);
        var solver = new ScheduleSolver();

        // Steffen hat Urlaub ab dem 16. bis 30.
        var steffen = schedule.Employees.First(e => e.Name.Contains("Steffen"));
        for (int d = 16; d <= 30; d++)
        {
            schedule.SetCode(steffen.Id, d, "U", isLocked: true);
        }

        solver.GenerateSchedule(schedule);

        // Prüfe jeden Tag
        for (int d = 1; d <= schedule.DaysInMonth; d++)
        {
            var cov = schedule.GetCoverage(d);

            // 1. S und N müssen IMMER vorhanden sein
            Assert.True(cov.HasS, $"Tag {d} hat keine Schicht S");
            Assert.True(cov.HasN, $"Tag {d} hat keine Schicht N");

            // 2. An Wochenenden und Feiertagen muss zusätzlich T vorhanden sein
            if (cov.NeedsDayShift)
            {
                Assert.True(cov.HasT, $"Tag {d} (WE/Feiertag) hat keine Schicht T");
            }

            // 3. Steffen darf an seinen Urlaubstagen keine Schicht haben
            if (d >= 16 && d <= 30)
            {
                var steffenCell = schedule.GetCell(steffen.Id, d);
                Assert.Equal("U", steffenCell.Code);
            }
        }

        // 4. Harte Regel: Nach Nachtschicht N (16:00-00:00) kein Frühdienst direkt (weder S noch T)
        foreach (var emp in schedule.Employees)
        {
            for (int d = 1; d < schedule.DaysInMonth; d++)
            {
                var today = schedule.GetCell(emp.Id, d);
                var tomorrow = schedule.GetCell(emp.Id, d + 1);

                if (today.ShiftType == ShiftType.N)
                {
                    Assert.NotEqual(ShiftType.S, tomorrow.ShiftType);
                    Assert.NotEqual(ShiftType.T, tomorrow.ShiftType);
                }
            }
        }
    }

    [Fact]
    public void ScheduleSolver_RespectsFirstWorkDaysAndBuildsCohesiveShiftBlocks()
    {
        var schedule = new MonthSchedule(2026, 10);
        var solver = new ScheduleSolver();

        var demir = schedule.Employees.First(e => e.Name.Contains("Demir"));
        var sitter = schedule.Employees.First(e => e.Name.Contains("Sitter"));

        // User-Szenario:
        // 1. Demir erster Arbeitstag ist Tag 2 mit Frühschicht (S)
        schedule.SetCode(demir.Id, 2, "S", isLocked: true);

        // 2. Sitter erster Arbeitstag ist Tag 7 mit Nachtschicht (N)
        schedule.SetCode(sitter.Id, 7, "N", isLocked: true);

        solver.GenerateSchedule(schedule);

        for (int d = 25; d <= schedule.DaysInMonth; d++)
        {
            var cov = schedule.GetCoverage(d);
            System.Console.WriteLine($"Oct Tag {d}: S={cov.HasS}, N={cov.HasN}, T={cov.HasT}");
            foreach (var emp in schedule.Employees)
            {
                var c = schedule.GetCell(emp.Id, d);
                if (!string.IsNullOrEmpty(c.Code))
                    System.Console.WriteLine($"   {emp.Name}: {c.Code}");
            }
        }

        // Prüfung 1: Demir darf an Tag 1 NICHT arbeiten (da sein erster Tag Tag 2 ist)
        var demirDay1 = schedule.GetCell(demir.Id, 1);
        Assert.True(string.IsNullOrEmpty(demirDay1.Code) || demirDay1.ShiftType == ShiftType.None || demirDay1.ShiftType == ShiftType.FT, 
            $"Demir sollte an Tag 1 vor seinem ersten Arbeitstag frei haben, hat aber {demirDay1.Code}");

        // Prüfung 2: Sitter darf an den Tagen 1 bis 6 NICHT arbeiten (da sein erster Tag Tag 7 ist)
        for (int d = 1; d <= 6; d++)
        {
            var sitterCell = schedule.GetCell(sitter.Id, d);
            Assert.True(string.IsNullOrEmpty(sitterCell.Code) || sitterCell.ShiftType == ShiftType.None || sitterCell.ShiftType == ShiftType.FT,
                $"Sitter sollte vor Tag 7 frei haben, hat aber an Tag {d} {sitterCell.Code}");
        }

        // Prüfung 3: Demir führt seinen S-Block ab Tag 2 fort (z.B. Tage 2, 3, 4 sind S!)
        var demirDay2 = schedule.GetCell(demir.Id, 2);
        var demirDay3 = schedule.GetCell(demir.Id, 3);
        var demirDay4 = schedule.GetCell(demir.Id, 4);
        Assert.Equal("S", demirDay2.Code);
        Assert.Equal("S", demirDay3.Code);
        Assert.Equal("S", demirDay4.Code);

        // Prüfung 4: Sitter führt seinen N-Block ab Tag 7 fort
        var sitterDay7 = schedule.GetCell(sitter.Id, 7);
        var sitterDay8 = schedule.GetCell(sitter.Id, 8);
        Assert.Equal("N", sitterDay7.Code);
        Assert.Equal("N", sitterDay8.Code);

        // Prüfung 5: Tag 1 ist trotzdem voll besetzt (durch Steffens / Aushilfen)
        var covDay1 = schedule.GetCoverage(1);
        Assert.True(covDay1.HasS);
        Assert.True(covDay1.HasN);
    }

    [Fact]
    public void CrossMonthContinuity_PrevMonthNightShift_BlocksDay1MorningShift()
    {
        // November 2026 planen
        var november = new MonthSchedule(2026, 11);
        var demir = november.Employees.First(e => e.Name.Contains("Demir"));

        // Demir hatte am 31. Oktober Nachtschicht (N bis 00:00 Uhr)
        november.PreviousMonthLastDayShifts[demir.Id] = ShiftType.N;

        var solver = new ScheduleSolver();
        solver.GenerateSchedule(november);

        // Am 1. November darf Demir KEINESFALLS Schicht S (00:00) oder T (07:15) haben!
        var demirNov1 = november.GetCell(demir.Id, 1);
        Assert.NotEqual(ShiftType.S, demirNov1.ShiftType);
        Assert.NotEqual(ShiftType.T, demirNov1.ShiftType);

        // Der 1. November muss trotzdem voll besetzt sein (durch andere Mitarbeiter)
        var covNov1 = november.GetCoverage(1);
        Assert.True(covNov1.HasS);
        Assert.True(covNov1.HasN);
    }

    [Fact]
    public void Exporters_PdfAndDoc_GenerateValidFiles()
    {
        var schedule = new MonthSchedule(2026, 10);
        var solver = new ScheduleSolver();
        solver.GenerateSchedule(schedule);

        for (int d = 25; d <= schedule.DaysInMonth; d++)
        {
            var cov = schedule.GetCoverage(d);
            System.Console.WriteLine($"Oct Tag {d}: S={cov.HasS}, N={cov.HasN}, T={cov.HasT}");
            foreach (var emp in schedule.Employees)
            {
                var c = schedule.GetCell(emp.Id, d);
                if (!string.IsNullOrEmpty(c.Code))
                    System.Console.WriteLine($"   {emp.Name}: {c.Code}");
            }
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "DienstplanerTests");
        Directory.CreateDirectory(tempDir);

        string pdfPath = Path.Combine(tempDir, "test_out.pdf");
        string docPath = Path.Combine(tempDir, "test_out.doc");
        string xlsxPath = Path.Combine(tempDir, "test_out.xlsx");

        var pdfExporter = new PdfScheduleExporter();
        pdfExporter.ExportToPdf(schedule, pdfPath);
        Assert.True(File.Exists(pdfPath));
        Assert.True(new FileInfo(pdfPath).Length > 1000);

        var docExporter = new DocScheduleExporter();
        docExporter.ExportToDoc(schedule, docPath);
        Assert.True(File.Exists(docPath));
        Assert.True(new FileInfo(docPath).Length > 1000);

        var excelExporter = new ExcelScheduleExporter();
        excelExporter.ExportToExcel(schedule, xlsxPath);
        Assert.True(File.Exists(xlsxPath));
        Assert.True(new FileInfo(xlsxPath).Length > 1000);

        // Aufräumen
        try { File.Delete(pdfPath); File.Delete(docPath); File.Delete(xlsxPath); } catch { }
    }

    [Fact]
    public void November2026_ReviewRequirements_SatisfiedFully()
    {
        // November 2026 planen
        var november = new MonthSchedule(2026, 11);
        var solver = new ScheduleSolver();
        solver.GenerateSchedule(november);

        var allStats = november.GetAllStats();

        // 1. Prio 1-3 (Demir, Sitter, Steffens) müssen mindestens 173 Std Minimum haben!
        var mainStaff = november.Employees.Where(e => e.IsMainStaff).ToList();
        Assert.Equal(3, mainStaff.Count);
        foreach (var emp in mainStaff)
        {
            var stats = allStats.First(s => s.Employee.Id == emp.Id);
            Assert.True(stats.TotalWorkedHours >= 173.0, 
                $"{emp.Name} hat nur {stats.TotalWorkedHours:F2}h, gefordert sind mindestens 173h!");
        }

        // 2. Hoffmanns reicht 1 Schicht egal wo
        var hoffmanns = november.Employees.FirstOrDefault(e => e.Name.Contains("Hoffmanns"));
        if (hoffmanns != null)
        {
            var hStats = allStats.First(s => s.Employee.Id == hoffmanns.Id);
            Assert.True(hStats.TotalShifts <= 1, 
                $"Hoffmanns sollte maximal 1 Schicht haben, hat aber {hStats.TotalShifts}");
        }

        // 3. Scheuerer auch weniger Schichten (1-2)
        var scheuerer = november.Employees.FirstOrDefault(e => e.Name.Contains("Scheuerer"));
        if (scheuerer != null)
        {
            var sStats = allStats.First(s => s.Employee.Id == scheuerer.Id);
            Assert.True(sStats.TotalShifts <= 2, 
                $"Scheuerer sollte maximal 2 Schichten haben, hat aber {sStats.TotalShifts}");
        }

        // 4. Saeed kann NUR am Wochenende oder an Feiertagen arbeiten
        var saeed = november.Employees.FirstOrDefault(e => e.Name.Contains("Saeed"));
        if (saeed != null)
        {
            for (int d = 1; d <= november.DaysInMonth; d++)
            {
                var cell = november.GetCell(saeed.Id, d);
                if (cell.ShiftType is ShiftType.S or ShiftType.N or ShiftType.T)
                {
                    bool isWeekend = cell.IsWeekend;
                    bool isHoliday = cell.IsHoliday;
                    Assert.True(isWeekend || isHoliday, 
                        $"Saeed darf nur am Wochenende/Feiertag arbeiten, wurde aber an Tag {d} ({cell.Date:dd.MM.yyyy, dddd}) eingeteilt!");
                }
            }
        }

        // 5. Aushilfen dürfen generell NUR T oder Spät (N) machen, NIEMALS S (Frühschicht)!
        var reliefStaff = november.Employees.Where(e => !e.IsMainStaff).ToList();
        foreach (var emp in reliefStaff)
        {
            for (int d = 1; d <= november.DaysInMonth; d++)
            {
                var cell = november.GetCell(emp.Id, d);
                Assert.NotEqual(ShiftType.S, cell.ShiftType);
            }
        }

        // 6. Volle Besetzung an jedem Tag
        for (int d = 1; d <= november.DaysInMonth; d++)
        {
            var cov = november.GetCoverage(d);
            Assert.True(cov.HasS, $"Tag {d} fehlt S");
            Assert.True(cov.HasN, $"Tag {d} fehlt N");
            if (cov.NeedsDayShift)
            {
                Assert.True(cov.HasT, $"Tag {d} (WE/Feiertag) fehlt T");
            }
        }

        // 7. Ruhezeitregel: Kein S oder T nach N
        foreach (var emp in november.Employees)
        {
            for (int d = 1; d < november.DaysInMonth; d++)
            {
                var today = november.GetCell(emp.Id, d);
                var tomorrow = november.GetCell(emp.Id, d + 1);
                if (today.ShiftType == ShiftType.N)
                {
                    Assert.NotEqual(ShiftType.S, tomorrow.ShiftType);
                    Assert.NotEqual(ShiftType.T, tomorrow.ShiftType);
                }
            }
        }
    }
}
