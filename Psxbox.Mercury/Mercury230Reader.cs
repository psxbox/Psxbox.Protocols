using Microsoft.Extensions.Logging;
using Psxbox.Streams;

namespace Psxbox.Mercury;

public class Mercury230Reader(IStream stream, ILogger? logger = null) : IReader
{
    public async Task<bool> Open(byte address, byte level, string password, bool passwordIsHex = false)
    {
        logger?.LogDebug("Open channel with address: {address}, level: {level}", address, level);

        var openPayload = QueryBuilder.GetOpenChannelBytes(address, level, password, passwordIsHex);
        byte[] response = [];
        for (int i = 0; i < 3; i++)
        {
            try
            {
                response = await SendAndGet(openPayload, 4);
                if (response[1] == 0x00 && QueryBuilder.CheckSumIsTrue(response)) return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error while Open: {ex}, Response: {resp}", ex.Message, BitConverter.ToString(response));
            }
        }
        return false;
    }

    public async Task Close(byte address)
    {
        logger?.LogDebug("Close channel with address: {address}", address);
        byte[] response = [];

        try
        {
            var closePayload = QueryBuilder.GetCloseChannelBytes(address);
            response = await SendAndGet(closePayload, 3);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error while Close: {ex}, Response: {resp}", ex.Message, BitConverter.ToString(response));
        }
    }

    /// <summary>
    /// Sends a payload to the stream and gets the response.
    /// </summary>
    /// <param name="payload">The payload to send.</param>
    /// <param name="bytesRead">The number of bytes to read from the stream.</param>
    /// <returns>The response from the stream.</returns>
    /// <exception cref="Exception">An error occurred while writing to or reading from the stream.</exception>
    private async Task<byte[]> SendAndGet(byte[] payload, int bytesRead)
    {
        if (logger?.IsEnabled(LogLevel.Debug) == true)
            logger?.LogDebug("Request: {payload}", BitConverter.ToString(payload));

        byte[] receivedData = new byte[bytesRead];
        int readed = 0;
        try
        {
            stream.Flush();
            await stream.WriteAsync(payload);
            readed = await stream.ReadAsync(receivedData);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error while SendAndGet: {ex}", ex.Message);
        }
        return receivedData[..readed];
    }

    /// <summary>
    /// Get phases values from meter.
    /// </summary>
    /// <param name="address">Address of the meter.</param>
    /// <param name="paramNumber">Number of the parameter.</param>
    /// <param name="bwri">Binary representation of the parameter.</param>
    /// <returns>Array of uint values.</returns>
    /// <exception cref="Exception">Throws exception if paramNumber is unknown parameter.</exception>
    private async Task<uint[]> GetPhasesValues(byte address, byte paramNumber, byte bwri)
    {
        byte[] resp = [];
        uint[] result = [];
        var req = QueryBuilder.GetCurrentValuesRequestBytes(address, paramNumber, bwri);
        var valueCount = paramNumber switch
        {
            0x11 => 1,
            0x14 or 0x16 => 3,
            _ => throw new Exception($"{paramNumber:X2} is unknown parameter")
        };
        for (int i = 0; i < 3; i++)
        {
            try
            {
                resp = await SendAndGet(req, valueCount * 3 + 3);

                logger?.LogDebug("Response: {resp}", BitConverter.ToString(resp));

                if (!QueryBuilder.CheckSumIsTrue(resp)) throw new Exception("CRC is mismatch");
                result = Parsers.ParsePhasesValues(paramNumber, resp[1..^2]);

                break;
            }
            catch (System.Exception ex)
            {
                logger?.LogError(ex, "Error while GetPhasesValues: {ex}, Response: {resp}", ex.Message, BitConverter.ToString(resp));
                if (i == 2) throw;
            }
        }
        return result;
    }

    public async Task<(float ab, float ac, float bc)> GetAngleOfUU(byte address)
    {
        return ((await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.AngleOfUU, 1)))[0] / 100f,
            (await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.AngleOfUU, 2)))[0] / 100f,
            (await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.AngleOfUU, 3)))[0] / 100f);
    }

    public async Task<(float a, float b, float c)> GetVoltages(byte address)
    {
        return ((await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.Voltage, 1)))[0] / 100f,
            (await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.Voltage, 2)))[0] / 100f,
            (await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.Voltage, 3)))[0] / 100f);
    }

    public async Task<(float a, float b, float c)> GetCurrents(byte address)
    {
        return ((await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.Current, 1)))[0] / 1000f,
            (await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.Current, 2)))[0] / 1000f,
            (await GetPhasesValues(address, 0x11, QueryBuilder.GenerateBWRI(BwriParam.Current, 3)))[0] / 1000f);
    }

    public async Task<float> GetFrequency(byte address)
    {
        return (await GetPhasesValues(address, 0x11, 0x40))[0] / 100f;
    }


    private async Task<(float sum, float a, float b, float c)> GetPower(
        byte address, BwriParam bwriParam, byte powerType, float scale = 0.01f, string byteOrder = "0123")
    {
        var req = QueryBuilder.GetCurrentValuesRequestBytes(address, 0x16, QueryBuilder.GenerateBWRI(bwriParam, powerType, 0));
        byte[] response = [];
        uint[] result = [0, 0, 0, 0];
        for (int j = 0; j < 3; j++)
        {
            try
            {
                response = await SendAndGet(req, 15);

                logger?.LogDebug("Response: {resp}", BitConverter.ToString(response));

                if (!QueryBuilder.CheckSumIsTrue(response)) throw new Exception("CRC is mismatch");
                var data = response[1..^2];
                for (var i = 0; i < 4; i++)
                {
                    data[i * 3] &= 0b0011_1111;
                }
                result = Parsers.ParseData(data, 3, 4, byteOrder);
                break;
            }
            catch (System.Exception ex)
            {
                logger?.LogError("Error while GetPower: {ex}, Response: {resp}", ex.Message, response);
                if (j == 2) throw;
            }
        }
        return (GetScaled(result[0], scale), GetScaled(result[1], scale), GetScaled(result[2], scale), GetScaled(result[3], scale));
    }

    public async Task<(float sum, float a, float b, float c)> GetPowerP(byte address) =>
        await GetPower(address, BwriParam.Power, PowerType.P.ToByte(), scale: 0.00001f);
    public async Task<(float sum, float a, float b, float c)> GetPowerQ(byte address) =>
        await GetPower(address, BwriParam.Power, PowerType.Q.ToByte(), scale: 0.00001f);
    public async Task<(float sum, float a, float b, float c)> GetPowerS(byte address) =>
        await GetPower(address, BwriParam.Power, PowerType.S.ToByte(), scale: 0.00001f);
    public async Task<(float avg, float a, float b, float c)> GetPowerFactor(byte address) =>
        await GetPower(address, BwriParam.PowerFactor, 0, 0.001f, byteOrder: "1032");

    private async Task<(float a1, float a2, float r1, float r2)> GetEnergy(
        byte address, byte code, byte array, byte month, byte tariff, float scale = 0.001f)
    {
        var req = QueryBuilder.GetEnergyRequestBytes(address, code, array, month, tariff);
        byte[] response = [];
        var result = (a1: 0f, a2: 0f, r1: 0f, r2: 0f);
        for (int j = 0; j < 3; j++)
        {
            try
            {
                response = await SendAndGet(req, 19);
                logger?.LogDebug("Response: {resp}", BitConverter.ToString(response));
                if (!QueryBuilder.CheckSumIsTrue(response)) throw new Exception("CRC is mismatch");
                var data = response[1..^2];
                var parsed = Parsers.ParseData(data, 4, 4, "1032");
                for (int i = 0; i < parsed.Length; i++)
                {
                    if (parsed[i] == uint.MaxValue) parsed[i] = 0;
                }

                result = (parsed[0] * scale, parsed[1] * scale, parsed[2] * scale, parsed[3] * scale);
                break;
            }
            catch (System.Exception ex)
            {
                logger?.LogError("Error while GetEnergy: {ex}, Response: {resp}", ex.Message, response);
                if (j == 2) throw;
            }
        }
        return result;
    }

    public async IAsyncEnumerable<(byte tarif, float a1, float a2, float r1, float r2)> GetLastEnergy(byte address)
    {
        for (byte tr = 0; tr < 5; tr++)
        {
            var energy = await GetEnergy(address, 0x5, 0x0, 0x0, tr);
            yield return (tr, energy.a1, energy.a2, energy.r1, energy.r2);
        }
    }

    private static float GetScaled(uint value, float scale)
    {
        if (value == uint.MaxValue) return 0;
        return value * scale;
    }

    public async IAsyncEnumerable<(DateOnly date, byte tarif, float v1, float v2, float v3, float v4)> GetArchive(
        byte address, ArchiveType archiveType, DateOnly from, DateOnly to)
    {
        var currDate = from;
        byte[] response = [];
        while (currDate <= to)
        {
            for (byte tr = 0; tr < 5; tr++)
            {
                var request = QueryBuilder.GetExtArrayRequestBytes(address, (byte)archiveType, currDate, tr);
                (uint v1, uint v2, uint v3, uint v4) result = default;
                bool noData = false;
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        response = await SendAndGet(request, 19);
                        logger?.LogDebug("Response: {resp}", BitConverter.ToString(response));

                        if (response.Length < 19 && response[1] == 0x01 && QueryBuilder.CheckSumIsTrue(response))
                        {
                            noData = true;
                            break;
                        }
                        result = Parsers.ParseExtArray(response);
                        break;
                    }
                    catch (System.Exception ex)
                    {
                        logger?.LogError(ex, "Error while GetArchive: {ex}, Response: {resp}", ex.Message, response);
                        if (i == 2) throw;
                    }
                }
                if (noData) continue;
                yield return (currDate, tr, GetScaled(result.v1, 0.001f), GetScaled(result.v2, 0.001f), GetScaled(result.v3, 0.001f), GetScaled(result.v4, 0.001f));
            }
            if (archiveType == ArchiveType.BeginOfDay || archiveType == ArchiveType.BeginOfDayQuadrant)
            {
                currDate = currDate.AddDays(1);
            }
            else
            {
                currDate = currDate.AddMonths(1);
            }
        }
    }

    public Task ReadWatch(byte address)
    {
        throw new NotImplementedException();
    }
}
