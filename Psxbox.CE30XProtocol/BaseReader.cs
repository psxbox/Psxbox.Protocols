using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using Psxbox.Utils.Helpers;
using System.Globalization;

namespace Psxbox.CE30XProtocol;

public abstract class BaseReader(IStream stream, string id, string password = "777777", ILogger? logger = null) : IDisposable
{
    protected readonly IStream stream = stream;
    protected readonly string id = id;
    protected readonly string password = password;
    protected readonly ILogger? logger = logger;
    public string ID => id;
    private bool disposedValue;

    abstract public int LoadProfilePeriodInMinutes { get; }
    abstract public int LoadProfileCountPerRequest { get; }

    /// <summary>
    /// Energomera hisoblagichlari O'zbekiston vaqt mintaqasida (UTC+5) ishlaydi.
    /// Arxiv sanalari shu offset bilan DateTimeOffset'ga o'tkaziladi.
    /// </summary>
    public static readonly TimeSpan MeterTimeOffset = TimeSpan.FromHours(5);

    /// <inheritdoc />
    public virtual DateTimeOffset ParseArchiveTimestamp(string date, ArchiveType archiveType)
    {
        // Modellar sanani turlicha qaytaradi: kunlik "d.M.yy"/"dd.MM.yy", oylik
        // "M.yy" (CE303/CE102M/CE208) yoki "d.M.yy" (CE308/CE6850M — ba'zan hisob-kun
        // bilan, masalan "00.09.25"), yillik esa "b.00.yy" ko'rinishida. Shuning uchun
        // maydonlar nuqta bilan ajratilib, arxiv turiga qarab parse qilinadi.
        var parts = date.Split('.');

        switch (archiveType)
        {
            case ArchiveType.Day:
                {
                    var dateOnly = DateOnly.ParseExact(date, "d.M.yy");
                    return new DateTimeOffset(dateOnly.ToDateTime(new TimeOnly(23, 59, 59)), MeterTimeOffset);
                }
            case ArchiveType.Month:
                {
                    // 3 maydon (kun.oy.yil) bo'lsa kun e'tiborga olinmaydi, 2 maydon (oy.yil) to'g'ridan-to'g'ri oy
                    var monthIndex = parts.Length == 3 ? 1 : 0;
                    var month = int.Parse(parts[monthIndex], CultureInfo.InvariantCulture);
                    var year = 2000 + int.Parse(parts[monthIndex + 1], CultureInfo.InvariantCulture);
                    return new DateTimeOffset(year, month, 1, 0, 0, 0, MeterTimeOffset).EndOfMonth();
                }
            case ArchiveType.Year:
                {
                    var year = 2000 + int.Parse(parts[^1], CultureInfo.InvariantCulture);
                    return new DateTimeOffset(year, 1, 1, 0, 0, 0, MeterTimeOffset).EndOfYear();
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(archiveType), archiveType, null);
        }
    }

    public virtual async Task<bool> Connect()
    {
        try
        {
            await Disconnect();
            return await CommonIEC61107.ConnectAndAuthorize(stream, id, password);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "{ex}", ex.Message);
            return false;
        }
    }

    public virtual async Task Disconnect()
    {
        await CommonIEC61107.Disconnect(stream);
    }

    protected virtual async Task<string> SendAndGet(CE30XCommand cmd, string func, byte[] waitingLastBytes, params string[] paramArg)
    {
        string resultStr = string.Empty;
        var maxRetries = 2;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                resultStr = await CommonIEC61107.SendAndGet(stream, cmd, func, waitingLastBytes, paramArg);
                break;
            }
            catch (IecQueryException)
            {
                // ERRxx - hisoblagichning ataylab rad javobi, qayta urinish foydasiz.
                // Pastdagi SendWrite(func, paramArg) da xuddi shu mantiq bor edi.
                throw;
            }
            catch (Exception)
            {
                if (attempt == maxRetries - 1)
                {
                    throw;
                }
                else
                {
                    await Disconnect();
                    await Task.Delay(1000);
                    await Connect();
                }
            }
        }

        return resultStr;
    }

    protected virtual DateTimeOffset GetRecordDateTime(DateTimeOffset dateTimeOffset, int fromRecord, int recordIndex)
    {
        var minutes = (fromRecord - 1 + recordIndex) * LoadProfilePeriodInMinutes;
        var result = dateTimeOffset.StartOfDay().AddMinutes(minutes);
        return result;
    }

    protected static double[] ParseDoubleValues(string response)
    {
        return CommonIEC61107.ParseResponseValues(response)
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture))
            .ToArray();
    }

    protected async Task<(double sum, double t1, double t2, double t3, double t4)> GetEnergyValues(string func)
    {
        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3], values[4]);
    }

    /// <summary>
    /// Yuklama relesini yoqish. Faqat relesi bor hisoblagichlarda override qilinadi.
    /// </summary>
    public virtual Task RelayOn() =>
        throw new NotSupportedException("Rele boshqaruvi bu hisoblagichda qo'llab-quvvatlanmaydi");

    /// <summary>
    /// Yuklama relesini o'chirish. Faqat relesi bor hisoblagichlarda override qilinadi.
    /// </summary>
    public virtual Task RelayOff() =>
        throw new NotSupportedException("Rele boshqaruvi bu hisoblagichda qo'llab-quvvatlanmaydi");

    /// <summary>
    /// Rele holatini o'qish. Faqat relesi bor hisoblagichlarda override qilinadi.
    /// </summary>
    public virtual Task<bool> GetRelayState() =>
        throw new NotSupportedException("Rele boshqaruvi bu hisoblagichda qo'llab-quvvatlanmaydi");

    protected async Task SendWrite(string func, params string[] paramArg)
    {
        var maxRetries = 2;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await CommonIEC61107.SendWrite(stream, func, paramArg);
                return;
            }
            catch (Exception ex)
            {
                // ERRxx - hisoblagichning ataylab rad javobi, qayta urinish foydasiz
                if (ex.Message.Contains("ERR", StringComparison.Ordinal) || attempt == maxRetries - 1)
                {
                    throw;
                }

                await Disconnect();
                await Task.Delay(1000);
                await Connect();
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Task.Run(Disconnect);
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
