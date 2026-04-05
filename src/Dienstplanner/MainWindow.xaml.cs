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

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        var infoWindow = new InfoWindow { Owner = this };
        infoWindow.ShowDialog();
    }
}
