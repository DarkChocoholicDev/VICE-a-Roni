using System;
using System.Collections.Generic;
using System.Text;
using VICEBinaryMonitor;

namespace MonitorControl
{
    /// <summary>
    /// A class representing a debugging breakpoint.
    /// </summary>
    public class Breakpoint
    {
        public uint ID { get; set; }

        public bool? Enabled { get; private set; }

        public ViceCheckpointOperation Operation { get; private set; }

        public UInt16 StartAddress { get; private set; }

        public string? SymbolOrAddress { get; private set; }

        public AddressSpace AddressSpace { get; private set; }

        public string? Condition { get; private set; }

        public static async Task<Result<Breakpoint>> CreateBreakpoint(string symbolOrAddress, Monitor monitor, IViceMonitorClient vice)
        {
            var result = new Result<Breakpoint>();
            AddressSpace addressSpace;
            UInt16 address;

            var parsedAddress = monitor.GetAddress(symbolOrAddress.Trim(), out var parsedAddressSpace);
            if (parsedAddress != null)
            {
                address = parsedAddress.Value;
                addressSpace = parsedAddressSpace ?? AddressSpace.Main;
            }
            else
            {
                return result.SetFailureResult("Invalid address or symbol not found.");
            }

            ViceMemorySpace viceMemorySpace = ViceMemorySpace.Main;
            string? checkpointCondition = null;
            switch (addressSpace)
            {
                case AddressSpace.Main:
                    break;

                case AddressSpace.Drive8:
                    viceMemorySpace = ViceMemorySpace.Drive8;
                    break;

                case AddressSpace.Drive9:
                    viceMemorySpace = ViceMemorySpace.Drive9;
                    break;

                case AddressSpace.Drive10:
                    viceMemorySpace = ViceMemorySpace.Drive10;
                    break;

                case AddressSpace.Drive11:
                    viceMemorySpace = ViceMemorySpace.Drive11;
                    break;

                case AddressSpace.COMALPage1:
                    checkpointCondition = "(@io:$de00 & $07) == $00";
                    break;

                case AddressSpace.COMALPage2:
                    checkpointCondition = "(@io:$de00 & $07) == $01";
                    break;

                case AddressSpace.COMALPage3:
                    checkpointCondition = "(@io:$de00 & $07) == $02";
                    break;

                case AddressSpace.COMALPage4:
                    checkpointCondition = "(@io:$de00 & $07) == $03";
                    break;

                case AddressSpace.COMALPage5:
                    checkpointCondition = "(@io:$de00 & $07) == $04";
                    break;

                case AddressSpace.COMALPage6:
                    checkpointCondition = "(@io:$de00 & $07) == $05";
                    break;
            }

            try
            {
                // Set the main execution checkpoint.
                var initialEnable = (checkpointCondition == null);
                ViceCheckpoint? checkpoint = null;
                checkpoint = await vice.SetCheckpointAsync(address, memorySpace: viceMemorySpace, enabled: initialEnable);

                // If this is a break in a COMAL page, add a condition for it.
                if (checkpointCondition != null)
                {
                    await vice.SetCheckpointConditionAsync(checkpoint.Number, checkpointCondition);
                    await vice.SetCheckpointEnabledAsync(checkpoint.Number, true);
                }

                result.Data = new Breakpoint
                {
                    AddressSpace = addressSpace,
                    Condition = checkpointCondition,
                    Enabled = true,
                    ID = checkpoint.Number,
                    Operation = checkpoint.Operation,
                    StartAddress = checkpoint.StartAddress,
                    SymbolOrAddress = symbolOrAddress
                };
            }
            catch (Exception e)
            {
                result.SetFailureResult($"VICE checkpoint request failed: {e.Message}");
            }

            return result;
        }
    }
}
