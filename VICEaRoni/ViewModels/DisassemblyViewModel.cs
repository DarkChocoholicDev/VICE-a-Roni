using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static VICEaRoni.ViewModels.MemoryViewModel;

namespace VICEaRoni.ViewModels;

public partial class DisassemblyViewModel : ViewModelBase
{
    public class InstructionRow
    {
        public string Instruction { get; set; } = string.Empty;
        public uint Address { get; set; }
        public Memory<byte>? Data { get; set; }

        public string AddressString => $"{Address:X4}";
        public string HexDataString =>
            $"{HexByte(00)} {HexByte(01)} {HexByte(02)} {HexByte(03)} {HexByte(04)} {HexByte(05)} {HexByte(06)} {HexByte(07)} - " +
            $"{HexByte(08)} {HexByte(09)} {HexByte(10)} {HexByte(11)} {HexByte(12)} {HexByte(13)} {HexByte(14)} {HexByte(15)} ";
        public string CharDataString =>
            $"{CharOrDot(00)}{CharOrDot(01)}{CharOrDot(02)}{CharOrDot(03)}{CharOrDot(04)}{CharOrDot(05)}{CharOrDot(06)}{CharOrDot(07)}" +
            $"{CharOrDot(08)}{CharOrDot(09)}{CharOrDot(10)}{CharOrDot(11)}{CharOrDot(12)}{CharOrDot(13)}{CharOrDot(14)}{CharOrDot(15)}";

        public string HexByte(int index) 
        { 
            return (Data.HasValue) ? $"{Data.Value.Span[index]:X2}" : "??"; 
        }

        public char CharOrDot(int index)
        {
            if (Data.HasValue)
            {
                var pc = PETSCIIHelper.PETSCIIChars[Data.Value.Span[index]].UnshiftedASCIIValue;
                return (pc != '\0') ? pc : '.';
            }
            else
            {
                return '?';
            }    
        }

        public char CharOrDot(byte c)
        { 
            var pc = PETSCIIHelper.PETSCIIChars[c].UnshiftedASCIIValue; return (pc != '\0') ? pc : '.'; 
        }
    }

    public AddressSpace[] AddressSpaceEnumValues => Enum.GetValues<AddressSpace>();

    [ObservableProperty]
    public partial IReadOnlyList<InstructionRow> InstructionRows { get; private set; } = [];

    [ObservableProperty]
    public partial AddressSpace AddressSpace { get; set; } = AddressSpace.Main;

    [ObservableProperty]
    public partial string StartingAddressOrLabel { get; set; } = "0000";

    [ObservableProperty]
    public partial string UnitCount { get; set; } = "1000";

    [ObservableProperty]
    public partial bool FollowPC { get; set; } = false;

    const int MaxEmptyRows = 200;
    const int MaxRows = 4096;

    UInt16 _pcAddress;
    byte[]? _bytes = null;

    public DisassemblyViewModel(Monitor monitor)
    {
        _monitor = monitor;
        _monitor.DebuggerStateChanged += _monitor_DebuggerStateChanged;

        if (!_monitor.IsStopped)
        {
            LoadEmptyValues();
        }
    }

    private async void _monitor_DebuggerStateChanged(object? sender, System.EventArgs e)
    {
        if (_monitor.IsStopped)
        {
            _pcAddress = _monitor.GetPC();
            LoadMemoryValues();
        }
    }

    partial void OnAddressSpaceChanged(AddressSpace value)
    {
        if (_monitor.IsStopped)
        {
            LoadMemoryValues();
        }
    }

    partial void OnStartingAddressOrLabelChanged(string value)
    {
        if (_monitor.IsStopped)
        {
            LoadMemoryValues();
        }
    }

    partial void OnUnitCountChanged(string value)
    {
        if (_monitor.IsStopped)
        {
            LoadMemoryValues();
        }
    }

    private async void LoadEmptyValues()
    {
        // Set up "empty" data to be displayed until the monitor is stopped.
        var rows = new List<InstructionRow>();
        for (uint u = 0; u < MaxEmptyRows; u++)
        {
            rows.Add(new InstructionRow { Address = u, Data = null, Instruction = "?" });
        }

        InstructionRows = rows;
    }

    private async void LoadMemoryValues()
    {
        uint? address = _pcAddress;

        if (!FollowPC)
        {
            address = _monitor.GetAddress(StartingAddressOrLabel);
        }

        bool lengthResult = uint.TryParse(UnitCount, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var parsedLength);

        if (address != null && lengthResult)
        {
            var result = await _monitor.ReadMemoryBytes(AddressSpace, 0, 65536);
            if (result.Success)
            {
                _bytes = result.Bytes;
                var region = new MemoryRegion(0, _bytes!);
                var nextInstructionAddress = (uint)address;
                var rows = new List<InstructionRow>();

                for (var i = 0; i < parsedLength && nextInstructionAddress <= 0xFFFF; i++)
                {
                    //// Display any code labels first.
                    //IEnumerable<SourceSymbol> matchingInstructionLabels = _symbolTable.GetSymbolsFromValue(nextInstructionAddress);
                    //foreach (var label in matchingInstructionLabels)
                    //{
                    //    console.WriteLine($"{label.Name}:");
                    //}

                    var instruction = _monitor.DisassembleSingle(nextInstructionAddress, region, out nextInstructionAddress);
                    rows.Add(new InstructionRow { Instruction = instruction });
                }

                InstructionRows = rows;
                return;
            }
        }

        LoadEmptyValues();
    }

    public Monitor _monitor { get; set; }
}
