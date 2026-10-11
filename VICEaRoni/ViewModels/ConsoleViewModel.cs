using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorControl;
using System.Linq;
using System.Threading.Tasks;

namespace VICEaRoni.ViewModels;

public partial class ConsoleViewModel : ViewModelBase, IConsoleOutput
{
    public ConsoleViewModel(Monitor monitor)
    {
        ConsoleOutput = new ConsoleOutputViewModel();
        ConsoleInput = new ConsoleInputViewModel();
        ConsoleInput.ProcessCommandAsync = OnProcessCommand;
        _monitor = monitor;
    }

    public ConsoleOutputViewModel ConsoleOutput { get; set; }

    public ConsoleInputViewModel ConsoleInput { get; set; }

    public Monitor _monitor { get; set; }

    private async Task OnProcessCommand(string command)
    {
        ConsoleOutput.WriteLine(command);
        await _monitor.ExecuteCommand(command, this);
    }

    public void Write(char c) => ConsoleOutput.Write(c);
    public void Write(string text) => ConsoleOutput.Write(text);
    public void WriteLine() => ConsoleOutput.WriteLine();
    public void WriteLine(string text) => ConsoleOutput.WriteLine(text);
}
