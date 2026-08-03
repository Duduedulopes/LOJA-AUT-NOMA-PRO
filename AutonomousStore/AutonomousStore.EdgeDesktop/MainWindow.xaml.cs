using System.Windows;
using AutonomousStore.EdgeDesktop.ViewModels;

namespace AutonomousStore.EdgeDesktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadProductsCommand.ExecuteAsync(null);
    }
}
