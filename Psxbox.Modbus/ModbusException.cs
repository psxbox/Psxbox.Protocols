namespace Psxbox.Modbus;

public sealed class ModbusException : Exception
{
    public ModbusException(byte functionCode, byte exceptionCode, string message)
        : base(message)
    {
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }

    public byte FunctionCode { get; }
    public byte ExceptionCode { get; }
}
