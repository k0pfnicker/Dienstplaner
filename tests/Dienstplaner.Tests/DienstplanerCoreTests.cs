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
