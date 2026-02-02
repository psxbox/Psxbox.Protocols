using Psxbox.Streams;

namespace Psxbox.Modbus;

public static class ModbusHelpers
{
    public const byte ReadCoilsFunc = 0x01;
    public const byte ReadDiscreteInputsFunc = 0x02;
    public const byte ReadHoldingRegistersFunc = 0x03;
    public const byte ReadInputRegistersFunc = 0x04;
    public const byte WriteSingleCoilFunc = 0x05;
    public const byte WriteSingleRegisterFunc = 0x06;
    public const byte WriteMultipleCoilsFunc = 0x0F;
    public const byte WriteMultipleRegistersFunc = 0x10;


    public static async Task<byte> ReadSlaveId(IStream stream, ushort slaveId)
    {
        var slave = await stream.ReadAsync();

        if (slave != slaveId)
            throw new Exception($"Invalid slave id, expected: {slaveId}, actual: {slave}");
        return slave;
    }

    public static async Task<byte> ReadFunction(IStream stream, byte requestFunc)
    {
        var actualFunc = await stream.ReadAsync();
        if (actualFunc != requestFunc)
        {
            var msg = $"Invalid function code, expected: {requestFunc}, actual: {actualFunc}";

            if (HasErrorResponse(requestFunc, actualFunc))
            {
                var errCode = await stream.ReadAsync();
                string errMsg = ParseErrorCode(errCode);

                msg += $" - {errMsg}";
            }

            throw new Exception(msg);
        }

        return actualFunc;
    }

    public static string ParseErrorCode(byte errCode)
    {
        return errCode switch
        {
            0x01 => "Illegal function",
            0x02 => "Illegal data address",
            0x03 => "Illegal data value",
            0x04 => "Slave device failed to respond",
            _ => $"Unknown error code {errCode}"
        };
    }

    public static bool HasErrorResponse(byte functionCode, byte actualFunc) => functionCode + 0x80 == actualFunc;

    public static void CheckResponseLength(byte[] response)
    {
        if (response[2] != response.Length - 3)
            throw new Exception($"Invalid response length, expected: {response[2] + 3}, actual: {response.Length}");
    }

    private static void CheckWritedAddress(byte[] response, ushort address)
    {
        var actualAddress = (ushort)(response[3] | (response[2] << 8));

        if (actualAddress != address)
            throw new Exception($"Invalid response address, expected: {address:X4}, actual: {actualAddress:X4}");
    }

    private static void CheckWritedValue(byte[] response, ushort value)
    {
        var actualValue = (ushort)(response[5] | (response[4] << 8));

        if (actualValue != value)
            throw new Exception($"Invalid response value, expected: {value:X4}, actual: {actualValue:X4}");
    }

    public static List<byte> FillRequest(byte slaveId, byte functionCode, ushort startAddress,
        ushort countOrValue, object[]? values = null)
    {
        var request = new List<byte>(5)
        {
            slaveId,
            functionCode,
            (byte)((startAddress >> 8) & 0xFF),
            (byte)(startAddress & 0xFF),
            (byte)((countOrValue >> 8) & 0xFF),
            (byte)(countOrValue & 0xFF)
        };

        if (functionCode is WriteMultipleCoilsFunc or WriteMultipleRegistersFunc)
        {
            if (values == null)
                throw new Exception("Invalid values array");

            int quantity = countOrValue * 2;

            if (functionCode == WriteMultipleCoilsFunc)
            {
                quantity = countOrValue / 8 + countOrValue % 8 > 0 ? 1 : 0;
            }

            if (quantity == 0 || (values.Length * 2) != quantity)
                throw new Exception("Invalid values array length");

            request.Add((byte)(quantity & 0xFF));

            for (int i = 0; i < values.Length; i++)
            {
                if (functionCode == WriteMultipleCoilsFunc)
                {
                    request.Add((byte)(((byte)values[i] >> 8) & 0xFF));
                }
                else
                {
                    ushort value = (ushort)values[i];
                    request.Add((byte)(value >> 8));
                    request.Add((byte)(value & 0xFF));
                }
            }
        }

        return request;
    }

    public static byte[] ParseReadCoilsResponse(byte[] response)
    {
        CheckResponseLength(response);
        return response[3..];
    }

    public static byte[] ParseReadHoldingRegistersResponse(byte[] response)
    {
        CheckResponseLength(response);
        return response[3..];
    }

    public static void ParseWriteSingleRegisterResponse(byte[] response, ushort address, ushort value)
    {
        CheckWritedAddress(response, address);
        CheckWritedValue(response, value);
    }

    public static byte[] ParseReadDiscreteInputsResponse(byte[] response)
    {
        CheckResponseLength(response);
        return response[3..];
    }

    public static byte[] ParseReadInputRegistersResponse(byte[] response)
    {
        CheckResponseLength(response);
        return response[3..];
    }

    public static void ParseWriteSingleCoilResponse(byte[] response, ushort address, bool value)
    {
        CheckWritedAddress(response, address);
        CheckWritedValue(response, (ushort)(value ? 0xFF00 : 0x00));
    }

    public static void ParseWriteMultipleCoilsResponse(byte[] response, ushort startAddress, ushort count)
    {
        CheckWritedAddress(response, startAddress);
        CheckWritedValue(response, count);
    }

    public static void ParseWriteMultipleRegistersResponse(byte[] response, ushort startAddress, ushort count)
    {
        CheckWritedAddress(response, startAddress);
        CheckWritedValue(response, count);
    }

    public static ushort[] BytesToUshorts(byte[] bytes) => bytes
        .Chunk(2)
        .Select(b => (ushort)(b[1] | (b[0] << 8)))
        .ToArray();


    /// <summary>
    /// Converts two bytes to a 16-bit unsigned integer, interpreting the bytes as a big-endian value regardless of
    /// system endianness.
    /// </summary>
    /// <remarks>This method always interprets the input bytes as big-endian, ensuring consistent results
    /// across platforms with different endianness.</remarks>
    /// <param name="b1">The first byte of the big-endian representation. This byte provides the most significant 8 bits of the result.</param>
    /// <param name="b2">The second byte of the big-endian representation. This byte provides the least significant 8 bits of the result.</param>
    /// <returns>A 16-bit unsigned integer constructed from the specified bytes, with <paramref name="b1"/> as the high byte and
    /// <paramref name="b2"/> as the low byte.</returns>
    public static ushort ConvertToUshort(byte b1, byte b2)
    {
        var (a, b) = BitConverter.IsLittleEndian ? (b1, b2) : (b2, b1);

        return (ushort)((a << 8) | b);
    }
}
