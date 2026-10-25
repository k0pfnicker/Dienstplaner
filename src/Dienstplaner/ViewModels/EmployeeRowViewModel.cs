using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dienstplaner.Domain.Models;

namespace Dienstplaner.ViewModels;

public class EmployeeRowViewModel : INotifyPropertyChanged
{
    public Employee Employee { get; }
    public ObservableCollection<DayCell> Cells { get; } = new();

    public string Name => Employee.Name;
    public string Role => Employee.RoleDisplay;
    public bool IsMainStaff => Employee.IsMainStaff;

    private string _totalDurationStr = "0:00";
    public string TotalDurationStr
    {
        get => _totalDurationStr;
        set
        {
            if (_totalDurationStr != value)
            {
                _totalDurationStr = value;
                OnPropertyChanged();
            }
        }
    }

    public EmployeeRowViewModel(Employee employee, IEnumerable<DayCell> cells)
    {
        Employee = employee;
        foreach (var c in cells)
        {
            Cells.Add(c);
            c.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DayCell.Code))
                {
                    UpdateTotalDuration();
                }
            };
        }
        UpdateTotalDuration();
    }

    public void UpdateTotalDuration()
    {
        TimeSpan total = TimeSpan.Zero;
        foreach (var cell in Cells)
        {
            total += cell.Duration;
        }
        TotalDurationStr = $"{(int)total.TotalHours}:{total.Minutes:D2}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
