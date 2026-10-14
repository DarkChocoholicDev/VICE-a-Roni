using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using VICEaRoni.ViewModels;

namespace VICEaRoni.Views
{
    public partial class ConsoleOutputControl : UserControl
    {
        ConsoleOutputViewModel _viewModel = null!;

        public ConsoleOutputControl()
        {
            InitializeComponent();
            Loaded += ConsoleOutputControl_Loaded;
        }

        private async void ConsoleOutputControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _viewModel = (ConsoleOutputViewModel)DataContext!;
            _viewModel.ScrollViewer = MainSV;
        }
    }
}