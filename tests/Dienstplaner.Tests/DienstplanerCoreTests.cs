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
    public void Exporters_PdfAndDoc_GenerateValidFiles()
    {
        var schedule = new MonthSchedule(2026, 10);
        var solver = new ScheduleSolver();
        solver.GenerateSchedule(schedule);

        string tempDir = Path.Combine(Path.GetTempPath(), "DienstplanerTests");
        Directory.CreateDirectory(tempDir);

        string pdfPath = Path.Combine(tempDir, "test_out.pdf");
        string docPath = Path.Combine(tempDir, "test_out.doc");

        var pdfExporter = new PdfScheduleExporter();
        pdfExporter.ExportToPdf(schedule, pdfPath);
        Assert.True(File.Exists(pdfPath));
        Assert.True(new FileInfo(pdfPath).Length > 1000);

        var docExporter = new DocScheduleExporter();
        docExporter.ExportToDoc(schedule, docPath);
        Assert.True(File.Exists(docPath));
        Assert.True(new FileInfo(docPath).Length > 1000);

        // Aufräumen
        try { File.Delete(pdfPath); File.Delete(docPath); } catch { }
    }
}
