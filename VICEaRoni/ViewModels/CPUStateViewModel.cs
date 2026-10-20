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

public partial class CPUStateViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the view model of the primary disassembly view.
    /// </summary>
    public DisassemblyViewModel Disassembly { get; init; }

    /// <summary>
    /// Gets the view model of the register display.
    /// </summary>
    public RegisterViewModel Registers { get; init; }

    /// <summary>
    /// The monitor instance used emulator access.
    /// </summary>
    private Monitor _monitor { get; set; }

    public CPUStateViewModel(Monitor monitor)
    {
        _monitor = monitor;
        Disassembly = new DisassemblyViewModel(_monitor);
        Registers = new RegisterViewModel(_monitor);
    }
}
