using System.Net;
using VICEBinaryMonitor;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MonitorControl
{
    public class Monitor
    {
        enum DumpType
        {
            Byte,
            Word,
            DWord,
            QWord,
        }

        /// <summary>
        /// Gets a value indicating whether a monitor connection has been established.
        /// </summary>
        public bool IsConnected => _vice.IsConnected;

        /// <summary>
        /// Gets a value indicating whether execution is stopped in the monitor.
        /// </summary>
        public bool IsStopped => _vice.IsStopped;

        /// <summary>
        /// Event indicating that the debugger state has changed.
        /// </summary>
        public event EventHandler? DebuggerStateChanged;

        /// <summary>
        /// Event indicating the memory has changed as a result of editing.
        /// </summary>
        public event EventHandler? MemoryChanged;

        /// <summary>
        /// The caller-provided method for dispatching monitor events.
        /// </summary>
        readonly Action<Action> _dispatcher;

        Disassembler _disassembler = new Disassembler();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Monitor(Action<Action> dispatcher)
        {
            _dispatcher = dispatcher;
            _vice.Connected += (sender, e) => NotifyDebuggerStateChanged();
            _vice.Disconnected += (sender, e) => NotifyDebuggerStateChanged();
            _vice.Jammed += (sender, e) => NotifyDebuggerStateChanged();
            _vice.Resumed += (sender, e) => NotifyDebuggerStateChanged();
            _vice.Stopped += (sender, e) => NotifyDebuggerStateChanged();
        }

        /// <summary>
        /// Sends a DebuggerStateChanged notification.
        /// </summary>
        void NotifyDebuggerStateChanged()
        {
            _dispatcher(() => DebuggerStateChanged?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>
        /// Sends a MemoryChanged notification.
        /// </summary>
        void NotifyMemoryChanged()
        {
            _dispatcher(() => MemoryChanged?.Invoke(this, EventArgs.Empty));
        }

        IViceMonitorClient _vice = new ViceMonitorClient();
        UInt16 _nextDumpAddress = 0;
        uint _defaultDumpLength = 128;
        UInt16 _nextDisassembleAddress = 0;
        uint _defaultDisassembleLength = 10;
        SourceSymbolTable _symbolTable = new SourceSymbolTable();
        DumpType _lastDumpType = DumpType.Byte;

        public async Task<(bool Success,string? FailureMessage)> Connect(string viceHostAddress = "127.0.0.1", int viceHostPort = 6502)
        {
            try
            {
                // If we're not currently connected, attempt to connect.
                if (!_vice.IsConnected)
                {
                    await _vice.ConnectAsync(viceHostAddress, viceHostPort);
                }

                return (true, null);
            }
            catch(Exception e)
            {
                return (false, e.Message);
            }
        }

        public async Task<(bool Success, byte[]? Bytes, string? FailureMessage)> ReadMemoryBytes(AddressSpace addressSpace, uint startingAddress, uint byteCount)
        {
            byte[]? bytes = null;

            try
            {
                // If we're stopped in the monitor...
                if (_vice.IsStopped)
                {
                    bytes = await MemoryRegion.ReadViceMemoryAsync((UInt16)startingAddress, byteCount, _vice, addressSpace);
                    if (bytes != null)
                    {
                        return (true, bytes, null);
                    }
                    else
                    {
                        return (false, null, "Failed to read emulator memory.");
                    }
                }
                else
                {
                    return (false, null, "Could not access memory because the emulator is running.");
                }
            }
            catch (Exception e)
            {
                return (false, null, e.Message);
            }
        }

        public string DisassembleSingle(uint instructionAddress, MemoryRegion region, out uint nextInstructionAddress)
        {
            if (instructionAddress <= 0xFFFF)
            {
                var instruction = _disassembler.DisassembleSingle((UInt16)instructionAddress, region, _symbolTable, out var next);
                nextInstructionAddress = next;
                return instruction;
            }
            else
            {
                nextInstructionAddress = 0;
                return "** Invalid address **";
            }
        }

        /// <summary>
        /// Retrieves the current CPU registers.
        /// </summary>
        /// <returns>The current CPU register snapshot if successful, otherwise a failure indication.</returns>
        public Result<Registers6502> GetRegisters()
        {
            var result = new Result<Registers6502>();
            var viceRegs = _vice.GetRegistersAsync().Result;
            var regs = new Registers6502()
            {
                A = (byte)viceRegs["A"],
                X = (byte)viceRegs["X"],
                Y = (byte)viceRegs["Y"],
                SP = (byte)viceRegs["SP"],
                Flags = (byte)viceRegs["FL"],
                PC = viceRegs["PC"],
            };

            result.Data = regs;
            return result;
        }

        public UInt16 GetPC()
        {
            var regs = _vice.GetRegistersAsync().Result;

            UInt16 regPC = regs["PC"];
            return regPC;
        }

        public async void RequestMonitorBreak(IConsoleOutput console)
        {
            console.WriteLine("Break requested.");
            await _vice.BreakAsync();
        }

        public async void StepInto(IConsoleOutput console)
        {
            await TraceInstruction(console);
        }

        public async void StepOver(IConsoleOutput console)
        {
            await ProcedureStep(console);
        }

        public async void ContinueExecution(IConsoleOutput console)
        {
            await Go(console);
        }

        public async Task ExecuteCommand(string commandLine, IConsoleOutput console)
        {
            if (!_vice.IsStopped)
            {
                console.WriteLine("Cannot issue command while VICE is still executing. Press Ctrl+Break to break into the monitor.");
                return;
            }

            commandLine = commandLine.Trim();
            var cmdBreak = commandLine.IndexOf(' ');
            var command = (cmdBreak > 0) ? commandLine.Substring(0, cmdBreak) : commandLine;
            var arguments = (cmdBreak > 0) ? commandLine.Substring(cmdBreak + 1).Trim() : string.Empty;

            try
            {
                switch (command)
                {
                    case "g":
                        await Go(console);
                        break;

                    case "r":
                        if (string.IsNullOrWhiteSpace(arguments))
                        {
                            ShowRegisters(console);
                        }
                        else
                        {
                            SetRegisters(arguments, console);
                        }
                        break;

                    case "t":
                        await TraceInstruction(console);
                        break;

                    case "p":
                        await ProcedureStep(console);
                        break;

                    case "d":
                        DumpMemory(arguments, console);
                        break;

                    case "db":
                        DumpMemoryBytes(arguments, console);
                        break;

                    case "eb":
                        EditMemoryBytes(arguments, console);
                        break;

                    case "u":
                        DisassembleMemory(arguments, console);
                        break;

                    case "bs":
                        SetBreakpoint(arguments, console);
                        break;

                    case "bl":
                        ListBreakpoints(console);
                        break;

                    case "clearsym":
                        ClearSymbols(console);
                        break;

                    case "loadsym":
                        LoadSymbols(arguments, console);
                        break;

                    case "help":
                        DisplayHelp(console);
                        break;

                    default:
                        console.WriteLine("Unknown command.");
                        break;
                }
            }
            catch (Exception ex)
            {
                    console.WriteLine($"'{command}' command failed: {ex.Message}");
            }
        }

        void DisplayHelp(IConsoleOutput console)
        {
            console.WriteLine("Available commands:");
            console.WriteLine("  g - Continue execution");
            console.WriteLine("  r - Show registers");
            console.WriteLine("  t - Trace instruction");
            console.WriteLine("  db - Dump bytes");
            console.WriteLine("  clearsym - Clear symbols");
            console.WriteLine("  loadsym - Load symbols");
            console.WriteLine("  help - Show this help");
            console.WriteLine("  q - Quit");
        }

        void Stopped(object sender, ViceStoppedEventArgs e)
        {
            //console.WriteLine($"STOPPED at {e.ProgramCounter:X4}");
        }

        void ShowRegisters(IConsoleOutput console)
        {
            var regs = _vice.GetRegistersAsync().Result;

            UInt16 regPC = regs["PC"];
            byte regA = (byte)regs["A"];
            byte regX = (byte)regs["X"];
            byte regY = (byte)regs["Y"];
            byte regSP = (byte)regs["SP"];
            byte regFlags = (byte)regs["FL"];
            var flags =
                $"{((regFlags & 0b10000000) != 0 ? "N" : "-")} " +
                $"{((regFlags & 0b01000000) != 0 ? "V" : "-")} " +
                $"{((regFlags & 0b00010000) != 0 ? "B" : "-")} " +
                $"{((regFlags & 0b00001000) != 0 ? "D" : "-")} " +
                $"{((regFlags & 0b00000100) != 0 ? "I" : "-")} " +
                $"{((regFlags & 0b00000010) != 0 ? "Z" : "-")} " +
                $"{((regFlags & 0b00000001) != 0 ? "C" : "-")} ";

            console.WriteLine($"A={regA:X2}  X={regX:X2}  Y={regY:X2}  SP={regSP:X2}  PC={regPC:X4}  Flags={regFlags:X2}  {flags}");
            DisassembleMemory($"{regPC:X4} l 1", console);
        }

        static readonly string[] _registerNames = [ "PC", "A", "X", "Y", "SP", "FL" ];
        static readonly int[] _registerByteSizes = [ 2, 1, 1, 1, 1, 1 ];

        async void SetRegisters(string argument, IConsoleOutput console)
        {
            // Validate that we have assignment arguments to process.
            var args = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0)
            {
                console.WriteLine("Improperly-formatted register assignment list.");
                return;
            }

            // Validate that the assignment arguments are valid.
            List<(string RegName, UInt16 NewValue)> registerAssignments = [];
            foreach (var arg in args)
            {
                var assignmentParts = arg.Split('=');
                if (assignmentParts.Length != 2)
                {
                    console.WriteLine($"Improperly-formatted register assignment \"{arg}\".");
                    return;
                }

                // Validate the register name.
                var reg = assignmentParts[0].ToUpper();
                var regIndex = _registerNames.IndexOf(reg);
                if (regIndex == -1)
                {
                    console.WriteLine($"Invalid register name \"{reg}\".");
                    return;
                }

                // Validate the value.
                var valueArg = assignmentParts[1];
                var newValue = (_registerByteSizes[regIndex] == 1) ? ParseByteValue(valueArg) : ParseWordValue(valueArg);
                if (newValue != null)
                {
                    registerAssignments.Add((reg, newValue.Value));
                }
                else
                {
                    console.WriteLine($"Invalid value \"{valueArg}\" for register \"{reg}\".");
                    return;
                }
            }

            string? currentReg = null;
            try
            {
                // Write the new register assignments.
                foreach (var assignment in registerAssignments)
                {
                    currentReg = assignment.RegName;
                    await _vice.SetRegisterAsync(assignment.RegName, assignment.NewValue);
                }
            }
            catch
            {
                console.WriteLine($"Failed to write updated value for register {currentReg}");
            }
        }

        void SetBreakpoint(string argument, IConsoleOutput console)
        {
            var args = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var addressArg = args.Length > 0 ? args[0] : null;
            UInt16 address;
            AddressSpace addressSpace = AddressSpace.Main;

            if (addressArg != null)
            {
                var parsedAddress = GetAddress(addressArg, out var specifiedAddressSpace);
                if (parsedAddress != null)
                {
                    address = parsedAddress.Value;
                    addressSpace = specifiedAddressSpace ?? AddressSpace.Main;
                }
                else
                {
                    console.WriteLine("Invalid address or symbol not found.");
                    return;
                }

                var checkpoint = _vice.SetCheckpointAsync(address).GetAwaiter().GetResult();
                if (checkpoint != null)
                {
                    ShowCheckpointDetails(checkpoint, console);
                }
                else
                {
                    console.WriteLine("VICE checkpoint request failed.");
                }
            }
        }

        void ListBreakpoints(IConsoleOutput console)
        {
            console.WriteLine("Breakpoints");

            var checkpoints = _vice.GetCheckpointsAsync().GetAwaiter().GetResult();
            foreach (var checkpoint in checkpoints)
            {
                ShowCheckpointDetails(checkpoint, console);
            }
        }

        void ShowCheckpointDetails(ViceCheckpoint checkpoint, IConsoleOutput console)
        {
            console.WriteLine($"    {checkpoint.Number}: {checkpoint.Operation} {checkpoint.StartAddress:X4}");
        }

        async Task TraceInstruction(IConsoleOutput console)
        {
            _vice.StepAsync().Wait();
            ShowRegisters(console);
        }

        async Task ProcedureStep(IConsoleOutput console)
        {
            _vice.StepOverAsync().Wait();
            ShowRegisters(console);
        }

        async Task Go(IConsoleOutput console)
        {
            _vice.ContinueAsync().Wait();
        }

        void DumpMemory(string argument, IConsoleOutput console)
        {
            switch (_lastDumpType)
            {
                case DumpType.Byte:
                    DumpMemoryBytes(argument, console);
                    break;
            }
        }

        async void DumpMemoryBytes(string argument, IConsoleOutput console)
        {
            var address = _nextDumpAddress;
            int length = (int)_defaultDumpLength;
            AddressSpace addressSpace = AddressSpace.Main;

            var args = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var addressArg = args.Length > 0 ? args[0] : null;
            var lOrToArg = args.Length > 1 ? args[1].ToLower() : null;
            var lengthOrEndAddressArg = args.Length > 2 ? args[2] : null;

            if (addressArg != null)
            {
                var parsedAddress = GetAddress(addressArg, out var specifiedAddressSpace);
                if (parsedAddress != null)
                {
                    address = parsedAddress.Value;
                    addressSpace = specifiedAddressSpace ?? AddressSpace.Main;
                }
                else
                {
                    console.WriteLine("Invalid address or symbol not found.");
                    return;
                }

                if (lOrToArg != null)
                {
                    if (lOrToArg == "l" || lOrToArg == "to")
                    {
                        var parsedLengthOrEndAddress = GetAddress(lengthOrEndAddressArg!, out _);
                        if (parsedLengthOrEndAddress != null)
                        {
                            if (lOrToArg == "l")
                            {
                                length = parsedLengthOrEndAddress.Value;
                            }
                            else if (lOrToArg == "to")
                            {
                                length = (UInt16)(parsedLengthOrEndAddress.Value - address + 1);
                                if (length <= 0)
                                {
                                    console.WriteLine("Length must be greater than 0.");
                                    return;
                                }
                            }
                        }
                        else
                        {
                            console.WriteLine("Invalid length or ending address, or symbol not found.");
                            return;
                        }
                    }
                    else
                    {
                        console.WriteLine("Invalid range specifier. Use 'l' for length or 'to' for ending address.");
                        return;
                    }
                }
            }

            length = Math.Min(length, 65536 - address);
            var memory = await MemoryRegion.ReadViceMemoryAsync(address, ((UInt16)(length)), _vice, addressSpace);
            //var memory = _vice.ReadMemoryAsync(address, ((UInt16)(address + length - 1))).Result;

            if (memory != null)
            {
                for (int i = 0; i < memory.Length; i += 16)
                {
                    console.Write($"{address + i:X4}: ");
                    for (int j = 0; j < 16 && i + j < memory.Length; j++)
                    {
                        console.Write($"{memory[i + j]:X2} ");
                    }

                    console.Write("    ");
                    for (int j = 0; j < 16 && i + j < memory.Length; j++)
                    {
                        byte b = memory[i + j];
                        char c = PETSCIIHelper.PETSCIIChars[b].ShiftedASCIIValue;
                        if (b < 32 || b > 127 || c == '\0')
                        {
                            c = '.';
                        }
                        console.Write(c);
                    }

                    console.WriteLine();
                }

                _nextDumpAddress = ((UInt16)(address + (UInt16)(memory.Length)));
            }
            else
            {
                console.WriteLine("Could not read memory.");
            }
        }

        async void EditMemoryBytes(string argument, IConsoleOutput console)
        {
            UInt16? address = null;
            AddressSpace addressSpace = AddressSpace.Main;

            var args = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Validate the presence of required arguments.
            if (args.Length < 1)
            {
                console.WriteLine("Missing required arguments <address> and <new-value>.");
                return;
            }
            else if (args.Length < 2)
            {
                console.WriteLine("Missing required argument <new-value>.");
                return;
            }

            // Obtain the arguments and parse the address.
            string addressArg = args[0];
            string[] newValueArgs = args[1..];
            address = GetAddress(addressArg, out var specifiedAddressSpace);
            if (address != null)
            {
                if (specifiedAddressSpace != null)
                {
                    addressSpace = specifiedAddressSpace.Value;
                }
            }
            else
            {
                console.WriteLine("Invalid address or symbol not found.");
                return;
            }

            // Parse the collection of new values.
            List<byte> newValues = [];
            foreach (var arg in newValueArgs)
            {
                var value = ParseByteValue(arg);
                if (value != null)
                {
                    newValues.Add(value.Value);
                }
                else
                {
                    console.WriteLine($"Invalid byte value \"{arg}\".");
                    return;
                }
            }

            // Write the new values to memory.
            uint currentAddress = address.Value;
            int bytesChangedCount = 0;
            foreach (var value in newValues)
            {
                if (currentAddress <= 0x0000FFFF)
                {
                    if (await MemoryRegion.WriteViceByteAsync(value, (UInt16)currentAddress, _vice, addressSpace))
                    {
                        bytesChangedCount++;
                        currentAddress++;
                    }
                    else
                    {
                        console.WriteLine($"Failed to write value {value:X2} to address {currentAddress:X4}.");
                        break;
                    }
                }
                else
                {
                    console.WriteLine($"Failed to write to address {currentAddress:X4}");
                    break;
                }
            }

            // Generate a notification if any bytes were changed.
            if (bytesChangedCount > 0)
            {
                NotifyMemoryChanged();
            }
        }

        byte? ParseByteValue(string byteValue)
        {
            if (byte.TryParse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        UInt16? ParseWordValue(string byteValue)
        {
            if (UInt16.TryParse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        async void DisassembleMemory(string argument, IConsoleOutput console)
        {
            var address = _nextDisassembleAddress;
            int instructionCount = (int)_defaultDisassembleLength;
            UInt16? endingAddress = null;

            var args = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var addressArg = args.Length > 0 ? args[0] : null;
            var lOrToArg = args.Length > 1 ? args[1].ToLower() : null;
            var lengthOrEndAddressArg = args.Length > 2 ? args[2] : null;
            AddressSpace addressSpace = AddressSpace.Main;

            if (addressArg != null)
            {
                var parsedAddress = GetAddress(addressArg, out var specifiedAddressSpace);
                if (parsedAddress != null)
                {
                    address = parsedAddress.Value;
                    addressSpace = specifiedAddressSpace ?? AddressSpace.Main;
                }
                else
                {
                    console.WriteLine("Invalid address or symbol not found.");
                    return;
                }

                if (lOrToArg != null)
                {
                    if (lOrToArg == "l" || lOrToArg == "to")
                    {
                        var parsedLengthOrEndAddress = GetAddress(lengthOrEndAddressArg!, out _);
                        if (parsedLengthOrEndAddress != null)
                        {
                            if (lOrToArg == "l")
                            {
                                instructionCount = parsedLengthOrEndAddress.Value;
                            }
                            else if (lOrToArg == "to")
                            {
                                endingAddress = parsedLengthOrEndAddress.Value;
                                if (endingAddress <= address)
                                {
                                    console.WriteLine("Ending address must be greater than or equal to the starting address.");
                                    return;
                                }
                            }
                        }
                        else
                        {
                            console.WriteLine("Invalid length or ending address, or symbol not found.");
                            return;
                        }
                    }
                    else
                    {
                        console.WriteLine("Invalid range specifier. Use 'l' for length or 'to' for ending address.");
                        return;
                    }
                }
            }

            // Determine the buffer length to read.
            // If a starting and ending address pair was provided, make sure to add an extra 2 to the calculation to
            // handle a 3-byte instruction starting at the last specified buffer address.
            // Otherwise, use the instruction count * the maximum instruction length (3).
            var bufferLength = (endingAddress != null) ? (UInt16)(endingAddress) - address + 1 + 2 : instructionCount * 3;
            bufferLength = Math.Min(bufferLength, 65536 - address);
            var memory = await MemoryRegion.ReadViceMemoryAsync(address, ((UInt16)(bufferLength)), _vice, addressSpace);
            if (memory == null)
            {
                console.WriteLine("Unable to access VICE memory.");
                return;
            }

            var region = new MemoryRegion(address, memory);
            var nextInstructionAddress = address;
            var disassembler = new Disassembler();

            while (endingAddress != null ? nextInstructionAddress <= endingAddress : instructionCount-- > 0)
            {
                // Display any code labels first.
                IEnumerable<SourceSymbol> matchingInstructionLabels = _symbolTable.GetSymbolsFromValue(nextInstructionAddress);
                foreach (var label in matchingInstructionLabels)
                {
                    console.WriteLine($"{label.Name}:");
                }

                var instruction = disassembler.DisassembleSingle(nextInstructionAddress, region, _symbolTable, out nextInstructionAddress);
                console.WriteLine(instruction);
            }

            _nextDisassembleAddress = nextInstructionAddress;
        }

        void LoadSymbols(string arguments, IConsoleOutput console)
        {
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                var filePath = arguments;
                var (success, countSymbolsAdded, countLinesIgnored) = SymLoader.LoadSymbolsFromFile(filePath, _symbolTable);
                if (success)
                {
                    console.WriteLine($"Loaded {countSymbolsAdded} symbols from '{filePath}'. Ignored {countLinesIgnored} lines.");
                }
                else
                {
                    console.WriteLine($"Failed to load symbols from '{filePath}'.");
                }
            }
            else
            {
                console.WriteLine("No file path provided for loading symbols.");
            }
        }

        void ClearSymbols(IConsoleOutput console)
        {
            _symbolTable.ClearSymbols();
        }

        public UInt16? GetAddress(string symbolOrAddress)
        {
            var address = GetAddress(symbolOrAddress, out _);
            return address;
        }

        UInt16? GetAddress(string symbolOrAddress, out AddressSpace? addressSpace)
        {
            addressSpace = AddressSpace.Main;

            var addressParts = symbolOrAddress.Split(':');
            if (addressParts.Length == 2)
            {
                addressSpace = MemoryRegion.AddressSpaceFromName(addressParts[0]);
                if (addressSpace == null)
                {
                    return null;
                }

                symbolOrAddress = addressParts[1];
            }

            var symbol = _symbolTable.GetSymbol(symbolOrAddress);
            if (symbol != null)
            {
                return symbol.Value;
            }
            else
            {
                if (UInt16.TryParse(symbolOrAddress, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var parsedAddress))
                {
                    return parsedAddress;
                }
            }

            return null;
        }

        public void LoadSettings(string settingsFilePath, IConsoleOutput console)
        {
            if (System.IO.File.Exists(settingsFilePath))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(settingsFilePath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<Settings>(json);
                    if (settings != null)
                    {
                        _defaultDumpLength = settings.DefaultDumpLength;
                        foreach (var symbolsFile in settings.SymbolsFiles)
                        {
                            var (success, countSymbolsAdded, countLinesIgnored) = SymLoader.LoadSymbolsFromFile(symbolsFile, _symbolTable);
                            if (success)
                            {
                                console.WriteLine($"Loaded {countSymbolsAdded} symbols from '{symbolsFile}'. Ignored {countLinesIgnored} lines.");
                            }
                            else
                            {
                                console.WriteLine($"Failed to load symbols from '{symbolsFile}'.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    console.WriteLine($"Error loading settings from '{settingsFilePath}': {ex.Message}");
                }
            }
            else
            {
                console.WriteLine($"Settings file '{settingsFilePath}' not found.");
            }
        }
    }
}
