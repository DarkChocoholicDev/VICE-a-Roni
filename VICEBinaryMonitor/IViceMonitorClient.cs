namespace VICEBinaryMonitor;

/// <summary>
/// High-level asynchronous client for VICE's Binary Monitor API version 2.
///
/// Important VICE behavior: sending a monitor command causes the emulated machine
/// to enter the monitor (stop). ContinueAsync() resumes it.
/// </summary>
public interface IViceMonitorClient : IAsyncDisposable
{
    bool IsConnected { get; }
    bool IsStopped { get; }
    ushort? CurrentProgramCounter { get; }

    event EventHandler<ViceStoppedEventArgs>? Stopped;
    event EventHandler<ViceResumedEventArgs>? Resumed;
    event EventHandler<ViceJammedEventArgs>? Jammed;
    event EventHandler<ViceCheckpointEventArgs>? CheckpointHit;
    event EventHandler<ViceRegistersEventArgs>? RegistersReceived;
    event EventHandler<ViceConnectionLostEventArgs>? ConnectionLost;
    event EventHandler<ViceUnhandledResponseEventArgs>? UnhandledResponse;

    Task ConnectAsync(string host = "127.0.0.1", int port = 6502,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends PING. Because any Binary Monitor command enters the monitor, this is
    /// also a convenient explicit "break/pause" operation.
    /// </summary>
    Task BreakAsync(CancellationToken cancellationToken = default);

    Task PingAsync(CancellationToken cancellationToken = default);

    Task<byte[]> ReadMemoryAsync(
        ushort startAddress,
        ushort endAddress,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        ushort bankId = 0,
        bool sideEffects = false,
        CancellationToken cancellationToken = default);

    Task WriteMemoryAsync(
        ushort startAddress,
        ReadOnlyMemory<byte> data,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        ushort bankId = 0,
        bool sideEffects = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViceRegisterDefinition>> GetRegisterDefinitionsAsync(
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViceRegisterValue>> GetRegisterValuesAsync(
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default);

    Task<ViceRegisterSnapshot> GetRegistersAsync(
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default);

    Task SetRegistersAsync(
        IEnumerable<ViceRegisterWrite> registers,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default);

    Task SetRegisterAsync(
        string registerName,
        ushort value,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViceBankDefinition>> GetBanksAsync(
        CancellationToken cancellationToken = default);

    Task<ViceBankDefinition?> FindBankAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<ViceCheckpoint> SetCheckpointAsync(
        ushort startAddress,
        ushort? endAddress = null,
        ViceCheckpointOperation operation = ViceCheckpointOperation.Execute,
        bool stopWhenHit = true,
        bool enabled = true,
        bool temporary = false,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default);

    Task<ViceCheckpoint> GetCheckpointAsync(
        uint checkpointNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViceCheckpoint>> GetCheckpointsAsync(
        CancellationToken cancellationToken = default);

    Task DeleteCheckpointAsync(uint checkpointNumber,
        CancellationToken cancellationToken = default);

    Task SetCheckpointEnabledAsync(uint checkpointNumber, bool enabled,
        CancellationToken cancellationToken = default);

    Task SetCheckpointConditionAsync(uint checkpointNumber, string condition,
        CancellationToken cancellationToken = default);

    Task StepAsync(ushort instructionCount = 1,
        bool stepOverSubroutines = false,
        CancellationToken cancellationToken = default);

    Task StepOverAsync(ushort instructionCount = 1,
        CancellationToken cancellationToken = default);

    Task ExecuteUntilReturnAsync(CancellationToken cancellationToken = default);

    /// <summary>Leaves the monitor and resumes emulation.</summary>
    Task ContinueAsync(CancellationToken cancellationToken = default);

    Task<ViceInfo> GetViceInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>VICE 3.10+.</summary>
    Task<IReadOnlyList<ViceCpuHistoryEntry>> GetCpuHistoryAsync(
        uint count,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default);

    Task<ViceDisplay> GetDisplayAsync(bool useVicII = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VicePaletteColor>> GetPaletteAsync(bool useVicII = true,
        CancellationToken cancellationToken = default);

    Task<ViceResourceValue> GetResourceAsync(string resourceName,
        CancellationToken cancellationToken = default);

    Task SetResourceAsync(string resourceName, string value,
        ViceResourceType type = ViceResourceType.String,
        CancellationToken cancellationToken = default);

    Task FeedKeyboardAsync(ReadOnlyMemory<byte> petscii,
        CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync(string filename, bool saveRoms = true, bool saveDisks = true,
        CancellationToken cancellationToken = default);

    Task<ushort> LoadSnapshotAsync(string filename,
        CancellationToken cancellationToken = default);

    Task ResetAsync(ViceResetTarget target = ViceResetTarget.System,
        CancellationToken cancellationToken = default);

    Task AutostartAsync(string filename, bool runAfterLoading = true, ushort fileIndex = 0,
        CancellationToken cancellationToken = default);

    Task SetJoyportAsync(ushort port, ushort value,
        CancellationToken cancellationToken = default);

    Task SetUserportAsync(ushort value,
        CancellationToken cancellationToken = default);

    /// <summary>Terminates the VICE emulator process. This is intentionally separate from ContinueAsync().</summary>
    Task QuitViceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Escape hatch for API additions not yet wrapped by this library. The terminal
    /// response type may differ from the command (e.g. CHECKPOINT_SET -> CHECKPOINT_INFO).
    /// </summary>
    Task<IReadOnlyList<ViceRawResponse>> SendRawAsync(
        byte command,
        byte terminalResponseType,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default);
}
