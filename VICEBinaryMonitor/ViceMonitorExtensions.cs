using System.Buffers.Binary;

namespace VICEBinaryMonitor;

public static class ViceMonitorExtensions
{
    public static async Task<byte> ReadByteAsync(this IViceMonitorClient vice, ushort address,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main, ushort bankId = 0,
        bool sideEffects = false, CancellationToken cancellationToken = default)
    {
        var data = await vice.ReadMemoryAsync(address, address, memorySpace, bankId, sideEffects, cancellationToken)
            .ConfigureAwait(false);
        return data[0];
    }

    public static async Task<ushort> ReadUInt16Async(this IViceMonitorClient vice, ushort address,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main, ushort bankId = 0,
        bool sideEffects = false, CancellationToken cancellationToken = default)
    {
        if (address == ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(address));
        var data = await vice.ReadMemoryAsync(address, (ushort)(address + 1), memorySpace, bankId, sideEffects, cancellationToken)
            .ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    public static Task WriteByteAsync(this IViceMonitorClient vice, ushort address, byte value,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main, ushort bankId = 0,
        bool sideEffects = false, CancellationToken cancellationToken = default) =>
        vice.WriteMemoryAsync(address, new[] { value }, memorySpace, bankId, sideEffects, cancellationToken);

    public static Task WriteUInt16Async(this IViceMonitorClient vice, ushort address, ushort value,
        ViceMemorySpace memorySpace = ViceMemorySpace.Main, ushort bankId = 0,
        bool sideEffects = false, CancellationToken cancellationToken = default)
    {
        if (address == ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(address));
        var data = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        return vice.WriteMemoryAsync(address, data, memorySpace, bankId, sideEffects, cancellationToken);
    }
}
