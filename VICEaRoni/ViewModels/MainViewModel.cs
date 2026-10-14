using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorControl;
using System;
using System.Threading.Tasks;

namespace VICEaRoni.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets the view model of the primary monitor console view.
    /// </summary>
    public ConsoleViewModel Console { get; init; }

    /// <summary>
    /// Gets the view model of the primary memory view.
    /// </summary>
    public MemoryViewModel Memory { get; init; }

    /// <summary>
    /// Gets the view model of the secondary memory view.
    /// </summary>
    public MemoryViewModel Memory2 { get; init; }

    /// <summary>
    /// Gets the view model of the primary disassembly view.
    /// </summary>
    public DisassemblyViewModel Disassembly { get; init; }

    /// <summary>
    /// Gets the active monitor console.
    /// </summary>
    public ConsoleViewModel ActiveConsoleView { get; init; }

    /// <summary>
    /// Monitor instance for sending debugger commands.
    /// </summary>
    readonly Monitor _monitor = new(DispatchMonitorEvent);

    /// <summary>
    /// Default constructor.
    /// </summary>
    public MainViewModel()
    {
        Console = new ConsoleViewModel(_monitor);
        Memory = new MemoryViewModel(_monitor);
        Memory2 = new MemoryViewModel(_monitor);
        Disassembly = new DisassemblyViewModel(_monitor);
        ActiveConsoleView = Console;
        _monitor.DebuggerStateChanged += _monitor_DebuggerStateChanged;
        UpdateMonitorStatus();
        ActiveConsoleView.WriteLine("Welcome to VICE-a-Roni!");
    }

    /// <summary>
    /// Dispatches monitor events in a UI-thread-safe way.
    /// </summary>
    /// <param name="action">The monitor action (event) to dispatch.</param>
    static void DispatchMonitorEvent(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    private void _monitor_DebuggerStateChanged(object? sender, System.EventArgs e)
    {
        UpdateMonitorStatus();
    }

    private void UpdateMonitorStatus()
    {
        if (!_monitor.IsConnected)
        {
            StatusMessage = "Disconnected from VICE. Please use Monitor -> Connect to connect to VICE.";
        }
        else if (!_monitor.IsStopped)
        {
            StatusMessage = "Running. Press Ctrl+Break to pause and inspect the emulation.";
        }
        else
        {
            StatusMessage = "Emulation paused.";
        }
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

    [RelayCommand]
    public void StepIntoCommand()
    {
        if (_monitor.IsStopped)
        {
            _monitor.StepInto(ActiveConsoleView);
        }
        else
        {
            ActiveConsoleView.WriteLine($"Cannot issue step request until the VICE emulator is stopped.");
        }
    }

    [RelayCommand]
    public void StepOverCommand()
    {
        if (_monitor.IsStopped)
        {
            _monitor.StepOver(ActiveConsoleView);
        }
        else
        {
            ActiveConsoleView.WriteLine($"Cannot issue step request until the VICE emulator is stopped.");
        }
    }

    [RelayCommand]
    public void ContinueCommand()
    {
        if (_monitor.IsStopped)
        {
            _monitor.ContinueExecution(ActiveConsoleView);
        }
        else
        {
            ActiveConsoleView.WriteLine($"Cannot issue continue request until the VICE emulator is stopped.");
        }
    }
}
