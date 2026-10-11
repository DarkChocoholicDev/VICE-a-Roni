namespace VICEBinaryMonitor;

public sealed class ViceMonitorException : Exception
{
    public ViceMonitorException(ViceErrorCode errorCode, byte responseType, uint requestId)
        : base($"VICE Binary Monitor error 0x{(byte)errorCode:X2} ({errorCode}) " +
               $"for response 0x{responseType:X2}, request 0x{requestId:X8}.")
    {
        ErrorCode = errorCode;
        ResponseType = responseType;
        RequestId = requestId;
    }

    public ViceErrorCode ErrorCode { get; }
    public byte ResponseType { get; }
    public uint RequestId { get; }
}
