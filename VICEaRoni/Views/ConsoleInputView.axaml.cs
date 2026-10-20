using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MonitorControl;
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
            _viewModel.SetCommandAsync = OnSetCommand;
        }

        private async void TextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                _viewModel.HandleEnterKeyAsync();
                editCommand.Clear();
            }
            else if (e.Key == Avalonia.Input.Key.Up)
            {
                e.Handled = true;
                _viewModel.SelectPreviousCommandAsync();
            }
            else if (e.Key == Avalonia.Input.Key.Escape)
            {
                e.Handled = true;
                editCommand.Clear();
            }
        }

        private async Task OnSetCommand(string command)
        {
            editCommand.Text = command;
            editCommand.CaretIndex = editCommand.Text.Length;
        }
    }
}