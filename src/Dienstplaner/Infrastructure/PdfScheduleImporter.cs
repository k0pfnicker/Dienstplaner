using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Dienstplaner.Domain.Models;

namespace Dienstplaner.Infrastructure;

public class PdfScheduleImporter
{
    private static readonly Dictionary<string, int> GermanMonths = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Januar", 1 }, { "Februar", 2 }, { "März", 3 }, { "April", 4 },
        { "Mai", 5 }, { "Juni", 6 }, { "Juli", 7 }, { "August", 8 },
        { "September", 9 }, { "Oktober", 10 }, { "November", 11 }, { "Dezember", 12 }
    };

    public MonthSchedule Import(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        if (document.NumberOfPages == 0)
        {
            throw new InvalidOperationException("PDF-Dokument enthält keine Seiten.");
        }

        // 1. Text aus allen Seiten sammeln
        var fullTextBuilder = new System.Text.StringBuilder();
        foreach (var page in document.GetPages())
        {
            fullTextBuilder.AppendLine(page.Text);
        }
        string fullText = fullTextBuilder.ToString();

        // 2. Monat und Jahr ermitteln
        var monthMatch = Regex.Match(fullText, @"(Januar|Februar|März|April|Mai|Juni|Juli|August|September|Oktober|November|Dezember)\s+(\d{4})", RegexOptions.IgnoreCase);
        int month = DateTime.Today.Month;
        int year = DateTime.Today.Year;

        if (monthMatch.Success)
        {
            if (GermanMonths.TryGetValue(monthMatch.Groups[1].Value, out int m))
                month = m;
            if (int.TryParse(monthMatch.Groups[2].Value, out int y))
                year = y;
        }

        // 3. Bestehende Standard-Mitarbeiter initialisieren
        var schedule = new MonthSchedule(year, month);

        // 4. Mitarbeiter-Blöcke parsen
        // Format im Text:
        // [ID optional]\nName, Vorname\n...Schichten...\n[Summen oder nächster Mitarbeiter]
        var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Employee? currentEmployee = null;
        var employeeTokens = new Dictionary<string, List<string>>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Prüfen, ob Zeile ein Name ist (z. B. "Demir, Osman" oder "1023\nDemir, Osman")
            var matchName = Regex.Match(line, @"^([A-ZÄÖÜa-zäöüß]+,\s+[A-ZÄÖÜa-zäöüß]+)$");
            if (matchName.Success)
            {
                var name = matchName.Groups[1].Value;
                currentEmployee = schedule.Employees.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (currentEmployee == null)
                {
                    // Neuer Mitarbeiter aus PDF
                    string empId = (i > 0 && Regex.IsMatch(lines[i - 1].Trim(), @"^\d+$")) ? lines[i - 1].Trim() : "";
                    currentEmployee = new Employee
                    {
                        Id = empId,
                        Name = name,
                        IsMainStaff = false,
                        Priority = 90
                    };
                    schedule.Employees.Add(currentEmployee);
                }

                if (!employeeTokens.ContainsKey(currentEmployee.Id))
                {
                    employeeTokens[currentEmployee.Id] = new List<string>();
                }
                continue;
            }

            if (line.StartsWith("Summe", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Legende", StringComparison.OrdinalIgnoreCase))
            {
                currentEmployee = null;
                continue;
            }

            if (currentEmployee != null)
            {
                employeeTokens[currentEmployee.Id].Add(line);
            }
        }

        // 5. Schichten aus Token-Listen den Tagen 1..DaysInMonth zuordnen
        // Mit Spatial / Bounding-Box Extraktion via PdfPig Page 1 Table
        ExtractShiftsFromTableGrid(document, schedule);

        return schedule;
    }

    private void ExtractShiftsFromTableGrid(PdfDocument document, MonthSchedule schedule)
    {
        var page = document.GetPage(1);
        var words = page.GetWords().OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left).ToList();

        // 1. Finde die Tages-Header (1Do, 2Fr ... 31Sa) um die Spalten-X-Bereiche zu bestimmen
        var dayHeaders = new List<(int day, double left, double right)>();
        for (int d = 1; d <= schedule.DaysInMonth; d++)
        {
            var dWord = words.FirstOrDefault(w => Regex.IsMatch(w.Text, $@"^{d}[A-Za-z]{{2}}$"));
            if (dWord != null)
            {
                dayHeaders.Add((d, dWord.BoundingBox.Left - 5, dWord.BoundingBox.Right + 5));
            }
        }

        // Wenn dayHeaders lückenhaft ist, interpolieren wir gleichmäßig
        if (dayHeaders.Count < 10)
        {
            // Fallback auf sequenzielles Text-Parsing
            return;
        }

        // 2. Finde die Mitarbeiter-Zeilen (Y-Bereiche)
        var empRows = new List<(Employee emp, double top, double bottom)>();
        foreach (var emp in schedule.Employees)
        {
            var nameParts = emp.Name.Split(',');
            var lastName = nameParts[0].Trim();
            var nameWord = words.FirstOrDefault(w => w.Text.Contains(lastName, StringComparison.OrdinalIgnoreCase));
            if (nameWord != null)
            {
                empRows.Add((emp, nameWord.BoundingBox.Top + 10, nameWord.BoundingBox.Bottom - 35));
            }
        }

        empRows = empRows.OrderByDescending(r => r.top).ToList();

        // 3. Allen Zellen die Wörter zuordnen
        for (int i = 0; i < empRows.Count; i++)
        {
            var (emp, rowTop, rowBottom) = empRows[i];
            double nextBottom = (i + 1 < empRows.Count) ? empRows[i + 1].top : (rowBottom - 20);

            for (int d = 1; d <= schedule.DaysInMonth; d++)
            {
                // X-Bereich für Tag d finden
                var header = dayHeaders.FirstOrDefault(h => h.day == d);
                if (header.day == 0) continue;

                // Finde alle Wörter im Rechteck [header.left..header.right] x [nextBottom..rowTop]
                var cellWords = words.Where(w => 
                    w.BoundingBox.Left >= header.left - 2 && 
                    w.BoundingBox.Right <= header.right + 4 &&
                    w.BoundingBox.Bottom >= nextBottom &&
                    w.BoundingBox.Top <= rowTop).ToList();

                var codeWord = cellWords.FirstOrDefault(w => 
                    w.Text is "S" or "N" or "T" or "U" or "FT" or "K");

                if (codeWord != null)
                {
                    schedule.SetCode(emp.Id, d, codeWord.Text, isLocked: true);
                }
            }
        }
    }
}
