using System.Windows;
using System.Windows.Controls;
using Dienstplaner.Domain.Models;
using Dienstplaner.ViewModels;

namespace Dienstplaner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private DayCell? GetContextCell(object sender)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
        {
            if (contextMenu.PlacementTarget is FrameworkElement element && element.DataContext is DayCell cell)
            {
                return cell;
            }
        }
        return null;
    }

    private void SetCellShift_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && DataContext is MainViewModel vm)
        {
            var cell = GetContextCell(sender);
            if (cell != null)
            {
                string shiftCode = menuItem.Tag?.ToString() ?? string.Empty;
                vm.SetCellManual(cell, shiftCode);
            }
        }
    }

    private void ToggleLock_Click(object sender, RoutedEventArgs e)
    {
        var cell = GetContextCell(sender);
        if (cell != null)
        {
            cell.IsLocked = !cell.IsLocked;
        }
    }
}
