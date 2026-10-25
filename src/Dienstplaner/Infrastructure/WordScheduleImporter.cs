using System.Text.RegularExpressions;
using Spire.Doc;
using Spire.Doc.Documents;
using Dienstplaner.Domain.Models;

namespace Dienstplaner.Infrastructure;

public class WordScheduleImporter
{
    public MonthSchedule Import(string filePath)
    {
        var doc = new Document();
        doc.LoadFromFile(filePath);

        if (doc.Sections.Count == 0 || doc.Sections[0].Tables.Count == 0)
        {
            throw new InvalidOperationException("Das Word-Dokument enthält keine Tabelle.");
        }

        var table = doc.Sections[0].Tables[0] as Table;
        if (table == null)
        {
            throw new InvalidOperationException("Keine gültige Tabelle gefunden.");
        }

        // 1. Suche nach Monat & Jahr
        int month = DateTime.Today.Month;
        int year = DateTime.Today.Year;

        for (int r = 0; r < table.Rows.Count; r++)
        {
            for (int c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                var txt = table.Rows[r].Cells[c].Paragraphs.Count > 0 ? table.Rows[r].Cells[c].Paragraphs[0].Text : "";
                var mMatch = Regex.Match(txt, @"Monat:\s*([A-Za-zäöüÄÖÜ]+)", RegexOptions.IgnoreCase);
                if (mMatch.Success)
                {
                    month = ParseMonth(mMatch.Groups[1].Value);
                }
                var yMatch = Regex.Match(txt, @"Jahr:\s*(\d{4})", RegexOptions.IgnoreCase);
                if (yMatch.Success && int.TryParse(yMatch.Groups[1].Value, out int y))
                {
                    year = y;
                }
            }
        }

        var schedule = new MonthSchedule(year, month);

        // 2. Finde Datumszeile
        int dateRowIdx = -1;
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var txt = table.Rows[r].Cells.Count > 0 && table.Rows[r].Cells[0].Paragraphs.Count > 0
                ? table.Rows[r].Cells[0].Paragraphs[0].Text.Trim() : "";
            if (txt.Equals("Datum", StringComparison.OrdinalIgnoreCase))
            {
                dateRowIdx = r;
                break;
            }
        }

        // 3. Mitarbeiterzeilen auslesen
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            if (row.Cells.Count == 0) continue;

            string headerText = row.Cells[0].Paragraphs.Count > 0 ? row.Cells[0].Paragraphs[0].Text.Trim() : "";
            Employee? matchingEmp = null;

            if (headerText.Contains("Sitter", StringComparison.OrdinalIgnoreCase))
                matchingEmp = schedule.Employees.FirstOrDefault(e => e.Name.Contains("Sitter", StringComparison.OrdinalIgnoreCase));
            else if (headerText.Contains("Demir", StringComparison.OrdinalIgnoreCase))
                matchingEmp = schedule.Employees.FirstOrDefault(e => e.Name.Contains("Demir", StringComparison.OrdinalIgnoreCase));
            else if (headerText.Contains("Steffens", StringComparison.OrdinalIgnoreCase) || headerText.Contains("Steffen", StringComparison.OrdinalIgnoreCase))
                matchingEmp = schedule.Employees.FirstOrDefault(e => e.Name.Contains("Steffen", StringComparison.OrdinalIgnoreCase));
            else if (headerText.Contains("Pfaffenzeller", StringComparison.OrdinalIgnoreCase))
                matchingEmp = schedule.Employees.FirstOrDefault(e => e.Name.Contains("Pfaffenzeller", StringComparison.OrdinalIgnoreCase));
            else if (headerText.Contains("Scheuerer", StringComparison.OrdinalIgnoreCase))
                matchingEmp = schedule.Employees.FirstOrDefault(e => e.Name.Contains("Scheuerer", StringComparison.OrdinalIgnoreCase));

            if (matchingEmp != null)
            {
                for (int d = 1; d <= schedule.DaysInMonth; d++)
                {
                    int colIdx = d;
                    if (colIdx < row.Cells.Count)
                    {
                        var cellText = row.Cells[colIdx].Paragraphs.Count > 0 ? row.Cells[colIdx].Paragraphs[0].Text.Trim() : "";
                        if (!string.IsNullOrEmpty(cellText))
                        {
                            var parsedType = ShiftDefinition.ParseCode(cellText);
                            if (parsedType != ShiftType.None && parsedType != ShiftType.Ignored)
                            {
                                schedule.SetCode(matchingEmp.Id, d, parsedType.ToString(), isLocked: true);
                            }
                        }
                    }
                }
            }
        }

        return schedule;
    }

    private static int ParseMonth(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "januar" => 1,
            "februar" => 2,
            "märz" => 3,
            "april" => 4,
            "mai" => 5,
            "juni" => 6,
            "juli" => 7,
            "august" => 8,
            "september" => 9,
            "oktober" => 10,
            "november" => 11,
            "dezember" => 12,
            _ => DateTime.Today.Month
        };
    }
}
