using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace VICEBinaryMonitor;

public sealed class ViceMonitorClient : IViceMonitorClient
{
    private const byte Stx = 0x02;
    private const byte ApiVersion = 0x02;
    private const uint EventRequestId = 0xFFFFFFFF;
    private const int ResponseHeaderLength = 12;
    private const int MaxResponseBodyLength = 64 * 1024 * 1024;

    private static class Command
    {
        public const byte MemoryGet = 0x01;
        public const byte MemorySet = 0x02;
        public const byte CheckpointGet = 0x11;
        public const byte CheckpointSet = 0x12;
        public const byte CheckpointDelete = 0x13;
        public const byte CheckpointList = 0x14;
        public const byte CheckpointToggle = 0x15;
        public const byte ConditionSet = 0x22;
        public const byte RegistersGet = 0x31;
        public const byte RegistersSet = 0x32;
        public const byte Dump = 0x41;
        public const byte Undump = 0x42;
        public const byte ResourceGet = 0x51;
        public const byte ResourceSet = 0x52;
        public const byte AdvanceInstructions = 0x71;
        public const byte KeyboardFeed = 0x72;
        public const byte ExecuteUntilReturn = 0x73;
        public const byte Ping = 0x81;
        public const byte BanksAvailable = 0x82;
        public const byte RegistersAvailable = 0x83;
        public const byte DisplayGet = 0x84;
        public const byte ViceInfo = 0x85;
        public const byte CpuHistory = 0x86;
        public const byte PaletteGet = 0x91;
        public const byte JoyportSet = 0xA2;
        public const byte UserportSet = 0xB2;
        public const byte Exit = 0xAA;
        public const byte Quit = 0xBB;
        public const byte Reset = 0xCC;
        public const byte Autostart = 0xDD;
    }

    private static class Response
    {
        public const byte Invalid = 0x00;
        public const byte CheckpointInfo = 0x11;
        public const byte RegisterInfo = 0x31;
        public const byte Jam = 0x61;
        public const byte Stopped = 0x62;
        public const byte Resumed = 0x63;
    }

    private readonly ConcurrentDictionary<uint, PendingRequest> _pending = new();
    private readonly ConcurrentDictionary<ViceMemorySpace, IReadOnlyList<ViceRegisterDefinition>> _registerDefinitions = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readerCancellation;
    private Task? _readerTask;
    private int _requestSequence;
    private volatile bool _isStopped;
    private ushort? _currentProgramCounter;

    public bool IsConnected => _tcpClient?.Connected == true && _stream is not null;
    public bool IsStopped => _isStopped;
    public ushort? CurrentProgramCounter => _currentProgramCounter;

    public event EventHandler<ViceStoppedEventArgs>? Stopped;
    public event EventHandler<ViceResumedEventArgs>? Resumed;
    public event EventHandler<ViceJammedEventArgs>? Jammed;
    public event EventHandler<ViceCheckpointEventArgs>? CheckpointHit;
    public event EventHandler<ViceRegistersEventArgs>? RegistersReceived;
    public event EventHandler<ViceConnectionLostEventArgs>? ConnectionLost;
    public event EventHandler<ViceUnhandledResponseEventArgs>? UnhandledResponse;

    public async Task ConnectAsync(string host = "127.0.0.1", int port = 6502,
        CancellationToken cancellationToken = default)
    {
        if (IsConnected) throw new InvalidOperationException("Already connected to VICE.");

        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            _tcpClient = client;
            _stream = client.GetStream();
            _readerCancellation = new CancellationTokenSource();
            _readerTask = ReceiveLoopAsync(_readerCancellation.Token);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var readerTask = _readerTask;
        _readerCancellation?.Cancel();

        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }

        if (readerTask is not null)
        {
            try { await readerTask.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { }
        }

        _readerTask = null;
        _readerCancellation?.Dispose();
        _readerCancellation = null;
        _stream = null;
        _tcpClient = null;
        _registerDefinitions.Clear();
        _isStopped = false;
        _currentProgramCounter = null;

        FailAllPending(new IOException("Disconnected from VICE."));
    }

    public Task BreakAsync(CancellationToken cancellationToken = default) => PingAsync(cancellationToken);

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        await SendSingleAsync(Command.Ping, Command.Ping, ReadOnlyMemory<byte>.Empty, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<byte[]> ReadMemoryAsync(
        ushort startAddress,
        ushort endAddress,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        ushort bankId = 0,
        bool sideEffects = false,
        CancellationToken cancellationToken = default)
    {
        if (endAddress < startAddress) throw new ArgumentOutOfRangeException(nameof(endAddress));

        var body = new byte[8];
        body[0] = BoolByte(sideEffects);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(3, 2), endAddress);
        body[5] = (byte)memorySpace;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6, 2), bankId);

        var response = await SendSingleAsync(Command.MemoryGet, Command.MemoryGet, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
        EnsureLength(response.Body, 2, "MEMORY_GET");

        var statedLength = BinaryPrimitives.ReadUInt16LittleEndian(response.Body.AsSpan(0, 2));
        var actualLength = response.Body.Length - 2;
        if (statedLength != 0 && statedLength > actualLength)
            throw new InvalidDataException($"VICE returned a memory length of {statedLength}, but only {actualLength} bytes followed.");

        // For $0000-$FFFF the protocol's 16-bit length field wraps to zero.
        var length = statedLength == 0 ? actualLength : statedLength;
        return response.Body.AsSpan(2, length).ToArray();
    }

    public async Task WriteMemoryAsync(
        ushort startAddress,
        ReadOnlyMemory<byte> data,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        ushort bankId = 0,
        bool sideEffects = false,
        CancellationToken cancellationToken = default)
    {
        if (data.Length == 0) return;
        if (data.Length > 65536) throw new ArgumentOutOfRangeException(nameof(data));
        var end = (long)startAddress + data.Length - 1;
        if (end > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(data), "Write crosses $FFFF.");

        var body = new byte[8 + data.Length];
        body[0] = BoolByte(sideEffects);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(3, 2), (ushort)end);
        body[5] = (byte)memorySpace;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6, 2), bankId);
        data.Span.CopyTo(body.AsSpan(8));

        var response = await SendSingleAsync(Command.MemorySet, Command.MemorySet, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task<IReadOnlyList<ViceRegisterDefinition>> GetRegisterDefinitionsAsync(
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default)
    {
        if (_registerDefinitions.TryGetValue(memorySpace, out var cached)) return cached;

        var response = await SendSingleAsync(
            Command.RegistersAvailable,
            Command.RegistersAvailable,
            new[] { (byte)memorySpace },
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);

        var definitions = ParseRegisterDefinitions(response.Body);
        _registerDefinitions[memorySpace] = definitions;
        return definitions;
    }

    public async Task<IReadOnlyList<ViceRegisterValue>> GetRegisterValuesAsync(
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(
            Command.RegistersGet,
            Response.RegisterInfo,
            new[] { (byte)memorySpace },
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return ParseRegisterValues(response.Body);
    }

    public async Task<ViceRegisterSnapshot> GetRegistersAsync(
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default)
    {
        var definitions = await GetRegisterDefinitionsAsync(memorySpace, cancellationToken).ConfigureAwait(false);
        var values = await GetRegisterValuesAsync(memorySpace, cancellationToken).ConfigureAwait(false);

        var byId = definitions.ToDictionary(d => d.Id);
        var named = new List<KeyValuePair<string, ushort>>(values.Count);
        foreach (var value in values)
        {
            var name = byId.TryGetValue(value.Id, out var definition)
                ? definition.Name
                : $"REG_{value.Id:X2}";
            named.Add(new KeyValuePair<string, ushort>(name, value.Value));
        }
        return new ViceRegisterSnapshot(named);
    }

    public async Task SetRegistersAsync(
        IEnumerable<ViceRegisterWrite> registers,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registers);
        var items = registers.ToArray();
        if (items.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(registers));

        var body = new byte[3 + items.Length * 4];
        body[0] = (byte)memorySpace;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1, 2), (ushort)items.Length);
        var offset = 3;
        foreach (var item in items)
        {
            body[offset] = 3; // ID (1) + value (2)
            body[offset + 1] = item.Id;
            BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(offset + 2, 2), item.Value);
            offset += 4;
        }

        var response = await SendSingleAsync(Command.RegistersSet, Response.RegisterInfo, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task SetRegisterAsync(
        string registerName,
        ushort value,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(registerName)) throw new ArgumentException("Register name is required.", nameof(registerName));
        var definitions = await GetRegisterDefinitionsAsync(memorySpace, cancellationToken).ConfigureAwait(false);
        var definition = definitions.FirstOrDefault(d => string.Equals(d.Name, registerName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"VICE register '{registerName}' was not found in {memorySpace}.");
        await SetRegistersAsync(new[] { new ViceRegisterWrite(definition.Id, value) }, memorySpace, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ViceBankDefinition>> GetBanksAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(Command.BanksAvailable, Command.BanksAvailable,
            ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return ParseBanks(response.Body);
    }

    public async Task<ViceBankDefinition?> FindBankAsync(string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bank name is required.", nameof(name));
        var banks = await GetBanksAsync(cancellationToken).ConfigureAwait(false);
        return banks.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ViceCheckpoint> SetCheckpointAsync(
        ushort startAddress,
        ushort? endAddress = null,
        ViceCheckpointOperation operation = ViceCheckpointOperation.Execute,
        bool stopWhenHit = true,
        bool enabled = true,
        bool temporary = false,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default)
    {
        var body = new byte[9];
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(0, 2), startAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2, 2), endAddress ?? startAddress);
        body[4] = BoolByte(stopWhenHit);
        body[5] = BoolByte(enabled);
        body[6] = (byte)operation;
        body[7] = BoolByte(temporary);
        body[8] = (byte)memorySpace;

        var response = await SendSingleAsync(Command.CheckpointSet, Response.CheckpointInfo, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
        return ParseCheckpoint(response.Body);
    }

    public async Task<ViceCheckpoint> GetCheckpointAsync(uint checkpointNumber,
        CancellationToken cancellationToken = default)
    {
        var body = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(body, checkpointNumber);
        var response = await SendSingleAsync(Command.CheckpointGet, Response.CheckpointInfo, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
        return ParseCheckpoint(response.Body);
    }

    public async Task<IReadOnlyList<ViceCheckpoint>> GetCheckpointsAsync(
        CancellationToken cancellationToken = default)
    {
        var frames = await SendRequestAsync(Command.CheckpointList, Command.CheckpointList,
            ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        var terminal = frames[^1];
        EnsureSuccess(terminal);

        return frames
            .Where(f => f.ResponseType == Response.CheckpointInfo && f.ErrorCode == ViceErrorCode.Ok)
            .Select(f => ParseCheckpoint(f.Body))
            .ToArray();
    }

    public async Task DeleteCheckpointAsync(uint checkpointNumber,
        CancellationToken cancellationToken = default)
    {
        var body = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(body, checkpointNumber);
        var response = await SendSingleAsync(Command.CheckpointDelete, Command.CheckpointDelete, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task SetCheckpointEnabledAsync(uint checkpointNumber, bool enabled,
        CancellationToken cancellationToken = default)
    {
        var body = new byte[5];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0, 4), checkpointNumber);
        body[4] = BoolByte(enabled);
        var response = await SendSingleAsync(Command.CheckpointToggle, Command.CheckpointToggle, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task SetCheckpointConditionAsync(uint checkpointNumber, string condition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var expression = Encoding.UTF8.GetBytes(condition);
        if (expression.Length > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(condition), "VICE condition expressions are limited to 255 encoded bytes.");

        var body = new byte[5 + expression.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0, 4), checkpointNumber);
        body[4] = (byte)expression.Length;
        expression.CopyTo(body.AsSpan(5));

        var response = await SendSingleAsync(Command.ConditionSet, Command.ConditionSet, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task StepAsync(ushort instructionCount = 1, bool stepOverSubroutines = false,
        CancellationToken cancellationToken = default)
    {
        if (instructionCount == 0) throw new ArgumentOutOfRangeException(nameof(instructionCount));
        var body = new byte[3];
        body[0] = BoolByte(stepOverSubroutines);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1, 2), instructionCount);
        var response = await SendSingleAsync(Command.AdvanceInstructions, Command.AdvanceInstructions, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public Task StepOverAsync(ushort instructionCount = 1,
        CancellationToken cancellationToken = default) =>
        StepAsync(instructionCount, true, cancellationToken);

    public async Task ExecuteUntilReturnAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(Command.ExecuteUntilReturn, Command.ExecuteUntilReturn,
            ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task ContinueAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(Command.Exit, Command.Exit, ReadOnlyMemory<byte>.Empty, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task<ViceInfo> GetViceInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(Command.ViceInfo, Command.ViceInfo, ReadOnlyMemory<byte>.Empty, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
        EnsureLength(response.Body, 1, "VICE_INFO");

        var offset = 0;
        var versionLength = response.Body[offset++];
        EnsureRemaining(response.Body, offset, versionLength + 1, "VICE_INFO version");
        var version = response.Body.AsSpan(offset, versionLength).ToArray();
        offset += versionLength;
        var revisionLength = response.Body[offset++];
        EnsureRemaining(response.Body, offset, revisionLength, "VICE_INFO revision");
        var revision = response.Body.AsSpan(offset, revisionLength).ToArray();
        return new ViceInfo(version, revision);
    }

    public async Task<IReadOnlyList<ViceCpuHistoryEntry>> GetCpuHistoryAsync(
        uint count,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main,
        CancellationToken cancellationToken = default)
    {
        var body = new byte[5];
        body[0] = (byte)memorySpace;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(1, 4), count);
        var response = await SendSingleAsync(Command.CpuHistory, Command.CpuHistory, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);

        var definitions = await GetRegisterDefinitionsAsync(memorySpace, cancellationToken).ConfigureAwait(false);
        return ParseCpuHistory(response.Body, definitions);
    }

    public async Task<ViceDisplay> GetDisplayAsync(bool useVicII = true,
        CancellationToken cancellationToken = default)
    {
        var body = new[] { BoolByte(useVicII), (byte)0x00 };
        var response = await SendSingleAsync(Command.DisplayGet, Command.DisplayGet, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
        EnsureLength(response.Body, 21, "DISPLAY_GET");

        var span = response.Body.AsSpan();
        var debugWidth = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4, 2));
        var debugHeight = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(6, 2));
        var xOffset = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(8, 2));
        var yOffset = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(10, 2));
        var innerWidth = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(12, 2));
        var innerHeight = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(14, 2));
        var bitsPerPixel = span[16];
        var bufferLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(17, 4)));
        EnsureRemaining(response.Body, 21, bufferLength, "DISPLAY_GET pixels");
        var pixels = span.Slice(21, bufferLength).ToArray();
        return new ViceDisplay(debugWidth, debugHeight, xOffset, yOffset, innerWidth, innerHeight, bitsPerPixel, pixels);
    }

    public async Task<IReadOnlyList<VicePaletteColor>> GetPaletteAsync(bool useVicII = true,
        CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(Command.PaletteGet, Command.PaletteGet,
            new[] { BoolByte(useVicII) }, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        EnsureLength(response.Body, 2, "PALETTE_GET");

        var count = BinaryPrimitives.ReadUInt16LittleEndian(response.Body.AsSpan(0, 2));
        var offset = 2;
        var colors = new List<VicePaletteColor>(count);
        for (var i = 0; i < count; i++)
        {
            EnsureRemaining(response.Body, offset, 1, "PALETTE_GET item size");
            var size = response.Body[offset];
            EnsureRemaining(response.Body, offset + 1, size, "PALETTE_GET item");
            if (size < 3) throw new InvalidDataException("Invalid palette item returned by VICE.");
            colors.Add(new VicePaletteColor(response.Body[offset + 1], response.Body[offset + 2], response.Body[offset + 3]));
            offset += size + 1;
        }
        return colors;
    }

    public async Task<ViceResourceValue> GetResourceAsync(string resourceName,
        CancellationToken cancellationToken = default)
    {
        var name = EncodeLengthPrefixedString(resourceName, nameof(resourceName));
        var response = await SendSingleAsync(Command.ResourceGet, Command.ResourceGet, name, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
        EnsureLength(response.Body, 2, "RESOURCE_GET");
        var type = (ViceResourceType)response.Body[0];
        var valueLength = response.Body[1];
        EnsureRemaining(response.Body, 2, valueLength, "RESOURCE_GET value");
        return new ViceResourceValue(type, response.Body.AsSpan(2, valueLength).ToArray());
    }

    public async Task SetResourceAsync(string resourceName, string value,
        ViceResourceType type = ViceResourceType.String,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resourceName)) throw new ArgumentException("Resource name is required.", nameof(resourceName));
        ArgumentNullException.ThrowIfNull(value);
        var nameBytes = Encoding.UTF8.GetBytes(resourceName);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        if (nameBytes.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(resourceName));
        if (valueBytes.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));

        var body = new byte[3 + nameBytes.Length + valueBytes.Length];
        var offset = 0;
        body[offset++] = (byte)type;
        body[offset++] = (byte)nameBytes.Length;
        nameBytes.CopyTo(body.AsSpan(offset));
        offset += nameBytes.Length;
        body[offset++] = (byte)valueBytes.Length;
        valueBytes.CopyTo(body.AsSpan(offset));

        var response = await SendSingleAsync(Command.ResourceSet, Command.ResourceSet, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task FeedKeyboardAsync(ReadOnlyMemory<byte> petscii,
        CancellationToken cancellationToken = default)
    {
        if (petscii.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(petscii));
        var body = new byte[1 + petscii.Length];
        body[0] = (byte)petscii.Length;
        petscii.Span.CopyTo(body.AsSpan(1));
        var response = await SendSingleAsync(Command.KeyboardFeed, Command.KeyboardFeed, body, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task SaveSnapshotAsync(string filename, bool saveRoms = true, bool saveDisks = true,
        CancellationToken cancellationToken = default)
    {
        var filenameBytes = Encoding.UTF8.GetBytes(filename ?? throw new ArgumentNullException(nameof(filename)));
        if (filenameBytes.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(filename));
        var body = new byte[3 + filenameBytes.Length];
        body[0] = BoolByte(saveRoms);
        body[1] = BoolByte(saveDisks);
        body[2] = (byte)filenameBytes.Length;
        filenameBytes.CopyTo(body.AsSpan(3));
        var response = await SendSingleAsync(Command.Dump, Command.Dump, body, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task<ushort> LoadSnapshotAsync(string filename,
        CancellationToken cancellationToken = default)
    {
        var body = EncodeLengthPrefixedString(filename, nameof(filename));
        var response = await SendSingleAsync(Command.Undump, Command.Undump, body, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        EnsureLength(response.Body, 2, "UNDUMP");
        return BinaryPrimitives.ReadUInt16LittleEndian(response.Body.AsSpan(0, 2));
    }

    public async Task ResetAsync(ViceResetTarget target = ViceResetTarget.System,
        CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(Command.Reset, Command.Reset,
            new[] { (byte)target }, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task AutostartAsync(string filename, bool runAfterLoading = true, ushort fileIndex = 0,
        CancellationToken cancellationToken = default)
    {
        var filenameBytes = Encoding.UTF8.GetBytes(filename ?? throw new ArgumentNullException(nameof(filename)));
        if (filenameBytes.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(filename));
        var body = new byte[4 + filenameBytes.Length];
        body[0] = BoolByte(runAfterLoading);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1, 2), fileIndex);
        body[3] = (byte)filenameBytes.Length;
        filenameBytes.CopyTo(body.AsSpan(4));
        var response = await SendSingleAsync(Command.Autostart, Command.Autostart, body, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task SetJoyportAsync(ushort port, ushort value,
        CancellationToken cancellationToken = default)
    {
        var body = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(0, 2), port);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2, 2), value);
        var response = await SendSingleAsync(Command.JoyportSet, Command.JoyportSet, body, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task SetUserportAsync(ushort value,
        CancellationToken cancellationToken = default)
    {
        var body = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(body, value);
        var response = await SendSingleAsync(Command.UserportSet, Command.UserportSet, body, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task QuitViceAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendSingleAsync(Command.Quit, Command.Quit, ReadOnlyMemory<byte>.Empty, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task<IReadOnlyList<ViceRawResponse>> SendRawAsync(
        byte command,
        byte terminalResponseType,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        return await SendRequestAsync(command, terminalResponseType, body, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try { await DisconnectAsync().ConfigureAwait(false); } catch { }
        _sendLock.Dispose();
    }

    private async Task<ViceRawResponse> SendSingleAsync(byte command, byte terminalResponseType,
        ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var frames = await SendRequestAsync(command, terminalResponseType, body, cancellationToken).ConfigureAwait(false);
        return frames[^1];
    }

    private async Task<IReadOnlyList<ViceRawResponse>> SendRequestAsync(byte command, byte terminalResponseType,
        ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("Not connected to VICE.");
        var requestId = NextRequestId();
        var pending = new PendingRequest(terminalResponseType);
        if (!_pending.TryAdd(requestId, pending)) throw new InvalidOperationException("Could not allocate a VICE request ID.");

        var packet = new byte[11 + body.Length];
        packet[0] = Stx;
        packet[1] = ApiVersion;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(2, 4), checked((uint)body.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(6, 4), requestId);
        packet[10] = command;
        body.Span.CopyTo(packet.AsSpan(11));

        try
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }

            try
            {
                return await pending.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _pending.TryRemove(requestId, out _);
            }
        }
        catch
        {
            _pending.TryRemove(requestId, out _);
            throw;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var stream = _stream ?? throw new InvalidOperationException("VICE stream is unavailable.");
            var header = new byte[ResponseHeaderLength];

            while (!cancellationToken.IsCancellationRequested)
            {
                await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
                if (header[0] != Stx) throw new InvalidDataException($"Invalid VICE response STX 0x{header[0]:X2}.");
                if (header[1] != ApiVersion) throw new InvalidDataException($"VICE returned Binary Monitor API version {header[1]}, expected {ApiVersion}.");

                var bodyLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(2, 4));
                if (bodyLength > MaxResponseBodyLength) throw new InvalidDataException($"VICE response body is unreasonably large: {bodyLength} bytes.");

                var responseType = header[6];
                var errorCode = (ViceErrorCode)header[7];
                var requestId = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));
                var body = new byte[checked((int)bodyLength)];
                if (body.Length != 0) await ReadExactlyAsync(stream, body, cancellationToken).ConfigureAwait(false);

                var response = new ViceRawResponse(header[1], responseType, errorCode, requestId, body);

                if (requestId == EventRequestId)
                {
                    DispatchEvent(response);
                    continue;
                }

                if (_pending.TryGetValue(requestId, out var pending))
                {
                    if (pending.Add(response)) _pending.TryRemove(requestId, out _);
                }
                else
                {
                    UnhandledResponse?.Invoke(this, new ViceUnhandledResponseEventArgs(response));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (EndOfStreamException ex)
        {
            FailAllPending(ex);
            ConnectionLost?.Invoke(this, new ViceConnectionLostEventArgs(ex));
        }
        catch (IOException ex)
        {
            FailAllPending(ex);
            if (!cancellationToken.IsCancellationRequested)
                ConnectionLost?.Invoke(this, new ViceConnectionLostEventArgs(ex));
        }
        catch (Exception ex)
        {
            FailAllPending(ex);
            if (!cancellationToken.IsCancellationRequested)
                ConnectionLost?.Invoke(this, new ViceConnectionLostEventArgs(ex));
        }
    }

    private void DispatchEvent(ViceRawResponse response)
    {
        if (response.ErrorCode != ViceErrorCode.Ok) return;

        switch (response.ResponseType)
        {
            case Response.CheckpointInfo:
            {
                var checkpoint = ParseCheckpoint(response.Body);
                if (checkpoint.CurrentlyHit)
                    CheckpointHit?.Invoke(this, new ViceCheckpointEventArgs(checkpoint));
                break;
            }
            case Response.RegisterInfo:
                RegistersReceived?.Invoke(this, new ViceRegistersEventArgs(ParseRegisterValues(response.Body)));
                break;
            case Response.Jam:
            {
                var pc = ParseProgramCounter(response.Body, "JAM");
                _isStopped = true;
                _currentProgramCounter = pc;
                Jammed?.Invoke(this, new ViceJammedEventArgs(pc));
                break;
            }
            case Response.Stopped:
            {
                var pc = ParseProgramCounter(response.Body, "STOPPED");
                _isStopped = true;
                _currentProgramCounter = pc;
                Stopped?.Invoke(this, new ViceStoppedEventArgs(pc));
                break;
            }
            case Response.Resumed:
            {
                var pc = ParseProgramCounter(response.Body, "RESUMED");
                _isStopped = false;
                _currentProgramCounter = pc;
                Resumed?.Invoke(this, new ViceResumedEventArgs(pc));
                break;
            }
            default:
                UnhandledResponse?.Invoke(this, new ViceUnhandledResponseEventArgs(response));
                break;
        }
    }

    private static IReadOnlyList<ViceRegisterValue> ParseRegisterValues(byte[] body)
    {
        EnsureLength(body, 2, "REGISTER_INFO");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(0, 2));
        var offset = 2;
        var values = new List<ViceRegisterValue>(count);

        for (var i = 0; i < count; i++)
        {
            EnsureRemaining(body, offset, 1, "REGISTER_INFO item size");
            var size = body[offset];
            EnsureRemaining(body, offset + 1, size, "REGISTER_INFO item");
            if (size < 3) throw new InvalidDataException("Invalid register item returned by VICE.");
            var id = body[offset + 1];
            var value = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(offset + 2, 2));
            values.Add(new ViceRegisterValue(id, value));
            offset += size + 1;
        }
        return values;
    }

    private static IReadOnlyList<ViceRegisterDefinition> ParseRegisterDefinitions(byte[] body)
    {
        EnsureLength(body, 2, "REGISTERS_AVAILABLE");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(0, 2));
        var offset = 2;
        var result = new List<ViceRegisterDefinition>(count);

        for (var i = 0; i < count; i++)
        {
            EnsureRemaining(body, offset, 1, "REGISTERS_AVAILABLE item size");
            var size = body[offset];
            EnsureRemaining(body, offset + 1, size, "REGISTERS_AVAILABLE item");
            if (size < 3) throw new InvalidDataException("Invalid register definition returned by VICE.");
            var id = body[offset + 1];
            var bitSize = body[offset + 2];
            var nameLength = body[offset + 3];
            if (3 + nameLength > size) throw new InvalidDataException("Invalid register-name length returned by VICE.");
            var name = Encoding.UTF8.GetString(body, offset + 4, nameLength);
            result.Add(new ViceRegisterDefinition(id, bitSize, name));
            offset += size + 1;
        }
        return result;
    }

    private static IReadOnlyList<ViceBankDefinition> ParseBanks(byte[] body)
    {
        EnsureLength(body, 2, "BANKS_AVAILABLE");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(0, 2));
        var offset = 2;
        var result = new List<ViceBankDefinition>(count);
        for (var i = 0; i < count; i++)
        {
            EnsureRemaining(body, offset, 1, "BANKS_AVAILABLE item size");
            var size = body[offset];
            EnsureRemaining(body, offset + 1, size, "BANKS_AVAILABLE item");
            if (size < 3) throw new InvalidDataException("Invalid bank definition returned by VICE.");
            var id = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(offset + 1, 2));
            var nameLength = body[offset + 3];
            if (3 + nameLength > size) throw new InvalidDataException("Invalid bank-name length returned by VICE.");
            var name = Encoding.UTF8.GetString(body, offset + 4, nameLength);
            result.Add(new ViceBankDefinition(id, name));
            offset += size + 1;
        }
        return result;
    }

    private static ViceCheckpoint ParseCheckpoint(byte[] body)
    {
        EnsureLength(body, 23, "CHECKPOINT_INFO");
        return new ViceCheckpoint(
            BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(0, 4)),
            body[4] != 0,
            BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(5, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(7, 2)),
            body[9] != 0,
            body[10] != 0,
            (ViceCheckpointOperation)body[11],
            body[12] != 0,
            BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(13, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(17, 4)),
            body[21] != 0,
            (ViceMemorySpace)body[22]);
    }

    private static IReadOnlyList<ViceCpuHistoryEntry> ParseCpuHistory(
        byte[] body,
        IReadOnlyList<ViceRegisterDefinition> definitions)
    {
        EnsureLength(body, 4, "CPU_HISTORY");
        var count = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(0, 4));
        if (count > int.MaxValue) throw new InvalidDataException("CPU history count is too large.");
        var byId = definitions.ToDictionary(d => d.Id);
        var offset = 4;
        var result = new List<ViceCpuHistoryEntry>((int)count);

        for (uint item = 0; item < count; item++)
        {
            EnsureRemaining(body, offset, 1, "CPU_HISTORY item size");
            var itemSize = body[offset];
            EnsureRemaining(body, offset + 1, itemSize, "CPU_HISTORY item");
            var itemEnd = offset + 1 + itemSize;
            var cursor = offset + 1;

            EnsureRemaining(body, cursor, 2, "CPU_HISTORY register count");
            var registerCount = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(cursor, 2));
            cursor += 2;
            var named = new List<KeyValuePair<string, ushort>>(registerCount);

            for (var r = 0; r < registerCount; r++)
            {
                EnsureRemaining(body, cursor, 1, "CPU_HISTORY register item size");
                var registerItemSize = body[cursor];
                EnsureRemaining(body, cursor + 1, registerItemSize, "CPU_HISTORY register item");
                if (registerItemSize < 3) throw new InvalidDataException("Invalid CPU-history register item.");
                var id = body[cursor + 1];
                var value = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(cursor + 2, 2));
                var name = byId.TryGetValue(id, out var definition) ? definition.Name : $"REG_{id:X2}";
                named.Add(new KeyValuePair<string, ushort>(name, value));
                cursor += registerItemSize + 1;
            }

            EnsureRemaining(body, cursor, 9, "CPU_HISTORY clock/instruction length");
            var clock = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(cursor, 8));
            cursor += 8;
            var instructionLength = body[cursor++];
            EnsureRemaining(body, cursor, instructionLength, "CPU_HISTORY instruction bytes");
            var instruction = body.AsSpan(cursor, instructionLength).ToArray();
            cursor += instructionLength;

            if (cursor > itemEnd) throw new InvalidDataException("CPU history item overran its declared size.");
            result.Add(new ViceCpuHistoryEntry(clock, new ViceRegisterSnapshot(named), instruction));
            offset = itemEnd;
        }
        return result;
    }

    private static ushort ParseProgramCounter(byte[] body, string context)
    {
        EnsureLength(body, 2, context);
        return BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(0, 2));
    }

    private static byte[] EncodeLengthPrefixedString(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(parameterName, "Encoded string is limited to 255 bytes.");
        var result = new byte[1 + bytes.Length];
        result[0] = (byte)bytes.Length;
        bytes.CopyTo(result.AsSpan(1));
        return result;
    }

    private static void EnsureSuccess(ViceRawResponse response)
    {
        if (response.ErrorCode != ViceErrorCode.Ok)
            throw new ViceMonitorException(response.ErrorCode, response.ResponseType, response.RequestId);
    }

    private static void EnsureLength(byte[] body, int minimum, string context)
    {
        if (body.Length < minimum) throw new InvalidDataException($"{context}: expected at least {minimum} bytes, received {body.Length}.");
    }

    private static void EnsureRemaining(byte[] body, int offset, int length, string context)
    {
        if (offset < 0 || length < 0 || offset > body.Length - length)
            throw new InvalidDataException($"{context}: truncated VICE response.");
    }

    private uint NextRequestId()
    {
        while (true)
        {
            var candidate = unchecked((uint)Interlocked.Increment(ref _requestSequence));
            if (candidate == EventRequestId || candidate == 0) continue;
            if (!_pending.ContainsKey(candidate)) return candidate;
        }
    }

    private void FailAllPending(Exception exception)
    {
        foreach (var pair in _pending.ToArray())
        {
            if (_pending.TryRemove(pair.Key, out var pending)) pending.Fail(exception);
        }
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("VICE closed the Binary Monitor connection.");
            offset += read;
        }
    }

    private static byte BoolByte(bool value) => value ? (byte)1 : (byte)0;

    private sealed class PendingRequest
    {
        private readonly byte _terminalResponseType;
        private readonly List<ViceRawResponse> _responses = new();
        private readonly object _sync = new();
        private readonly TaskCompletionSource<IReadOnlyList<ViceRawResponse>> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingRequest(byte terminalResponseType) => _terminalResponseType = terminalResponseType;

        public Task<IReadOnlyList<ViceRawResponse>> Task => _completion.Task;

        public bool Add(ViceRawResponse response)
        {
            lock (_sync)
            {
                if (_completion.Task.IsCompleted) return true;
                _responses.Add(response);
                if (response.ErrorCode != ViceErrorCode.Ok ||
                    response.ResponseType == Response.Invalid ||
                    response.ResponseType == _terminalResponseType)
                {
                    _completion.TrySetResult(_responses.ToArray());
                    return true;
                }
                return false;
            }
        }

        public void Fail(Exception exception) => _completion.TrySetException(exception);
    }
}
