using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using MonitorControl;

namespace VICEaRoni.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Welcome to VICE-a-Roni!";

    /// <summary>
    /// Gets the primary monitor console.
    /// </summary>
    public ConsoleViewModel Console { get; init; }

    /// <summary>
    /// Gets the secondary monitor console.
    /// </summary>
    public ConsoleViewModel Console2 { get; init; }

    /// <summary>
    /// Gets the active monitor console.
    /// </summary>
    public ConsoleViewModel ActiveConsoleView { get; init; }

    /// <summary>
    /// Monitor instance for sending debugger commands.
    /// </summary>
    readonly Monitor _monitor = new();

    /// <summary>
    /// Default constructor.
    /// </summary>
    public MainViewModel()
    {
        Console = new ConsoleViewModel(_monitor);
        Console2 = new ConsoleViewModel(_monitor);
        ActiveConsoleView = Console;
    }


    [RelayCommand]
    public void OpenCommand()
    {
        ActiveConsoleView.WriteLine("Open me!");
    }

    [RelayCommand]
    public void SaveCommand()
    {
        ActiveConsoleView.WriteLine("Save me!");
    }

    [RelayCommand]
    public void ExitCommand()
    {
        ActiveConsoleView.WriteLine("Say Goodnight, Gracie!");
    }

    [RelayCommand]
    public async Task ConnectCommand()
    {
        if (!_monitor.IsConnected)
        {
            ActiveConsoleView.Write("Connecting...");
            var result = await _monitor.Connect();
            if (result.Success)
            {
                ActiveConsoleView.WriteLine("success!");
            }
            else
            {
                ActiveConsoleView.WriteLine($"failed!\nFailure: {result.FailureMessage}");
            }
        }
        else
        {
            ActiveConsoleView.WriteLine($"The monitor is already connectioned to a VICE session.");
        }
    }

    [RelayCommand]
    public void BreakCommand()
    {
        if (_monitor.IsConnected)
        {
            _monitor.RequestMonitorBreak(ActiveConsoleView);
        }
        else
        {
            ActiveConsoleView.WriteLine($"Cannot request a break until a VICE connection has been established.");
        }
    }
}
