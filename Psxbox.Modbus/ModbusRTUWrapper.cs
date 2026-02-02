using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using Psxbox.Streams;
using Psxbox.Utils;

namespace Psxbox.Modbus;

public class ModbusRTUWrapper : IModbusWrapper
{
    public (byte[] request, int responseSize, ushort transactionId) BuildReadCoilsRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadCoilsFunc, startAddress, count);

        return (request, (int)Math.Ceiling(count / 8.0) + 5, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadHoldingRegistersRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadHoldingRegistersFunc, startAddress, count);

        return (request, count * 2 + 5, 0);
    }

    public byte[] BuildRequest(byte slaveId, byte functionCode, ushort startAddress, ushort countOrValue, object[]? values = null)
    {
        // Build the request
        List<byte> request = ModbusHelpers.FillRequest(slaveId, functionCode, startAddress, countOrValue, values);

        // Calculate the CRC
        ushort crc = Calculators.CalcModbusCRC(CollectionsMarshal.AsSpan(request));

        // Add the CRC
        request.Add((byte)(crc >> 8));
        request.Add((byte)(crc & 0xFF));

        return [.. request];
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleRegisterRequest(byte slaveId, ushort address, ushort value)
    {
        // Build the request
        var request = BuildRequest(slaveId, ModbusHelpers.WriteSingleRegisterFunc, address, value);

        return (request, 8, 0);
    }

    public void CheckChecksum(byte[] bytes)
    {
        ushort crc = (ushort)(bytes[^1] | (bytes[^2] << 8));
        ushort calculatedCrc = Calculators.CalcModbusCRC(bytes[..^2]);

        if (crc != calculatedCrc)
            throw new Exception($"Invalid CRC, expected: {crc:X4}, actual: {calculatedCrc:X4}");
    }

    public async Task<byte[]> ReadResponse(IStream stream, byte slaveId, byte requestFunc, int responseSize, ushort transactionId)
    {
        // First read one byte to get slave id
        byte slave = await ModbusHelpers.ReadSlaveId(stream, slaveId);

        // Second read one byte to get function code
        var func = await ModbusHelpers.ReadFunction(stream, requestFunc);

        byte[] lastBytes = new byte[responseSize - 2]; // Adjust size as needed
        int readedBytes = await stream.ReadAsync(lastBytes).ConfigureAwait(false);
        if (readedBytes != responseSize - 2)
            throw new Exception($"Invalid response length, expected: {responseSize - 2}, actual: {readedBytes}");
        byte[] response = [slave, func, .. lastBytes];

        CheckChecksum(response); // Check CRC

        return response[..^2];
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadDiscreteInputsRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadDiscreteInputsFunc, startAddress, count);

        return (request, (int)Math.Ceiling(count / 8.0) + 5, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadInputRegistersRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadInputRegistersFunc, startAddress, count);

        return (request, count * 2 + 5, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleCoilRequest(byte slaveId, ushort address, bool value)
    {
        // Build the request
        byte[] request = BuildRequest(slaveId, ModbusHelpers.WriteSingleCoilFunc, address, (ushort)(value ? 0xFF00 : 0x00));

        return (request, 8, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleCoilsRequest(
        byte slaveId, ushort startAddress, ushort count, byte[] values)
    {
        // Build the request
        byte[] request = BuildRequest(slaveId, ModbusHelpers.WriteMultipleCoilsFunc, startAddress, count,
            values.Select(x => (object)x).ToArray());

        return (request, 8, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleRegistersRequest(
        byte slaveId, ushort startAddress, ushort count, ushort[] values)
    {
        // Build the request
        byte[] request = BuildRequest(slaveId, ModbusHelpers.WriteMultipleRegistersFunc, startAddress, count,
            values.Select(x => (object)x).ToArray());

        return (request.ToArray(), 8, 0);
    }
}