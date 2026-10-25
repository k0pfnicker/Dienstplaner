using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Dienstplaner.Domain.Models;
using Dienstplaner.Domain.Services;
using Dienstplaner.Infrastructure;

namespace Dienstplaner.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ScheduleSolver _solver = new();
    private readonly PdfScheduleImporter _pdfImporter = new();
    private readonly WordScheduleImporter _wordImporter = new();
    private readonly PdfScheduleExporter _pdfExporter = new();
    private readonly DocScheduleExporter _docExporter = new();

    private MonthSchedule _schedule;
    private int _year;
    private int _month;
    private string _statusMessage = "Bereit.";

    public int Year
    {
        get => _year;
        set
        {
            if (SetProperty(ref _year, value))
            {
                OnPropertyChanged(nameof(MonthYearTitle));
                LoadSchedule(Year, Month);
            }
        }
    }

    public int Month
    {
        get => _month;
        set
        {
            if (SetProperty(ref _month, value))
            {
                OnPropertyChanged(nameof(MonthYearTitle));
                LoadSchedule(Year, Month);
            }
        }
    }

    public string MonthYearTitle
    {
        get
        {
            var date = new DateTime(Year, Month, 1);
            return date.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public MonthSchedule Schedule => _schedule;
    public ObservableCollection<EmployeeRowViewModel> EmployeeRows { get; } = new();
    public ObservableCollection<DayCoverage> DayHeaders { get; } = new();
    public ObservableCollection<EmployeeMonthlyStats> EmployeeStats { get; } = new();

    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand GeneratePlanCommand { get; }
    public ICommand ImportPdfCommand { get; }
    public ICommand ImportDocCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ExportDocCommand { get; }
    public ICommand ClearPlanCommand { get; }
    public ICommand CellClickCommand { get; }

    public MainViewModel()
    {
        _year = DateTime.Today.Year;
        _month = DateTime.Today.Month;
        _schedule = new MonthSchedule(_year, _month);

        PreviousMonthCommand = new RelayCommand(_ => ChangeMonth(-1));
        NextMonthCommand = new RelayCommand(_ => ChangeMonth(1));
        GeneratePlanCommand = new RelayCommand(_ => GeneratePlan());
        ImportPdfCommand = new RelayCommand(_ => ImportPdf());
        ImportDocCommand = new RelayCommand(_ => ImportDoc());
        ExportPdfCommand = new RelayCommand(_ => ExportPdf());
        ExportDocCommand = new RelayCommand(_ => ExportDoc());
        ClearPlanCommand = new RelayCommand(_ => ClearPlan());
        CellClickCommand = new RelayCommand(param => OnCellClicked(param as DayCell));

        LoadSchedule(_year, _month);
    }

    public void LoadSchedule(int year, int month, MonthSchedule? existingSchedule = null)
    {
        _schedule = existingSchedule ?? new MonthSchedule(year, month);
        _year = _schedule.Year;
        _month = _schedule.Month;

        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(Month));
        OnPropertyChanged(nameof(MonthYearTitle));

        RefreshUi();
        StatusMessage = $"Plan für {MonthYearTitle} geladen ({_schedule.DaysInMonth} Tage).";
    }

    public void RefreshUi()
    {
        // 1. Day Headers & Coverage
        DayHeaders.Clear();
        foreach (var cov in _schedule.GetAllCoverages())
        {
            DayHeaders.Add(cov);
        }

        // 2. Employee Rows
        EmployeeRows.Clear();
        foreach (var emp in _schedule.Employees)
        {
            var cells = new List<DayCell>();
            for (int d = 1; d <= _schedule.DaysInMonth; d++)
            {
                var cell = _schedule.GetCell(emp.Id, d);
                cells.Add(cell);
            }
            EmployeeRows.Add(new EmployeeRowViewModel(emp, cells));
        }

        // 3. Stats
        RefreshStats();
    }

    public void RefreshStats()
    {
        EmployeeStats.Clear();
        foreach (var stat in _schedule.GetAllStats())
        {
            EmployeeStats.Add(stat);
        }
    }

    private void ChangeMonth(int delta)
    {
        var dt = new DateTime(Year, Month, 1).AddMonths(delta);
        Year = dt.Year;
        Month = dt.Month;
    }

    private void GeneratePlan()
    {
        try
        {
            StatusMessage = "Generiere Dienstplan nach Prioritäten und Ruhezeitregeln...";
            _solver.GenerateSchedule(_schedule);
            RefreshUi();
            StatusMessage = "Dienstplan erfolgreich generiert! Alle Schichten wurden besetzt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Generieren: {ex.Message}";
            MessageBox.Show($"Fehler beim automatischen Erstellen des Dienstplans:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportPdf()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Dienstplan PDF (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*",
            Title = "Dienstplan-PDF auswählen"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Lese PDF-Dienstplan ein...";
                var imported = _pdfImporter.Import(ofd.FileName);
                LoadSchedule(imported.Year, imported.Month, imported);
                StatusMessage = $"PDF erfolgreich importiert: {Path.GetFileName(ofd.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim PDF-Import: {ex.Message}";
                MessageBox.Show($"Die PDF-Datei konnte nicht gelesen werden:\n{ex.Message}", "Importfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportDoc()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Word Dienstplan (*.doc;*.docx)|*.doc;*.docx|Alle Dateien (*.*)|*.*",
            Title = "Word-Dienstplan auswählen"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Lese Word-Dienstplan ein...";
                var imported = _wordImporter.Import(ofd.FileName);
                LoadSchedule(imported.Year, imported.Month, imported);
                StatusMessage = $"Word-Plan erfolgreich importiert: {Path.GetFileName(ofd.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Word-Import: {ex.Message}";
                MessageBox.Show($"Die Word-Datei konnte nicht gelesen werden:\n{ex.Message}", "Importfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportPdf()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "PDF Dokument (*.pdf)|*.pdf",
            FileName = $"Dienstplan_Pforte_{MonthYearTitle.Replace(" ", "_")}.pdf",
            Title = "Dienstplan als PDF exportieren"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Exportiere nach PDF...";
                _pdfExporter.ExportToPdf(_schedule, sfd.FileName);
                StatusMessage = $"Erfolgreich als PDF exportiert: {Path.GetFileName(sfd.FileName)}";
                MessageBox.Show($"Dienstplan erfolgreich exportiert:\n{sfd.FileName}", "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim PDF-Export: {ex.Message}";
                MessageBox.Show($"Fehler beim PDF-Export:\n{ex.Message}", "Exportfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportDoc()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Word 97-2003 Dokument (*.doc)|*.doc",
            FileName = $"Dienstplan_Pforte_{MonthYearTitle.Replace(" ", "_")}.doc",
            Title = "Dienstplan als Word DOC exportieren"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Exportiere nach Word DOC...";
                _docExporter.ExportToDoc(_schedule, sfd.FileName);
                StatusMessage = $"Erfolgreich als DOC exportiert: {Path.GetFileName(sfd.FileName)}";
                MessageBox.Show($"Dienstplan erfolgreich als .doc exportiert:\n{sfd.FileName}", "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim DOC-Export: {ex.Message}";
                MessageBox.Show($"Fehler beim DOC-Export:\n{ex.Message}", "Exportfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ClearPlan()
    {
        var res = MessageBox.Show("Möchten Sie alle nicht-gesperrten Schichten dieses Monats leeren?", "Plan leeren", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            for (int d = 1; d <= _schedule.DaysInMonth; d++)
            {
                foreach (var emp in _schedule.Employees)
                {
                    var cell = _schedule.GetCell(emp.Id, d);
                    if (!cell.IsLocked)
                    {
                        cell.Code = string.Empty;
                    }
                }
            }
            RefreshUi();
            StatusMessage = "Ungesperrte Schichten wurden zurückgesetzt.";
        }
    }

    public void OnCellClicked(DayCell? cell)
    {
        if (cell == null) return;

        // Durchklicken: Leer -> S -> N -> T -> U -> FT -> Leer
        string nextCode = cell.Code switch
        {
            "" => "S",
            "S" => "N",
            "N" => cell.IsWeekend || cell.IsHoliday ? "T" : "U",
            "T" => "U",
            "U" => "FT",
            "FT" => "K",
            "K" => "",
            _ => ""
        };

        cell.Code = nextCode;
        cell.IsLocked = !string.IsNullOrEmpty(nextCode); // Manuelle Eingabe sperrt die Zelle automatisch vor dem Solver

        // Update Header & Stats
        DayHeaders[cell.Day - 1] = _schedule.GetCoverage(cell.Day);
        RefreshStats();
    }

    public void SetCellManual(DayCell cell, string code)
    {
        cell.Code = code;
        cell.IsLocked = !string.IsNullOrEmpty(code);
        DayHeaders[cell.Day - 1] = _schedule.GetCoverage(cell.Day);
        RefreshStats();
    }
}
