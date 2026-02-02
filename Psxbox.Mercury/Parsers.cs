namespace Psxbox.Mercury;

public static class Parsers
{
    public static DateTimeOffset ParseDateTime(byte[] payload)
    {
        var value = BitConverter.ToString(payload);
        var timeOnly = TimeOnly.ParseExact(value[3..11], "ss-mm-HH");
        var dateOnly = DateOnly.ParseExact(value[15..23], "dd-MM-yy");
        return new DateTimeOffset(dateOnly, timeOnly, TimeSpan.FromHours(5));
    }

    public static uint ConvertToUint32(byte[] b, int startIndex = 0, string byteOrder = "1032")
    {
        var order = byteOrder.Select(ch => int.Parse(ch.ToString())).ToArray();
        return (uint)(b[startIndex + order[0]] << 24 | b[startIndex + order[1]] << 16 | b[startIndex + order[2]] << 8 | b[startIndex + order[3]]);
    }

    // Парсинг Расширенные массивы суточных и месячных профилей
    public static (uint v1, uint v2, uint v3, uint v4) ParseExtArray(byte[] payload, bool checkCrc = true)
    {
        if (checkCrc && !QueryBuilder.CheckSumIsTrue(payload))
        {
            throw new Exception($"CRC mismatch");
        }

        return (ConvertToUint32(payload, 1), ConvertToUint32(payload, 5), ConvertToUint32(payload, 9), ConvertToUint32(payload, 13));
    }

    public static uint[] ParseData(byte[] data, int bytesPerValue, int count, string byteOrder = "1032")
    {
        List<uint> result = new();
        byte[] s = new byte[4];
        for (var i = 0; i < count; i++)
        {
            Array.Fill<byte>(s, 0);
            Array.Copy(data, i * bytesPerValue, s, 4 - bytesPerValue, bytesPerValue);
            result.Add(ConvertToUint32(s, byteOrder: byteOrder));
        }

        return result.ToArray();
    }

    // Парсинг текуших фазних значений (V, A, Angle of phases)
    public static uint[] ParsePhasesValues(byte paramNumber, byte[] data)
    {
        var valueCount = paramNumber switch
        {
            0x11 => 1,
            0x14 or 0x16 => 3,
            _ => throw new Exception($"{paramNumber:X2} is unknown parameter")
        };

        return ParseData(data, 3, valueCount);
    }
}
