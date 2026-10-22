using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VICEaRoni.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    public string RegA => (Registers != null) ? $"{Registers.A:X2}" : "??";
    public string RegX => (Registers != null) ? $"{Registers.X:X2}" : "??";
    public string RegY => (Registers != null) ? $"{Registers.Y:X2}" : "??";
    public string RegSP => (Registers != null) ? $"{Registers.SP:X2}" : "??";
    public string RegPC => (Registers != null) ? $"{Registers.PC:X4}" : "????";
    public string RegFlags => (Registers != null) ? $"{Registers.FlagString} ({Registers.Flags:X2})" : "???????? (??)";

    public Registers6502? Registers 
    { 
        get; 
        set
        {
            field = value;
            OnPropertyChanged(nameof(RegA));
            OnPropertyChanged(nameof(RegX));
            OnPropertyChanged(nameof(RegY));
            OnPropertyChanged(nameof(RegSP));
            OnPropertyChanged(nameof(RegPC));
            OnPropertyChanged(nameof(RegFlags));
        }
    }

    public Monitor _monitor { get; set; }

    public RegisterViewModel(Monitor monitor)
    {
        _monitor = monitor;
        _monitor.DebuggerStateChanged += _monitor_DebuggerStateChanged;
        _monitor.RegistersChanged += _monitor_RegistersChanged;

        if (!_monitor.IsStopped)
        {
            LoadEmptyValues();
        }
    }

    private async void _monitor_DebuggerStateChanged(object? sender, System.EventArgs e)
    {
        if (_monitor.IsStopped)
        {
            await LoadRegisterValues();
        }
    }

    private async void _monitor_RegistersChanged(object? sender, EventArgs e)
    {
        if (_monitor.IsStopped)
        {
            await LoadRegisterValues();
        }
    }

    private async void LoadEmptyValues()
    {
        Registers = null;
    }

    private async Task LoadRegisterValues()
    {
        var regResult = _monitor.GetRegisters();
        Registers = regResult.Success ? regResult.Data : null;
    }
}
