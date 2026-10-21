using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VICEaRoni.ViewModels;

public partial class MemoryViewModel : ViewModelBase
{
    public class MemoryRow
    {
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
            if (Data.HasValue)
            {
                if (index < Data.Value.Length)
                {
                    return $"{Data.Value.Span[index]:X2}";
                }
                else
                {
                    return "  ";
                }
            }
            else
            {
                return "??"; 
            }
        }

        public char CharOrDot(int index)
        {
            if (Data.HasValue)
            {
                if (index < Data.Value.Length)
                {
                    var pc = PETSCIIHelper.PETSCIIChars[Data.Value.Span[index]].UnshiftedASCIIValue;
                    return (pc != '\0') ? pc : '.';
                }
                else
                {
                    return ' ';
                }
            }
            else
            {
                return '?';
            }    
        }
    }

    public AddressSpace[] AddressSpaceEnumValues => Enum.GetValues<AddressSpace>();

    [ObservableProperty]
    public partial IReadOnlyList<MemoryRow> MemoryRows { get; private set; } = [];

    [ObservableProperty]
    public partial AddressSpace AddressSpace { get; set; } = AddressSpace.Main;

    [ObservableProperty]
    public partial string StartingAddressOrLabel { get; set; } = "0000";

    [ObservableProperty]
    public partial string UnitCount { get; set; } = "10000";

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

    public MemoryViewModel(Monitor monitor)
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
            LoadMemoryValues();
        }
    }

    private async void LoadEmptyValues()
    {
        // Set up "empty" data to be displayed until the monitor is stopped.
        var memoryRows = new List<MemoryRow>();
        for (uint u = 0; u < 65536 / 16; u++)
        {
            memoryRows.Add(new MemoryRow { Address = u * 16, Data = null });
        }

        MemoryRows = memoryRows;
    }

    private async void LoadMemoryValues()
    {
        var parsedAddress = _monitor.GetAddress(StartingAddressOrLabel);
        if (parsedAddress != null)
        {
            if (uint.TryParse(UnitCount, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var parsedLength))
            {
                var startingAddress = (uint)parsedAddress!;
                var byteCount = parsedLength;
                var result = await _monitor.ReadMemoryBytes(AddressSpace, startingAddress, byteCount);
                if (result.Success)
                {
                    Memory<byte> bytes = result.Bytes!;
                    var memoryRows = new List<MemoryRow>();
                    var bytesLeft = (int)byteCount;
                    for (uint row = 0; bytesLeft > 0; row++, bytesLeft -= 16)
                    {
                        memoryRows.Add(new MemoryRow { Address = startingAddress + row * 16, Data = bytes.Slice((int)(row * 16), Math.Min(16, (int)bytesLeft)) });
                    }

                    MemoryRows = memoryRows;
                    return;
                }
            }
        }

        LoadEmptyValues();
    }

    public Monitor _monitor { get; set; }
}
