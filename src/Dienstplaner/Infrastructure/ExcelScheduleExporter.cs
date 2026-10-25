using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Dienstplaner.Domain.Models;

namespace Dienstplaner.Infrastructure;

public class ExcelScheduleExporter
{
    private static readonly string[] MonthNames = 
    {
        "", "Januar", "Februar", "März", "April", "Mai", "Juni", 
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    public void ExportToExcel(MonthSchedule schedule, string filePath)
    {
        string monthName = (schedule.Month >= 1 && schedule.Month <= 12) ? MonthNames[schedule.Month] : schedule.Month.ToString();
        var allStats = schedule.GetAllStats();
        var coverages = schedule.GetAllCoverages();

        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        var sheet = new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = $"{monthName}_{schedule.Year}"
        };
        sheets.Append(sheet);

        // 1. Titel-Zeile
        var titleRow = new Row();
        titleRow.Append(CreateTextCell($"Monatsplan Constantia Pirk — {monthName} {schedule.Year}"));
        sheetData.Append(titleRow);

        // Leerzeile
        sheetData.Append(new Row());

        // 2. Header-Zeilen (Wochentag & Tag)
        var weekdayRow = new Row();
        weekdayRow.Append(CreateTextCell("Wochentag"));
        for (int d = 1; d <= schedule.DaysInMonth; d++)
        {
            weekdayRow.Append(CreateTextCell(coverages[d - 1].WeekdayShort));
        }
        weekdayRow.Append(CreateTextCell(""));
        sheetData.Append(weekdayRow);

        var dateRow = new Row();
        dateRow.Append(CreateTextCell("Mitarbeiter / Tag"));
        for (int d = 1; d <= schedule.DaysInMonth; d++)
        {
            dateRow.Append(CreateNumberCell(d));
        }
        dateRow.Append(CreateTextCell("Gesamt-Std."));
        sheetData.Append(dateRow);

        // 3. Mitarbeiterzeilen
        foreach (var emp in schedule.Employees)
        {
            var empRow = new Row();
            empRow.Append(CreateTextCell($"{emp.Name} ({emp.RoleDisplay})"));

            for (int d = 1; d <= schedule.DaysInMonth; d++)
            {
                var cell = schedule.GetCell(emp.Id, d);
                string code = cell.DisplayCode == "-" ? "" : cell.DisplayCode;
                empRow.Append(CreateTextCell(code));
            }

            var stats = allStats.FirstOrDefault(s => s.Employee.Id == emp.Id);
            empRow.Append(CreateTextCell(stats != null ? stats.FormattedDuration : "0:00"));
            sheetData.Append(empRow);
        }

        // 4. Statuszeile
        var statusRow = new Row();
        statusRow.Append(CreateTextCell("Besetzung"));
        for (int d = 1; d <= schedule.DaysInMonth; d++)
        {
            statusRow.Append(CreateTextCell(coverages[d - 1].IsFullyCovered ? "OK" : "FEHLT"));
        }
        sheetData.Append(statusRow);

        workbookPart.Workbook.Save();
    }

    private Cell CreateTextCell(string text)
    {
        return new Cell
        {
            DataType = CellValues.String,
            CellValue = new CellValue(text)
        };
    }

    private Cell CreateNumberCell(int number)
    {
        return new Cell
        {
            DataType = CellValues.Number,
            CellValue = new CellValue(number.ToString())
        };
    }
}
