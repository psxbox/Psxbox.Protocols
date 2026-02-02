using Psxbox.Streams;

namespace Psxbox.Modbus;

public class ModbusTCPWrapper : IModbusWrapper
{
    ushort _transactionId = 0;

    public (byte[] request, int responseSize, ushort transactionId) BuildReadCoilsRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadCoilsFunc, startAddress, count);
        return (request, (int)Math.Ceiling(count / 8.0) + 9, _transactionId);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadDiscreteInputsRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadDiscreteInputsFunc, startAddress, count);
        return (request, (int)Math.Ceiling(count / 8.0) + 9, _transactionId);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadHoldingRegistersRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadHoldingRegistersFunc, startAddress, count);
        return (request, count * 2 + 9, _transactionId);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadInputRegistersRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadInputRegistersFunc, startAddress, count);
        return (request, count * 2 + 9, _transactionId);
    }

    public byte[] BuildRequest(byte slaveId, byte functionCode, ushort startAddress, ushort countOrValue, object[]? values = null)
    {
        var request = ModbusHelpers.FillRequest(slaveId, functionCode, startAddress, countOrValue, values);
        var length = request.Count;

        _transactionId++;
        byte[] mbap = BuildMBAP(_transactionId, length);

        return [.. mbap, .. request];
    }

    private static byte[] BuildMBAP(ushort transactionId, int length)
    {
        return [
            (byte)(transactionId >> 8),
            (byte)(transactionId & 0xFF),
            0x00,
            0x00,
            (byte)(length >> 8),
            (byte)(length & 0xFF)
        ];
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleCoilsRequest(
        byte slaveId, ushort startAddress, ushort count, byte[] values)
    {
        throw new NotImplementedException();
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleRegistersRequest(
        byte slaveId, ushort startAddress, ushort count, ushort[] values)
    {
        throw new NotImplementedException();
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleCoilRequest(byte slaveId, ushort address, bool value)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.WriteSingleCoilFunc, address, (ushort)(value ? 0xFF00 : 0x00));
        return (request, 12, _transactionId);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleRegisterRequest(byte slaveId, ushort address, ushort value)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.WriteSingleRegisterFunc, address, value);
        return (request, 12, _transactionId);
    }

    public void CheckChecksum(byte[] response)
    {
        // pass
    }

    public async Task<byte[]> ReadResponse(IStream stream, byte slaveId, byte func, int responseSize, ushort transactionId)
    {
        byte[] transactionIdBytes = [
            (byte)(transactionId >> 8),
            (byte)(transactionId & 0xFF),
            00,
            00
        ];

        var head = await stream.ReadUntilAsync(transactionIdBytes);

        if (head.Length != transactionIdBytes.Length)
        {
            throw new Exception("Invalid transaction id");
        }

        ushort length = (ushort)((await stream.ReadAsync().ConfigureAwait(false)) << 8 | (await stream.ReadAsync().ConfigureAwait(false)));
        var slaveIdRead = await ModbusHelpers.ReadSlaveId(stream, slaveId);
        var funcRead = await ModbusHelpers.ReadFunction(stream, func);

        byte[] response = new byte[length - 2];
        int readedBytes = await stream.ReadAsync(response).ConfigureAwait(false);
        if (readedBytes != length - 2)
        {
            throw new Exception($"Invalid response length, expected: {length + 6}, actual: {readedBytes + 8}");
        }

        return [slaveIdRead, funcRead, .. response];
    }
}