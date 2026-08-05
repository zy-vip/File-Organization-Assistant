using System.Windows;
using FileTidy.App.ViewModels;

namespace FileTidy.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) => (DataContext as MainViewModel)?.Shutdown();
    }
}