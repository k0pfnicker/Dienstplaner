using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Dienstplaner.Domain.Models;

namespace Dienstplaner.Infrastructure;

public class PdfScheduleExporter
{
    static PdfScheduleExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
    }

    private static readonly string[] MonthNames = 
    {
        "", "Januar", "Februar", "März", "April", "Mai", "Juni", 
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    public void ExportToPdf(MonthSchedule schedule, string destinationPath)
    {
        string monthName = (schedule.Month >= 1 && schedule.Month <= 12) ? MonthNames[schedule.Month] : schedule.Month.ToString();
        var allStats = schedule.GetAllStats();
        var coverages = schedule.GetAllCoverages();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(12);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text($"Monatsplan Constantia Pirk — {monthName} {schedule.Year}")
                                .FontSize(13).Bold().FontColor("#1A237E");
                            titleCol.Item().Text("Pfortendienst & Objektschutz | Erstellt mit Dienstplaner")
                                .FontSize(7).FontColor("#546E7A");
                        });

                        row.ConstantItem(180).AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
                            .FontSize(7).FontColor("#90A4AE");
                    });
                    col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor("#CFD8DC");
                });

                page.Content().PaddingTop(2).Table(table =>
                {
                    // Definition der Spalten: 1 Name-Spalte + 28..31 Tagesspalten + 1 Summenspalte
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(85); // Mitarbeiter
                        for (int i = 0; i < schedule.DaysInMonth; i++)
                        {
                            cols.RelativeColumn();
                        }
                        cols.ConstantColumn(40); // Summe
                    });

                    // 1. Header-Zeile: Wochentage & Datumszahlen
                    table.Header(header =>
                    {
                        header.Cell().Background("#ECEFF1").Border(0.5f).BorderColor("#B0BEC5").Padding(2)
                            .Text("Name / Tag").Bold().FontSize(6.5f).AlignCenter();

                        for (int d = 1; d <= schedule.DaysInMonth; d++)
                        {
                            var cov = coverages[d - 1];
                            string bg = cov.IsHoliday ? "#FFE0B2" : (cov.IsWeekend ? "#ECEFF1" : "#F5F5F5");

                            header.Cell().Background(bg).Border(0.5f).BorderColor("#B0BEC5").Padding(1).Column(c =>
                            {
                                c.Item().AlignCenter().Text(cov.WeekdayShort).FontSize(5.5f).Bold();
                                c.Item().AlignCenter().Text(d.ToString()).FontSize(6.5f).Bold();
                            });
                        }

                        header.Cell().Background("#ECEFF1").Border(0.5f).BorderColor("#B0BEC5").Padding(2)
                            .Text("Std.").Bold().FontSize(6.5f).AlignCenter();
                    });

                    // 2. Zeilen pro Mitarbeiter
                    foreach (var emp in schedule.Employees)
                    {
                        var stats = allStats.FirstOrDefault(s => s.Employee.Id == emp.Id);

                        // Mitarbeiter-Zelle
                        table.Cell().Background(emp.IsMainStaff ? "#FAFAFA" : "#FFFFFF")
                            .Border(0.5f).BorderColor("#CFD8DC").Padding(2).Column(c =>
                            {
                                c.Item().Text(emp.Name).Bold().FontSize(6.5f);
                                c.Item().Text(emp.RoleDisplay).FontSize(5f).FontColor("#78909C");
                            });

                        // Tagesspalten
                        for (int d = 1; d <= schedule.DaysInMonth; d++)
                        {
                            var cell = schedule.GetCell(emp.Id, d);
                            string cellBg = cell.ShiftType switch
                            {
                                ShiftType.S => "#BBDEFB",
                                ShiftType.N => "#E1BEE7",
                                ShiftType.T => "#FFE0B2",
                                ShiftType.U => "#C8E6C9",
                                ShiftType.FT => "#F5F5F5",
                                ShiftType.K => "#FFCDD2",
                                _ => cell.IsHoliday ? "#FFF8E1" : (cell.IsWeekend ? "#FAFAFA" : "#FFFFFF")
                            };

                            string textColor = cell.ShiftType switch
                            {
                                ShiftType.S => "#0D47A1",
                                ShiftType.N => "#4A148C",
                                ShiftType.T => "#E65100",
                                ShiftType.U => "#1B5E20",
                                ShiftType.FT => "#616161",
                                ShiftType.K => "#B71C1C",
                                _ => "#9E9E9E"
                            };

                            table.Cell().Background(cellBg).Border(0.5f).BorderColor("#CFD8DC")
                                .AlignCenter().AlignMiddle()
                                .Text(cell.DisplayCode == "-" ? "" : cell.DisplayCode)
                                .FontSize(7).Bold().FontColor(textColor);
                        }

                        // Summen-Zelle
                        string durationStr = stats != null ? stats.FormattedDuration : "0:00";
                        table.Cell().Background("#F5F5F5").Border(0.5f).BorderColor("#CFD8DC")
                            .AlignCenter().AlignMiddle().Text(durationStr).Bold().FontSize(6.5f);
                    }

                    // 3. Statuszeile (Besetzungsprüfung)
                    table.Cell().Background("#ECEFF1").Border(0.5f).BorderColor("#B0BEC5").Padding(2)
                        .Text("Besetzung").Bold().FontSize(6f).AlignCenter();

                    for (int d = 1; d <= schedule.DaysInMonth; d++)
                    {
                        var cov = coverages[d - 1];
                        string statusIcon = cov.IsFullyCovered ? "✔" : "!";
                        string statusBg = cov.IsFullyCovered ? "#E8F5E9" : "#FFEBEE";
                        string statusColor = cov.IsFullyCovered ? "#2E7D32" : "#C62828";

                        table.Cell().Background(statusBg).Border(0.5f).BorderColor("#B0BEC5")
                            .AlignCenter().AlignMiddle().Text(statusIcon).Bold().FontSize(7).FontColor(statusColor);
                    }

                    table.Cell().Background("#ECEFF1").Border(0.5f).BorderColor("#B0BEC5").Text("");
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor("#CFD8DC");
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(
                            "Legende: S: Früh/Spät 00:00-08:15 (8:15h) | N: Nacht 16:00-00:00 (8:00h) | T: Tag WE/Feiertag 07:15-16:00 (8:45h) | U: Urlaub | FT: Freier Tag"
                        ).FontSize(6).FontColor("#546E7A");

                        row.ConstantItem(100).AlignRight().Text(x =>
                        {
                            x.Span("Seite ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf(destinationPath);
    }
}
