using System.Collections.ObjectModel;
using System.Windows.Input;
using Dienstplanner.Application.Interfaces;
using Dienstplanner.Domain.Models;
using Microsoft.Win32;

namespace Dienstplanner.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IScheduleImporter _importer;
    
    private MonthSchedule? _currentSchedule;
    private PersonSchedule? _selectedPersonSchedule;
    private string _statusText = "Bereit.";

    public MainViewModel(IScheduleImporter importer)
    {
        _importer = importer;
        LoadCommand = new RelayCommand(async _ => await LoadScheduleAsync());
    }

    public ICommand LoadCommand { get; }

    public ObservableCollection<PersonSchedule> AvailablePersons { get; } = new();

    public ObservableCollection<string> DebugLogs { get; } = new();

    public string FullDebugLog => string.Join(Environment.NewLine, DebugLogs);

    public PersonSchedule? SelectedPersonSchedule
    {
        get => _selectedPersonSchedule;
        set 
        {
            if (SetProperty(ref _selectedPersonSchedule, value))
            {
                OnPropertyChanged(nameof(Assignments));
            }
        }
    }

    public IEnumerable<DayAssignment>? Assignments => SelectedPersonSchedule?.Assignments;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private async Task LoadScheduleAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Word Documents (*.docx;*.doc)|*.docx;*.doc|All files (*.*)|*.*",
            Title = "Dienstplan auswählen"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                StatusText = $"Lade {openFileDialog.FileName}...";
                DebugLogs.Clear();
                
                var result = await _importer.ImportAsync(openFileDialog.FileName);
                
                foreach (var log in result.DebugLogs)
                {
                    DebugLogs.Add(log);
                }
                OnPropertyChanged(nameof(FullDebugLog));

                if (result.IsSuccess)
                {
                    _currentSchedule = result.Schedule;
                    
                    AvailablePersons.Clear();
                    if (_currentSchedule?.PersonSchedules != null)
                    {
                        foreach (var person in _currentSchedule.PersonSchedules)
                        {
                            AvailablePersons.Add(person);
                        }
                    }
                    
                    SelectedPersonSchedule = AvailablePersons.FirstOrDefault(p => p.Name.Equals("Hr. Demir", StringComparison.OrdinalIgnoreCase)) ?? AvailablePersons.FirstOrDefault();
                    StatusText = $"Erfolgreich geladen: {_currentSchedule?.Month:D2}/{_currentSchedule?.Year} - {openFileDialog.FileName}";
                }
                else
                {
                    StatusText = $"Dienstplan konnte nicht eingelesen werden. Siehe Logs.";
                    _currentSchedule = null;
                    AvailablePersons.Clear();
                    SelectedPersonSchedule = null;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Unerwarteter Fehler beim Laden: {ex.Message}";
                DebugLogs.Add("Exception: " + ex.ToString());
                OnPropertyChanged(nameof(FullDebugLog));
                _currentSchedule = null;
                AvailablePersons.Clear();
                SelectedPersonSchedule = null;
            }
        }
    }
}
