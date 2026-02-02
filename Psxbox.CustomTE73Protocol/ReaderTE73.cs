using Psxbox.Streams;
using Psxbox.Utils;

namespace Psxbox.CustomTE73Protocol;

public class ReaderTE73(IStream stream)
{
    private readonly IStream stream = stream;

    /// <summary>
    /// Registerdan qiymatni o'qib olish
    /// </summary>
    /// <param name="id">Meter ID</param>
    /// <param name="register">Register</param>
    /// <returns>Integer qiymat string sifatida</returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> ReadDataAsync(string id, string register, bool ignoreLastBit = false)
    {
        var meterId = NumbersToBCD(id).Reverse().ToArray();
        var request = GetRequestPackage(register, meterId);

        // SENDING REQUEST
        stream.Flush();
        await stream.WriteAsync(request);

        List<byte> response =
        [
            // 0x68
            (await stream.ReadUntilAsync(0x68).ConfigureAwait(false))[^1]
        ];

        // Meter ID
        byte[] respMeterId = new byte[meterId.Length];

        await stream.ReadAsync(respMeterId).ConfigureAwait(false);

        if (!meterId.SequenceEqual(respMeterId))
        {
            throw new Exception("Meter ID bir xil emas");
        }
        response.AddRange(respMeterId);

        // 0x68
        var terminator = await stream.ReadUntilAsync(0x68).ConfigureAwait(false);
        response.Add(terminator[^1]);

        // CMD
        byte cmd = await stream.ReadAsync().ConfigureAwait(false);
        if (cmd != 0x91)
        {
            throw new Exception($"Javob funksiya codi: {cmd:X2}");
        }
        response.Add(cmd);

        // DATA COUNT
        byte dataCount = await stream.ReadAsync().ConfigureAwait(false);
        response.Add(dataCount);

        // DATA
        byte[] data = new byte[dataCount];
        await stream.ReadAsync(data).ConfigureAwait(false);
        var respRegister = data[0..4];
        if (!EncodeValue(register).SequenceEqual(respRegister))
        {
            throw new Exception("Savol registeri javob registeri bilan bir xil emas");
        }
        response.AddRange(data);

        //Console.WriteLine(BitConverter.ToString(response.ToArray()));

        // CRC
        byte crc = await stream.ReadAsync().ConfigureAwait(false);
        byte calculatedCrc = Calculators.Modulo256(response);
        if (crc != calculatedCrc)
        {
            throw new Exception($"Kelgan CRC {crc:X2} hisoblangan CRC {calculatedCrc:X2}" +
                $" bilan bir xil emas!");
        }

        // 0x16
        byte endByte = await stream.ReadAsync().ConfigureAwait(false);
        if (endByte != 0x16)
        {
            throw new Exception($"Javob oxiri bayti 0x16 emas, balki {endByte:X2}");
        }

        return DecodeValue(data[4..], ignoreLastBit);
    }

    private static byte[] GetRequestPackage(string register, byte[] meterId)
    {
        List<byte> request = new()
        {
            0x68
        };

        request.AddRange(meterId);
        request.Add(0x68);
        request.Add(0x11);
        request.Add(0x04);
        request.AddRange(EncodeValue(register));
        request.Add(Calculators.Modulo256(request));
        request.Add(0x16);

        return request.ToArray();
    }

    private static IEnumerable<byte> NumbersToBCD(string id)
    {
        var result = Enumerable.Range(0, id.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(id.Substring(x, 2), 16));
        return result;
    }

    public static string DecodeValue(byte[] bytes, bool ignoreLastBit = false)
    {
        // Explicit Enumerable.Reverse to avoid binding to Array.Reverse (void)
        var reversed = Enumerable.Reverse(bytes).ToArray();
        var value = BitConverter.ToString(reversed)
            .Replace("-", "")
            .Select(DecodeChar)
            .ToArray();

        if (ignoreLastBit && value.Length > 0)
        {
            value[0] = (byte)(value[0] & 0b0111);
        }

        return string.Join("", value);
    }

    public static byte[] EncodeValue(string value)
    {
        if (value.Length % 2 != 0)
            throw new ArgumentException("Qiymat uzunligi 2 ga bo'linishi kerak");

        var encoded = string.Join("", value.Select(EncodeChar));

        var result = Enumerable.Range(0, encoded.Length)
                        .Where(x => x % 2 == 0)
                        .Select(x => Convert.ToByte(encoded.Substring(x, 2), 16))
                        .Reverse()
                        .ToArray();
        return result;
    }

    private static string EncodeChar(char c)
    {
        return (int.Parse(c.ToString()) + 3).ToString("X1");
    }

    private static int DecodeChar(char c)
    {
        return int.Parse(c.ToString(), System.Globalization.NumberStyles.AllowHexSpecifier) - 3;
    }
}
