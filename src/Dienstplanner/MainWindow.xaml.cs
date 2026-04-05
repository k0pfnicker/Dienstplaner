using System.Windows;
using Dienstplanner.ViewModels;

namespace Dienstplanner;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
