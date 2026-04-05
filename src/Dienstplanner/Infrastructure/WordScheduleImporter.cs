using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Spire.Doc;
using Spire.Doc.Documents;
using Spire.Doc.Fields;
using Dienstplanner.Application.Interfaces;
using Dienstplanner.Domain.Models;
using Dienstplanner.Domain.Services;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;

namespace Dienstplanner.Infrastructure;

public class WordScheduleImporter : IScheduleImporter
{
    private readonly ShiftCodeInterpreter _interpreter;

    public WordScheduleImporter(ShiftCodeInterpreter interpreter)
    {
        _interpreter = interpreter;
    }

    public async Task<ImportResult> ImportAsync(string filePath)
    {
        return await Task.Run(() => ParseDocument(filePath));
    }

    private ImportResult ParseDocument(string filePath)
    {
        var logs = new List<string>();
        logs.Add($"Starte Import für Datei: {filePath}");
        
        try
        {
            List<List<List<string>>> tables;

            if (filePath.EndsWith(".doc", StringComparison.OrdinalIgnoreCase))
            {
                logs.Add("Verwende NPOI für altes .doc Format...");
                tables = ReadDocTables(filePath, logs);
            }
            else
            {
                logs.Add("Verwende OpenXML für .docx Format...");
                tables = ReadDocxTables(filePath, logs);
            }

            if (!tables.Any())
            {
                logs.Add("FEHLER: Das Dokument enthält keine Tabellen.");
                return new ImportResult { DebugLogs = logs };
            }

            for (int i = 0; i < tables.Count; i++)
            {
                logs.Add($"--- Überprüfe Tabelle {i + 1} ---");
                var schedule = TryParseTable(tables[i], logs);
                if (schedule != null)
                {
                    logs.Add($"ERFOLG: Gültiger Dienstplan in Tabelle {i + 1} gefunden und geparst.");
                    return new ImportResult { Schedule = schedule, DebugLogs = logs };
                }
            }

            logs.Add("FEHLER: In keiner Tabelle wurde ein gültiger Dienstplan gefunden (Kriterium: 'Monat:...' / 'Jahr:...' Zeile fehlt).");
            return new ImportResult { DebugLogs = logs };
        }
        catch (Exception ex)
        {
            logs.Add($"SCHWERWIEGENDER FEHLER beim Öffnen/Parsen: {ex.Message}");
            logs.Add(ex.StackTrace ?? "");
            return new ImportResult { DebugLogs = logs };
        }
    }

    private List<List<List<string>>> ReadDocTables(string filePath, List<string> logs)
    {
        var tables = new List<List<List<string>>>();
        try
        {
            var doc = new Spire.Doc.Document();
            doc.LoadFromFile(filePath);

            foreach (Section section in doc.Sections)
            {
                foreach (Spire.Doc.Table table in section.Tables)
                {
                    var tableRows = new List<List<string>>();
                    for (int r = 0; r < table.Rows.Count; r++)
                    {
                        var row = table.Rows[r];
                        var rowCells = new List<string>();
                        for (int c = 0; c < row.Cells.Count; c++)
                        {
                            var cell = row.Cells[c];
                            string text = "";
                            foreach (Spire.Doc.Documents.Paragraph p in cell.Paragraphs)
                            {
                                text += p.Text + " ";
                            }
                            rowCells.Add(text.Trim());
                        }
                        tableRows.Add(rowCells);
                    }
                    tables.Add(tableRows);
                }
            }
            logs.Add($"FreeSpire.Doc: {tables.Count} Tabelle(n) gefunden.");
        }
        catch (Exception ex)
        {
            logs.Add($"Fehler beim Lesen mit FreeSpire.Doc: {ex.Message}");
        }
        
        return tables;
    }

    private List<List<List<string>>> ReadDocxTables(string filePath, List<string> logs)
    {
        var tables = new List<List<List<string>>>();
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document.Body;

        if (body == null)
        {
            logs.Add("OpenXML: Body ist null.");
            return tables;
        }

        var openXmlTables = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().ToList();
        logs.Add($"OpenXML: {openXmlTables.Count} Tabelle(n) gefunden.");

        foreach (var table in openXmlTables)
        {
            var tableRows = new List<List<string>>();
            foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
            {
                var rowCells = new List<string>();
                foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                {
                    rowCells.Add(cell.InnerText ?? string.Empty);
                }
                tableRows.Add(rowCells);
            }
            tables.Add(tableRows);
        }
        return tables;
    }

    private MonthSchedule? TryParseTable(List<List<string>> rows, List<string> logs)
    {
        logs.Add($"Tabelle hat {rows.Count} Zeilen.");

        int startRowIdx = -1;
        int month = -1;
        int year = -1;

        // 1. Suche die Zeile mit Monat / Jahr
        for (int i = 0; i < rows.Count; i++)
        {
            var rowText = string.Join(" ", rows[i]);
            var monthMatch = Regex.Match(rowText, @"Monat:\s*([^\s]+)", RegexOptions.IgnoreCase);
            var yearMatch = Regex.Match(rowText, @"Jahr:\s*(\d{4})", RegexOptions.IgnoreCase);

            if (monthMatch.Success && yearMatch.Success)
            {
                startRowIdx = i;
                int.TryParse(yearMatch.Groups[1].Value, out year);
                var monthStr = monthMatch.Groups[1].Value;
                month = ParseMonth(monthStr);
                logs.Add($"Zeile {i + 1}: Monat ({monthStr} -> {month}) und Jahr ({year}) Header gefunden.");
                break;
            }
        }

        if (startRowIdx == -1 || month == -1 || year == -1)
        {
            logs.Add("Info: Tabelle enthält keinen 'Monat:' und 'Jahr:' Header, wird übersprungen.");
            return null; 
        }

        int endRowIdx = -1;
        var dateToColumnMap = new Dictionary<int, int>(); 
        var personSchedules = new List<PersonSchedule>();
        
        bool dateRowFound = false;

        for (int i = startRowIdx + 1; i < rows.Count; i++)
        {
            var cells = rows[i];
            
            if (!dateRowFound && cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (!dateRowFound)
            {
                var datesMatch = cells.Select((text, index) => (text: text.Trim(), index: index))
                                      .Where(x => int.TryParse(x.text, out int d) && d >= 1 && d <= 31)
                                      .ToList();

                if (datesMatch.Count > 20)
                {
                    dateRowFound = true;
                    logs.Add($"Zeile {i + 1}: Datums-Zeile gefunden. {datesMatch.Count} Tage gescannt.");
                    foreach (var d in datesMatch)
                    {
                        dateToColumnMap[int.Parse(d.text)] = d.index;
                    }
                }
                else
                {
                    logs.Add($"Zeile {i + 1}: Übersprungen (Keine Datums-Zeile). Inhalt: {string.Join(" | ", cells.Take(3))}");
                }
            }
            else
            {
                var name = cells.FirstOrDefault()?.Trim() ?? "";

                if (cells.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                int nonEmptyCells = cells.Count(c => !string.IsNullOrWhiteSpace(c));
                if ((nonEmptyCells == 1 && name.Length > 25 && !name.StartsWith("Aushilfen", StringComparison.OrdinalIgnoreCase)) || 
                    name.StartsWith("Feiertage", StringComparison.OrdinalIgnoreCase))
                {
                    endRowIdx = i - 1;
                    logs.Add($"Zeile {i + 1}: Text-Zeile oder 'Feiertage' gefunden. Markiert das Ende des Dienstplan-Blocks.");
                    break;
                }

                if (name.StartsWith("Aushilfen", StringComparison.OrdinalIgnoreCase))
                {
                    logs.Add($"Zeile {i + 1}: 'Aushilfen' Header gefunden.");
                    continue;
                }

                if (!string.IsNullOrEmpty(name) && name.ToLower() != "datum" && name.ToLower() != "wochentag" && name.ToLower() != "tag")
                {
                    var assignments = new List<DayAssignment>();
                    int daysInMonth = DateTime.DaysInMonth(year, month);
                    
                    int readCount = 0;
                    foreach (var kvp in dateToColumnMap)
                    {
                        int day = kvp.Key;
                        int colIdx = kvp.Value;
                        string code = colIdx < cells.Count ? cells[colIdx].Trim() : "";

                        if (day <= daysInMonth)
                        {
                            var date = new DateTime(year, month, day);
                            assignments.Add(_interpreter.Interpret(code, date, false));
                            readCount++;
                        }
                    }

                    if (assignments.Any())
                    {
                        personSchedules.Add(new PersonSchedule { Name = name, Assignments = assignments });
                        logs.Add($"Zeile {i + 1}: Person '{name}' mit {readCount} Tag(en) erfolgreich eingelesen.");
                    }
                    else
                    {
                        logs.Add($"Zeile {i + 1}: Person '{name}' gefunden, aber konnte keine Schichten/Zellen zuordnen.");
                    }
                }
            }
        }

        if (endRowIdx == -1)
        {
            endRowIdx = rows.Count;
            logs.Add("Warnung: Keine leere Zeile als Ende gefunden. Lese bis zum Tabellenende.");
        }

        var holidays = new List<Holiday>();
        int holidayColIdx = -1;
        
        logs.Add($"Suche ab Zeile {endRowIdx + 1} nach 'Feiertage:' Block.");

        for (int i = endRowIdx + 1; i < rows.Count; i++)
        {
            var cells = rows[i];
            
            if (holidayColIdx == -1)
            {
                var idx = cells.FindIndex(c => c.Trim().StartsWith("Feiertage", StringComparison.OrdinalIgnoreCase));
                if (idx != -1)
                {
                    holidayColIdx = idx;
                    logs.Add($"Zeile {i + 1}: Spalte {idx} als Basis für Feiertage identifiziert.");
                }
            }
            else
            {
                if (holidayColIdx < cells.Count)
                {
                    var cellText = cells[holidayColIdx].Trim();
                    if (!string.IsNullOrWhiteSpace(cellText))
                    {
                        // Nur Zeilen akzeptieren, die mit einem genauen Datum (dd.MM.yyyy) beginnen
                        var match = Regex.Match(cellText, @"^\s*(\d{1,2})\.(\d{1,2})\.(\d{4})\s+(.+)$");
                        if (match.Success)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int d) && 
                                int.TryParse(match.Groups[2].Value, out int m) &&
                                int.TryParse(match.Groups[3].Value, out int y))
                            {
                                try
                                {
                                    var holidayDate = new DateTime(y, m, d);
                                    var holidayName = match.Groups[4].Value.Trim();
                                    holidays.Add(new Holiday(holidayDate, holidayName));
                                    logs.Add($"Zeile {i + 1}: Feiertag erkannt -> {holidayDate:dd.MM.yyyy} ({holidayName})");
                                }
                                catch (Exception ex)
                                {
                                    logs.Add($"Zeile {i + 1}: Warnung - Fehler beim Parsen des Datums aus '{cellText}': {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            logs.Add($"Zeile {i + 1}: Warnung - Ignoriere Kommentar/Info-Zeile (kein genaues Datum 'dd.MM.yyyy Name'): '{cellText}'");
                        }
                    }
                }
            }
        }

        logs.Add($"Zweite Phase: Wende {holidays.Count} Feiertage auf bestehende Pläne an...");
        var holidayDates = new HashSet<DateTime>(holidays.Select(h => h.Date));
        
        var finalPersonSchedules = new List<PersonSchedule>();
        foreach (var ps in personSchedules)
        {
            var updatedAssignments = ps.Assignments.Select(a =>
            {
                bool isHoliday = holidayDates.Contains(a.Date);
                bool isTomorrowHoliday = holidayDates.Contains(a.Date.AddDays(1));
                if (a.Type != ShiftType.Frei)
                {
                   return _interpreter.Interpret(a.Code, a.Date, isHoliday, isTomorrowHoliday);
                }
                return a;
            }).OrderBy(a => a.Date).ToList();

            // Padding Days hinzufügen, wenn der 1. des Monats kein Montag ist
            var firstOfMonth = new DateTime(year, month, 1);
            int offset = (int)firstOfMonth.DayOfWeek - (int)DayOfWeek.Monday;
            if (offset < 0) offset += 7; // Falls Sonntag (0), Offset ist 6

            for (int d = 1; d <= offset; d++)
            {
                var padDate = firstOfMonth.AddDays(-d);
                // An den Anfang der Liste einfügen
                updatedAssignments.Insert(0, new DayAssignment(padDate, "", null, null, ShiftType.Frei, false, true));
            }

            finalPersonSchedules.Add(new PersonSchedule { Name = ps.Name, Assignments = updatedAssignments });
        }

        logs.Add($"Tabellen-Parse-Vorgang abgeschlossen. {finalPersonSchedules.Count} Personen geladen.");

        return new MonthSchedule
        {
            Month = month,
            Year = year,
            Holidays = holidays,
            PersonSchedules = finalPersonSchedules
        };
    }

    private int ParseMonth(string monthStr)
    {
        var lower = monthStr.ToLowerInvariant();
        if (lower.StartsWith("jan")) return 1;
        if (lower.StartsWith("feb")) return 2;
        if (lower.StartsWith("mär") || lower.StartsWith("mar")) return 3;
        if (lower.StartsWith("apr")) return 4;
        if (lower.StartsWith("mai") || lower.StartsWith("may")) return 5;
        if (lower.StartsWith("jun")) return 6;
        if (lower.StartsWith("jul")) return 7;
        if (lower.StartsWith("aug")) return 8;
        if (lower.StartsWith("sep")) return 9;
        if (lower.StartsWith("okt") || lower.StartsWith("oct")) return 10;
        if (lower.StartsWith("nov")) return 11;
        if (lower.StartsWith("dez") || lower.StartsWith("dec")) return 12;

        return 1; // Default
    }
}
