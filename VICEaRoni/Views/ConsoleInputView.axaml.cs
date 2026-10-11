using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using VICEaRoni.ViewModels;

namespace VICEaRoni.Views
{
    public partial class ConsoleInputControl : UserControl
    {
        ConsoleInputViewModel _viewModel = null!;

        public ConsoleInputControl()
        {
            InitializeComponent();
            Loaded += ConsoleInputControl_Loaded;
        }

        private async void ConsoleInputControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _viewModel = (ConsoleInputViewModel)DataContext!;
        }

        private async void TextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                _viewModel.HandleEnterKeyAsync();
                editCommand.Clear();
            }
        }
    }
}