using System.IO;
using System.Reflection;
using Spire.Doc;
using Spire.Doc.Documents;
using Dienstplaner.Domain.Models;

namespace Dienstplaner.Infrastructure;

public class DocScheduleExporter
{
    private static readonly string[] MonthNames = 
    {
        "", "Januar", "Februar", "März", "April", "Mai", "Juni", 
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    public void ExportToDoc(MonthSchedule schedule, string destinationPath)
    {
        // 1. Template laden
        var doc = new Document();
        string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Dienstplan Pforte blanko.doc");

        if (File.Exists(templatePath))
        {
            doc.LoadFromFile(templatePath);
        }
        else
        {
            // Aus eingebetteter Ressource laden
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Dienstplan Pforte blanko.doc"));
            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    doc.LoadFromStream(ms, FileFormat.Doc);
                }
            }
            else
            {
                throw new FileNotFoundException("Vorlage 'Dienstplan Pforte blanko.doc' wurde nicht gefunden.");
            }
        }

        // 2. Ersetze Monat und Jahr im Text
        string monthName = (schedule.Month >= 1 && schedule.Month <= 12) ? MonthNames[schedule.Month] : schedule.Month.ToString();
        doc.Replace("Monat:   Juni", $"Monat:   {monthName}", true, true);
        doc.Replace("Monat: Juni", $"Monat: {monthName}", true, true);
        doc.Replace("Jahr:", $"Jahr: {schedule.Year}", true, true);

        // 3. Tabellenzellen befüllen
        if (doc.Sections.Count > 0 && doc.Sections[0].Tables.Count > 0)
        {
            var table = doc.Sections[0].Tables[0] as Table;
            if (table != null)
            {
                FillTable(table, schedule);
            }
        }

        // 4. Als echtes .doc Format speichern
        doc.SaveToFile(destinationPath, FileFormat.Doc);
    }

    private void FillTable(Table table, MonthSchedule schedule)
    {
        int dateRowIdx = -1;
        int weekdayRowIdx = -1;

        // Finde Zeilen für Wochentag und Datum
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            if (row.Cells.Count > 0)
            {
                var txt = row.Cells[0].Paragraphs.Count > 0 ? row.Cells[0].Paragraphs[0].Text.Trim() : "";
                if (txt.Equals("Wochentag", StringComparison.OrdinalIgnoreCase)) weekdayRowIdx = r;
                if (txt.Equals("Datum", StringComparison.OrdinalIgnoreCase)) dateRowIdx = r;
            }
        }

        // Wochentage und Datum setzen
        if (dateRowIdx != -1 && weekdayRowIdx != -1)
        {
            for (int d = 1; d <= schedule.DaysInMonth; d++)
            {
                int colIdx = d; // Spalte 1 = Tag 1, etc.
                if (colIdx < table.Rows[dateRowIdx].Cells.Count)
                {
                    var date = new DateTime(schedule.Year, schedule.Month, d);
                    string weekday = date.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                    SetCellText(table.Rows[weekdayRowIdx].Cells[colIdx], weekday);
                    SetCellText(table.Rows[dateRowIdx].Cells[colIdx], d.ToString());
                }
            }
        }

        // Mitarbeiterzeilen suchen und befüllen
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
                        var cell = schedule.GetCell(matchingEmp.Id, d);
                        SetCellText(row.Cells[colIdx], cell.DisplayCode == "-" ? "" : cell.DisplayCode);
                    }
                }
            }
        }
    }

    private void SetCellText(TableCell cell, string text)
    {
        cell.Paragraphs.Clear();
        var p = cell.AddParagraph();
        p.Format.HorizontalAlignment = HorizontalAlignment.Center;
        var r = p.AppendText(text);
        r.CharacterFormat.FontSize = 8f;
    }
}
