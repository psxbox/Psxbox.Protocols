using Psxbox.Streams;

namespace Psxbox.Modbus;

public interface IModbusWrapper
{
    (byte[] request, int responseSize, ushort transactionId) BuildReadCoilsRequest(byte slaveId, ushort startAddress, ushort count);
    (byte[] request, int responseSize, ushort transactionId) BuildReadDiscreteInputsRequest(byte slaveId, ushort startAddress, ushort count);
    (byte[] request, int responseSize, ushort transactionId) BuildReadHoldingRegistersRequest(byte slaveId, ushort startAddress, ushort count);
    (byte[] request, int responseSize, ushort transactionId) BuildReadInputRegistersRequest(byte slaveId, ushort startAddress, ushort count);
    (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleCoilsRequest(byte slaveId, ushort startAddress, ushort count, byte[] values);
    (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleRegistersRequest(byte slaveId, ushort startAddress, ushort count, ushort[] values);
    (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleCoilRequest(byte slaveId, ushort address, bool value);
    (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleRegisterRequest(byte slaveId, ushort address, ushort value);
    byte[] BuildRequest(byte slaveId, byte functionCode, ushort startAddress, ushort countOrValue, object[]? values = null);
    Task<byte[]> ReadResponse(IStream stream, byte slaveId, byte func, int responseSize, ushort transactionId);
    void CheckChecksum(byte[] response);
}
