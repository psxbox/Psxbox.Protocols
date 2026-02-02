using Psxbox.Streams;
using Psxbox.Utils;
using System.Runtime.InteropServices;
using System.Text;

namespace Psxbox.Modbus;

public class ModbusAsciiWrapper : IModbusWrapper
{
    private readonly byte[] END = [(byte)'\r', (byte)'\n'];

    public (byte[] request, int responseSize, ushort transactionId) BuildReadCoilsRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadCoilsFunc, startAddress, count);
        return (request, (int)Math.Ceiling(count / 8.0) * 2 + 9, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadDiscreteInputsRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadDiscreteInputsFunc, startAddress, count);
        return (request, (int)Math.Ceiling(count / 8.0) * 2 + 9, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadHoldingRegistersRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadHoldingRegistersFunc, startAddress, count);
        return (request, count * 2 + 9, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildReadInputRegistersRequest(byte slaveId, ushort startAddress, ushort count)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.ReadInputRegistersFunc, startAddress, count);
        return (request, count * 2 + 9, 0);
    }

    public byte[] BuildRequest(byte slaveId, byte functionCode, ushort startAddress, ushort countOrValue, object[]? values = null)
    {
        var request = ModbusHelpers.FillRequest(slaveId, functionCode, startAddress, countOrValue, values);

        var sb = new StringBuilder();
        sb.Append(':');

        foreach (var t in request)
        {
            sb.Append(t.ToString("X2"));
        }

        var lrc = Calculators.LRC(CollectionsMarshal.AsSpan(request), 0, request.Count);
        sb.Append(lrc.ToString("X2"));
        sb.Append("\r\n");

        return Encoding.ASCII.GetBytes(sb.ToString(), 0, sb.Length);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleCoilsRequest(byte slaveId, ushort startAddress, ushort count, byte[] values)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.WriteMultipleCoilsFunc, startAddress, count,
            values.Select(x => (object)x).ToArray());
        return (request, 17, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteMultipleRegistersRequest(byte slaveId, ushort startAddress, ushort count, ushort[] values)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.WriteMultipleRegistersFunc, startAddress, count,
            values.Select(x => (object)x).ToArray());
        return (request, 17, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleCoilRequest(byte slaveId, ushort address, bool value)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.WriteSingleCoilFunc, address, (ushort)(value ? 0xFF00 : 0x00));
        return (request, 17, 0);
    }

    public (byte[] request, int responseSize, ushort transactionId) BuildWriteSingleRegisterRequest(byte slaveId, ushort address, ushort value)
    {
        var request = BuildRequest(slaveId, ModbusHelpers.WriteSingleRegisterFunc, address, value);
        return (request, 17, 0);
    }

    public void CheckChecksum(byte[] response)
    {
        var lrc = Calculators.LRC(response, 0, response.Length - 1);
        if (lrc != response[^1])
            throw new Exception($"Invalid LRC, calculated: {lrc:X2}, actual: {response[^1]}");
    }

    public async Task<byte[]> ReadResponse(IStream stream, byte slaveId, byte func, int responseSize, ushort transactionId)
    {
        await stream.ReadUntilAsync(':').ConfigureAwait(false);
        var response = await stream.ReadUntilAsync(END).ConfigureAwait(false);
        var respBytes = Converters.HexStringToByteArray(Encoding.ASCII.GetString(response[..^2]));
        var actualFunc = respBytes[1];

        if (func != actualFunc)
        {
            var msg = $"Invalid function code, expected: {func}, actual: {actualFunc}";

            if (ModbusHelpers.HasErrorResponse(func, actualFunc))
            {
                var errCode = respBytes[2];

                msg += $" - {ModbusHelpers.ParseErrorCode(errCode)}";
            }

            throw new Exception(msg);
        }

        CheckChecksum(respBytes);

        respBytes[2] = (byte)(respBytes[2] / 2);

        return respBytes[..^1];
    }
}