using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VICEaRoni.ViewModels;

namespace VICEaRoni.Views
{
    public partial class ConsoleView: UserControl
    {
        ConsoleViewModel _viewModel = null!;

        public ConsoleView()
        {
            InitializeComponent();
            Loaded += Console_Loaded;
        }

        private void Console_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _viewModel = (ConsoleViewModel)DataContext!;
        }
    }
}