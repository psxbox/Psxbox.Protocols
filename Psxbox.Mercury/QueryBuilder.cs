using System.Globalization;
using System.Text;

namespace Psxbox.Mercury;

public class QueryBuilder
{
    /// <summary>
    /// Calculates the CRC16 of the given byte array.
    /// </summary>
    /// <param name="data">The byte array to calculate the CRC16 for.</param>
    /// <returns>The CRC16 result of the given byte array.</returns>
    public static ushort ComputeCRC(byte[] data)
    {
        ushort crc = 0xFFFF; // Инициализируем значение CRC

        foreach (byte b in data)
        {
            crc ^= b; // XOR с байтом данных

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001; // Полином MODBUS
                }
                else
                {
                    crc >>= 1;
                }
            }
        }

        // Меняем байты местами для соответствия спецификации MODBUS
        crc = (ushort)((crc << 8) | (crc >> 8));

        return crc;
    }

    /// <summary>
    /// Converts a string password to a byte array, either in ASCII or
    /// hexadecimal representation.
    /// </summary>
    /// <param name="password">The password to convert to bytes.</param>
    /// <param name="isHex">Whether the password is in hexadecimal format.
    /// If true, each two characters in the password will be converted to a
    /// single byte. Otherwise, the password will be converted to bytes using
    /// the ASCII encoding.</param>
    /// <returns>The byte representation of the password.</returns>
    public static byte[] PasswordToBytes(string password, bool isHex = false)
    {
        if (isHex)
        {
            return Enumerable.Range(0, password.Length)
                .Select(x => Convert.ToByte(password.Substring(x, 1), 16))
                .ToArray();
        }
        else
        {
            return Encoding.ASCII.GetBytes(password);
        }
    }

    /// <summary>
    /// Generates the byte array for opening the connection channel with the specified address, level, and password.
    /// </summary>
    /// <param name="address">The device address.</param>
    /// <param name="level">The security level for the channel.</param>
    /// <param name="password">The password for the channel, as a string.</param>
    /// <param name="passwordIsHex">Indicates whether the password is in hexadecimal format.</param>
    /// <returns>The byte array representing the open channel command with CRC appended.</returns>
    public static byte[] GetOpenChannelBytes(byte address, byte level, string password, bool passwordIsHex = false)
    {
        byte[] passwordBytes = PasswordToBytes(password, passwordIsHex);
        byte[] payload = [address, 0x01, level, .. passwordBytes];
        var crc = ComputeCRC(payload);

        return [.. payload, (byte)(crc >> 8), (byte)(crc & 0xFF)];
    }

    /// <summary>
    /// Generates the byte array for closing the connection channel with the specified address.
    /// </summary>
    /// <param name="address">The device address.</param>
    /// <returns>The byte array representing the close channel command with CRC appended.</returns>
    public static byte[] GetCloseChannelBytes(byte address)
    {
        var crc = ComputeCRC([address, 0x02]);

        return [address, 0x02, (byte)(crc >> 8), (byte)(crc & 0xFF)];
    }

    /// <summary>
    /// Generates the byte array for testing the connection channel with the specified address.
    /// </summary>
    /// <param name="address">The device address.</param>
    /// <returns>The byte array representing the test channel command with CRC appended.</returns>
    public static byte[] GetTestChannelBytes(byte address)
    {
        var crc = ComputeCRC([address, 0x00]);

        return [address, 0x00, (byte)(crc >> 8), (byte)(crc & 0xFF)];
    }

    /// <summary>
    /// Checks if the given message has a valid CRC at the end.
    /// </summary>
    /// <param name="msg">The message to check.</param>
    /// <returns><c>true</c> if the CRC is valid, <c>false</c> otherwise.</returns>
    public static bool CheckSumIsTrue(byte[] msg)
    {
        ushort crc = ComputeCRC(msg[0..^2]);
        ushort actualCrc = (ushort)((msg[^2] << 8) | msg[^1]);
        return actualCrc == crc;
    }

    /// <summary>
    /// Get request parameter bytes
    /// </summary>
    /// <param name="address">Device address</param>
    /// <param name="param">Parameter number</param>
    /// <returns>Request bytes</returns>
    public static byte[] GetParamRequestBytes(byte address, byte param)
    {
        var crc = ComputeCRC([address, 0x04, param]);

        return [address, 0x04, param, (byte)(crc >> 8), (byte)(crc & 0xFF)];
    }


    /// <summary>
    /// Расширенные массивы суточных и месячных профилей
    /// </summary>
    /// <param name="address">Сетевой адрес</param>
    /// <param name="archiveType">№ массива: <br/>
    ///      0h - A+, A-, R+, R- на начало заданных суток<br/>
    ///      1h - A+, A-, R+, R- на начало заданного месяца<br/>
    ///      2h - R1, R2, R3, R4 на начало заданных суток<br/>
    ///      3h - R1, R2, R3, R4 на начало заданного месяца</param>
    /// <param name="date">дата</param>
    /// <param name="tarif">№ тарифа: <br/>
    ///      0h - сумма, 1h - тариф 1, 2h - тариф 2, 3h - тариф 3, 4h - тариф 4</param>
    /// <returns></returns>
    public static byte[] GetExtArrayRequestBytes(byte address, byte archiveType, DateOnly date, byte tarif)
    {
        byte[] req = [ address, 0x18, archiveType,
        byte.Parse(date.ToString("dd"), NumberStyles.HexNumber),
        byte.Parse(date.ToString("MM"), NumberStyles.HexNumber),
        byte.Parse(date.ToString("yy"), NumberStyles.HexNumber), tarif ];
        var crc = ComputeCRC(req);

        return [.. req, (byte)(crc >> 8), (byte)(crc & 0xFF)];
    }


    /// <summary>
    /// Generate BWRI parameter for Mercury230 reader
    /// </summary>
    /// <param name="bwriParam">BWRI parameter</param>
    /// <param name="p1">First part of parameter</param>
    /// <param name="p2">Second part of parameter</param>
    /// <returns>Generated parameter</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="p2"/> is null and <paramref name="bwriParam"/> is 0</exception>
    public static byte GenerateBWRI(BwriParam bwriParam, byte p1, byte? p2 = null)
    {
        var parameter = (byte)bwriParam;
        if (parameter == 0 && p2 == null) throw new ArgumentNullException(nameof(p2), $"{nameof(p2)} must be not null when {nameof(parameter)} is 0");
        byte part2 = parameter == 0 ? (byte)(p1 << 2 | (p2! & 0b11)) : p1;
        return (byte)(parameter << 4 | (part2 & 0b1111));
    }

    /// <summary>
    /// Generate the request bytes for getting current values
    /// </summary>
    /// <param name="address">Device address</param>
    /// <param name="paramNumber">Parameter number</param>
    /// <param name="bwri">BWRI parameter</param>
    /// <returns>Request bytes</returns>
    public static byte[] GetCurrentValuesRequestBytes(byte address, byte paramNumber, byte bwri)
    {
        byte[] req = [address, 0x08, paramNumber, bwri];
        var crc = ComputeCRC(req);

        return [.. req, (byte)(crc >> 8), (byte)(crc & 0xFF)];
    }

    /// <summary>
    /// Generate the request bytes for getting energy values
    /// </summary>
    /// <param name="address">Device address</param>
    /// <param name="code">Code of request</param>
    /// <param name="array">Array number</param>
    /// <param name="month">Month number</param>
    /// <param name="tariff">Tariff number</param>
    /// <returns>Request bytes</returns>
    public static byte[] GetEnergyRequestBytes(byte address, byte code, byte array, byte month, byte tariff)
    {
        byte[] req = [address, code, (byte)(array << 4 | month & 0b1111), tariff];
        var crc = ComputeCRC(req);

        return [.. req, (byte)(crc >> 8), (byte)(crc & 0xFF)];
    }
}
