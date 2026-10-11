using System.Collections.ObjectModel;

namespace VICEBinaryMonitor;

public enum ViceMemorySpace : byte
{
    Main = 0x00,
    Drive8 = 0x01,
    Drive9 = 0x02,
    Drive10 = 0x03,
    Drive11 = 0x04
}

[Flags]
public enum ViceCheckpointOperation : byte
{
    Load = 0x01,
    Store = 0x02,
    Execute = 0x04
}

public enum ViceResourceType : byte
{
    String = 0x00,
    Integer = 0x01
}

public enum ViceResetTarget : byte
{
    System = 0x00,
    PowerCycle = 0x01,
    Drive8 = 0x08,
    Drive9 = 0x09,
    Drive10 = 0x0A,
    Drive11 = 0x0B
}

public enum ViceErrorCode : byte
{
    Ok = 0x00,
    ObjectDoesNotExist = 0x01,
    InvalidMemorySpace = 0x02,
    InvalidCommandLength = 0x80,
    InvalidParameter = 0x81,
    UnsupportedApiVersion = 0x82,
    UnknownCommand = 0x83,
    GeneralFailure = 0x8F
}

public sealed record ViceRegisterDefinition(byte Id, byte SizeInBits, string Name);
public sealed record ViceRegisterValue(byte Id, ushort Value);
public sealed record ViceRegisterWrite(byte Id, ushort Value);
public sealed record ViceBankDefinition(ushort Id, string Name);
public sealed record VicePaletteColor(byte Red, byte Green, byte Blue);

public sealed record ViceCheckpoint(
    uint Number,
    bool CurrentlyHit,
    ushort StartAddress,
    ushort EndAddress,
    bool StopWhenHit,
    bool Enabled,
    ViceCheckpointOperation Operation,
    bool Temporary,
    uint HitCount,
    uint IgnoreCount,
    bool HasCondition,
    ViceMemorySpace MemorySpace);

public sealed class ViceRegisterSnapshot
{
    private readonly IReadOnlyDictionary<string, ushort> _values;

    public ViceRegisterSnapshot(IEnumerable<KeyValuePair<string, ushort>> values)
    {
        var dictionary = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            dictionary[item.Key] = item.Value;
        }
        _values = new ReadOnlyDictionary<string, ushort>(dictionary);
    }

    public IReadOnlyDictionary<string, ushort> Values => _values;

    public ushort this[string name] => _values[name];

    public bool TryGetValue(string name, out ushort value) => _values.TryGetValue(name, out value);

    public ushort? GetOrNull(string name) => _values.TryGetValue(name, out var value) ? value : null;

    public override string ToString() =>
        string.Join(" ", _values.Select(kvp => $"{kvp.Key}=${kvp.Value:X}"));
}

public sealed record ViceCpuHistoryEntry(
    ulong Clock,
    ViceRegisterSnapshot Registers,
    byte[] InstructionBytes);

public sealed record ViceDisplay(
    ushort DebugWidth,
    ushort DebugHeight,
    ushort XOffset,
    ushort YOffset,
    ushort InnerWidth,
    ushort InnerHeight,
    byte BitsPerPixel,
    byte[] Pixels);

public sealed record ViceInfo(byte[] VersionComponents, byte[] RevisionBytes)
{
    public string Version => string.Join('.', VersionComponents.Select(b => b.ToString()));

    public ulong? Revision
    {
        get
        {
            if (RevisionBytes.Length == 0 || RevisionBytes.Length > sizeof(ulong)) return null;
            ulong value = 0;
            for (var i = 0; i < RevisionBytes.Length; i++) value |= (ulong)RevisionBytes[i] << (8 * i);
            return value;
        }
    }
}

public sealed record ViceResourceValue(ViceResourceType Type, byte[] RawValue)
{
    public string Text => System.Text.Encoding.UTF8.GetString(RawValue);
}

public sealed record ViceRawResponse(
    byte ApiVersion,
    byte ResponseType,
    ViceErrorCode ErrorCode,
    uint RequestId,
    byte[] Body);

public sealed class ViceStoppedEventArgs(ushort programCounter) : EventArgs
{
    public ushort ProgramCounter { get; } = programCounter;
}

public sealed class ViceResumedEventArgs(ushort programCounter) : EventArgs
{
    public ushort ProgramCounter { get; } = programCounter;
}

public sealed class ViceJammedEventArgs(ushort programCounter) : EventArgs
{
    public ushort ProgramCounter { get; } = programCounter;
}

public sealed class ViceCheckpointEventArgs(ViceCheckpoint checkpoint) : EventArgs
{
    public ViceCheckpoint Checkpoint { get; } = checkpoint;
}

public sealed class ViceRegistersEventArgs(IReadOnlyList<ViceRegisterValue> registers) : EventArgs
{
    public IReadOnlyList<ViceRegisterValue> Registers { get; } = registers;
}

public sealed class ViceConnectionLostEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public sealed class ViceUnhandledResponseEventArgs(ViceRawResponse response) : EventArgs
{
    public ViceRawResponse Response { get; } = response;
}
