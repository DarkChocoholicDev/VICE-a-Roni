using Avalonia.Controls;
using Avalonia.Input;
using VICEaRoni.ViewModels;

namespace VICEaRoni.Views;

public partial class MainWindow : Window
{
    MainViewModel _viewModel = null!;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel = (MainViewModel)DataContext!;
    }
}