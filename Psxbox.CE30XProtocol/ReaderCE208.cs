using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using Psxbox.Utils;
using Psxbox.Utils.Helpers;
using System.Globalization;

namespace Psxbox.CE30XProtocol;

public class ReaderCE208(IStream stream,
                         string id,
                         string password = "777777",
                         ILogger? logger = null) : BaseReader(stream, id, password, logger), IReader
{
    public const string READER_TYPE = "CE208";

    private readonly List<DateOnly> _dayArchiveDates = [];
    private readonly List<DateOnly> _monthArchiveDates = [];
    private int? _recordsPerDay;

    // === O'lchov metodlari (Task 5) ===

    public async Task<DateTimeOffset> GetWatch()
    {
        logger?.LogDebug("Getting watch");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.WATCH.ToString(), CommonIEC61107.DEFAULT_END);

        // Javob formati: (HH:mm:ss,D.dd.MM.yy,s) - D hafta kuni, s mavsum belgisi
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray()[0]
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        var time = TimeOnly.ParseExact(values[0], "HH:mm:ss");
        var firstDotIndex = values[1].IndexOf('.');
        var date = DateOnly.ParseExact(values[1][(firstDotIndex + 1)..], "dd.MM.yy");

        return new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5));
    }

    public async Task<double> GetFrequency()
    {
        logger?.LogDebug("Getting frequency");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.FREQU.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return values[0];
    }

    public async Task<(double a, double b, double c)> GetCurrent()
    {
        logger?.LogDebug("Getting current");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.CURRE.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], 0, 0); // bir fazali
    }

    public async Task<(double a, double b, double c)> GetVoltage()
    {
        logger?.LogDebug("Getting voltage");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.VOLTA.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], 0, 0); // bir fazali
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerA()
    {
        logger?.LogDebug("Getting active power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.POWEP.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], 0, 0, values[0]); // bir fazali
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerS()
    {
        logger?.LogDebug("Getting power kVA");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.POWES.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], 0, 0, values[0]); // bir fazali
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        logger?.LogDebug("Getting reactive power kVar");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.POWEQ.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], 0, 0, values[0]); // bir fazali
    }

    // === Energiya metodlari (Task 5 + Task 8) ===

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated active input energy");
        if (forCurrentPeriod)
        {
            return await GetEnergyValues(CE208Function.ET0PE.ToString());
        }

        var func = GetPeriodFunc(period, "ENDPE", "ENMPE");
        var (_, tSum, t1, t2, t3, t4) = await GetEndOfPeriod(1, func); // oxirgi yopilgan davr
        return (tSum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated reactive input energy");
        if (forCurrentPeriod)
        {
            return await GetEnergyValues(CE208Function.ET0QI.ToString());
        }

        var func = GetPeriodFunc(period, "ERD", "ERM");
        var (_, tSum, t1, t2, t3, t4) = await GetEndOfPeriod(1, func); // oxirgi yopilgan davr
        return (tSum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        if (!forCurrentPeriod)
        {
            throw new NotSupportedException("CE208 da reaktiv eksport energiyasining davr-oxiri arxivi yo'q");
        }

        logger?.LogDebug("Getting accumulated reactive output energy");
        return await GetEnergyValues(CE208Function.ET0QE.ToString());
    }

    public async Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(
        ushort ago, string func, params string[] args)
    {
        logger?.LogDebug("Getting {func}, {ago} period ago", func, ago);

        bool daily = func is "ENDPE" or "ERD";
        bool monthly = func is "ENMPE" or "ERM";
        if (!daily && !monthly)
        {
            throw new ArgumentException($"Unknown function: {func}", nameof(func));
        }

        // Arxiv sanalari keshini to'ldirish (birinchi so'rovda qurilmadan o'qiladi)
        var dates = daily ? _dayArchiveDates : _monthArchiveDates;
        if (dates.Count == 0)
        {
            await GetListOfArchiveTimes(daily
                ? CE208Function.DATED.ToString()
                : CE208Function.DATEM.ToString());
        }

        var today = DateTime.Today;
        var requested = DateOnly.FromDateTime(daily
            ? today.AddDays(-ago)
            : today.StartOfAMonth().AddMonths(-ago));

        var index = dates.IndexOf(requested);
        if (index < 0)
        {
            logger?.LogWarning("Requested date {date} not found in {func} archive", requested, func);
            return ("", 0, 0, 0, 0, 0);
        }

        // Aktiv - sana bilan, reaktiv - arxiv pozitsiyasi (1-dan) indeksi bilan
        var responceStr = func switch
        {
            "ENDPE" => await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], requested.ToString("dd.MM.yy")),
            "ENMPE" => await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], requested.ToString("MM.yy")),
            "ERD" => await SendAndGet(CE30XCommand.R1, $"ERD{index + 1:D2}", [CommonIEC61107.ETX]),
            _ => await SendAndGet(CE30XCommand.R1, $"ERM{index + 1:D2}", [CommonIEC61107.ETX]),
        };

        var values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        if (values.Length == 0 || values[0].StartsWith("ERR"))
        {
            return ("", 0, 0, 0, 0, 0);
        }

        return (
            requested.ToString(daily ? "dd.MM.yy" : "MM.yy"),
            double.Parse(values[0], CultureInfo.InvariantCulture),
            double.Parse(values[1], CultureInfo.InvariantCulture),
            double.Parse(values[2], CultureInfo.InvariantCulture),
            double.Parse(values[3], CultureInfo.InvariantCulture),
            double.Parse(values[4], CultureInfo.InvariantCulture));
    }

    private static string GetPeriodFunc(string period, string dayFunc, string monthFunc) => period.ToLower() switch
    {
        "day" => dayFunc,
        "month" => monthFunc,
        _ => throw new ArgumentException($"Invalid period: {period}", nameof(period)),
    };

    private static readonly string[] DayDateFormats = ["dd.MM.yy", "d.M.yy"];
    private static readonly string[] MonthDateFormats = ["MM.yy", "M.yy"];

    /// <summary>
    /// Arxiv sanasini parse qiladi. Kunlik: dd.MM.yy, oylik: MM.yy (bir xonali variantlar bilan).
    /// </summary>
    public static DateOnly ParseArchiveDate(string value, bool daily)
    {
        return DateOnly.ParseExact(value, daily ? DayDateFormats : MonthDateFormats,
            CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    public async Task<IEnumerable<string>> GetListOfArchiveTimes(string func)
    {
        logger?.LogDebug("Getting {func} dates", func);

        string[] validFuncs = [
            CE208Function.DATED.ToString(),
            CE208Function.DATEM.ToString(),
            CE208Function.DATEP.ToString(),
        ];
        if (!validFuncs.Contains(func))
        {
            throw new ArgumentException($"Unknown function: {func}", nameof(func));
        }

        // DATED/DATEP - 128 tagacha, DATEM - 36 tagacha yozuv; bo'lib-bo'lib o'qiymiz
        const int chunk = 16;
        int max = func == CE208Function.DATEM.ToString() ? 36 : 128;

        List<string> result = [];
        for (int i = 1; i <= max; i += chunk)
        {
            var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], $"{i}.{chunk}");
            var values = CommonIEC61107.ParseResponseValues(responceStr)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToArray();

            if (values.Length == 0 || values[0].StartsWith("ERR")) break;
            result.AddRange(values);
            if (values.Length < chunk) break;
        }

        if (func == CE208Function.DATED.ToString())
        {
            _dayArchiveDates.Clear();
            _dayArchiveDates.AddRange(result.Select(v => ParseArchiveDate(v, daily: true)));
        }
        else if (func == CE208Function.DATEM.ToString())
        {
            _monthArchiveDates.Clear();
            _monthArchiveDates.AddRange(result.Select(v => ParseArchiveDate(v, daily: false)));
        }

        return result;
    }

    /// <summary>
    /// Profil yozuvini parse qiladi. Format: "qiymat" yoki "qiymat;bayroq".
    /// </summary>
    public static (double value, short flags) ParseProfileRecord(string record)
    {
        var parts = record.Split(';');
        var value = double.Parse(parts[0], CultureInfo.InvariantCulture);
        short flags = parts.Length > 1 ? short.Parse(parts[1]) : (short)0;
        return (value, flags);
    }

    /// <summary>
    /// Kunlik profil yozuvlari soni = 1440 / TAVER. TAVER birinchi so'rovda o'qilib keshlanadi.
    /// </summary>
    private async Task<int> GetRecordsPerDay()
    {
        if (_recordsPerDay is int cached) return cached;

        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.TAVER.ToString(), CommonIEC61107.DEFAULT_END);
        var taver = int.Parse(CommonIEC61107.ParseResponseValues(responceStr).First(), CultureInfo.InvariantCulture);
        _recordsPerDay = 1440 / taver;
        logger?.LogDebug("TAVER = {taver} min, records per day = {records}", taver, _recordsPerDay);
        return _recordsPerDay.Value;
    }

    public async Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(ushort daysAgo,
        short fromRecord, string func)
    {
        logger?.LogDebug("Getting load profiles {func}. Days ago: {ago}", func, daysAgo);

        var recordsPerDay = await GetRecordsPerDay();
        int recCount = recordsPerDay - (fromRecord - 1);
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysAgo));

        return await ReadProfileRecords(func, date, fromRecord, recCount);
    }

    public async Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(
        DateTimeOffset lastReadedDate, DateTimeOffset deviceDateTime, string func)
    {
        logger?.LogDebug("Getting load profiles {func}, Date: {date}, Device date: {deviceDate}",
            func, lastReadedDate, deviceDateTime);

        if (lastReadedDate > deviceDateTime)
        {
            throw new Exception("Oxirgi o'qilgan vaqt qurilma vaqtidan katta");
        }

        var recordsPerDay = await GetRecordsPerDay();
        int minutesPerRecord = 1440 / recordsPerDay;

        var fromRecord = (short)((lastReadedDate.Hour * 60 + lastReadedDate.Minute) / minutesPerRecord + 1);
        int recCount = recordsPerDay - (fromRecord - 1);
        var daysAgo = (int)(deviceDateTime.StartOfDay() - lastReadedDate.StartOfDay()).TotalDays;

        if (daysAgo == 0)
        {
            TimeSpan timeSpan = deviceDateTime - lastReadedDate;
            recCount = (int)timeSpan.TotalMinutes / minutesPerRecord;
        }
        if (recCount > recordsPerDay) recCount = recordsPerDay;

        var date = DateOnly.FromDateTime(lastReadedDate.Date);
        return await ReadProfileRecords(func, date, fromRecord, recCount);
    }

    private async Task<(string date, IEnumerable<(double, short)> data)> ReadProfileRecords(
        string func, DateOnly date, short fromRecord, int recCount)
    {
        // GRAPE(dd.MM.yy,nn,kk) - javobda sana yo'q, shuning uchun so'ralgan sana qaytariladi
        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX],
            date.ToString("dd.MM.yy"), fromRecord.ToString(), recCount.ToString());

        var values = CommonIEC61107.ParseResponseValues(responceStr)
            .Where(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith("ERR"))
            .ToArray();

        List<(double, short)> data = [];
        foreach (var item in values)
        {
            try
            {
                data.Add(ParseProfileRecord(item));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error parsing profile record: {item}", item);
            }
        }

        return (date.ToString("dd.MM.yy"), data);
    }

    private const int PowerStatusReadCount = 10; // jurnaldan o'qiladigan oxirgi yozuvlar soni

    /// <summary>
    /// Jurnal yozuvini parse qiladi. Format: "dd-MM-yy;HH:mm;XX".
    /// </summary>
    public static (DateTimeOffset dateTime, byte status) ParseLogRecord(string record)
    {
        var parts = record.Split(';');
        var date = DateOnly.ParseExact(parts[0], "dd-MM-yy");
        var time = TimeOnly.Parse(parts[1]);
        var status = byte.Parse(parts[2]);
        return (new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5)), status);
    }

    public async Task<IEnumerable<(ushort recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func)
    {
        logger?.LogDebug("Getting {func} journal", func);

        string[] validFuncs = [
            CE208Function.LOG01.ToString(),
            CE208Function.LOG02.ToString(),
            CE208Function.LOG03.ToString(),
        ];
        if (!validFuncs.Contains(func))
        {
            throw new ArgumentException($"Unknown function: {func}", nameof(func));
        }

        // Jurnal yozuvlari yangi->eski tartibda, 1-yozuvdan boshlab o'qiymiz
        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX],
            "1", PowerStatusReadCount.ToString());
        var values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        var result = new List<(ushort recNo, DateTimeOffset dateTime, byte status)>();
        for (int i = 0; i < values.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]) || values[i].StartsWith("ERR")) continue;
            try
            {
                var (dateTime, status) = ParseLogRecord(values[i]);
                result.Add(((ushort)(i + 1), dateTime, status));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error parsing: {item}", values[i]);
            }
        }
        return result;
    }

    // === CE208 protokolida mavjud emas ===

    /// <summary>CE208 bir fazali - burchak komandasi yo'q</summary>
    public Task<(double a, double b, double c)> GetCorIU() =>
        throw new NotImplementedException("CE208 da burchak komandasi yo'q");

    /// <summary>CE208 bir fazali - burchak komandasi yo'q</summary>
    public Task<(double ab, double bc, double ca)> GetCorUU() =>
        throw new NotImplementedException("CE208 da burchak komandasi yo'q");

    /// <summary>CE208 faqat aktiv import (A+) energiyasini o'lchaydi</summary>
    public Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day") =>
        throw new NotImplementedException("CE208 faqat aktiv import (A+) energiyasini o'lchaydi");

    // === Rele boshqaruvi ===

    public override async Task RelayOn() => await SetRelay(true);

    public override async Task RelayOff() => await SetRelay(false);

    private async Task SetRelay(bool on)
    {
        logger?.LogDebug("Setting relay: {on}", on);
        try
        {
            await SendWrite(CE208Function.RCTL1.ToString(), on ? "1" : "0");
        }
        catch (Exception ex) when (ex.Message.Contains("ERR18"))
        {
            throw new Exception(
                "Rele komandasi rad etildi (ERR18). REL_1 konfiguratsiyasida " +
                "interfeys orqali boshqarish (bit 3) yoqilmagan bo'lishi mumkin.", ex);
        }
    }

    public override async Task<bool> GetRelayState()
    {
        logger?.LogDebug("Getting relay state");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE208Function.STAT_.ToString(), CommonIEC61107.DEFAULT_END);
        var value = CommonIEC61107.ParseResponseValues(responceStr).First();
        return ParseRelayState(value);
    }

    /// <summary>
    /// STAT_ holat so'zining (16-bit hex) 15-bitidan rele holatini ajratadi.
    /// </summary>
    /// <returns><b>true</b> - rele yoniq</returns>
    public static bool ParseRelayState(string statHex)
    {
        var stat = Convert.ToUInt16(statHex, 16);
        return (stat & 0x8000) != 0;
    }

    // === Funksiya ro'yxatlari ===

    public string[] GetEndOfDayFunctions() => [CE208Function.ENDPE.ToString()];
    public string[] GetEndOfMonthFunctions() => [CE208Function.ENMPE.ToString()];
    public string[] GetEndOfYearFunctions() => []; // CE208 da yillik arxiv yo'q
    public string[] GetCurrentDayFunctions() => [CE208Function.ENDPE.ToString()];
    public string[] GetCurrentMonthFunctions() => [CE208Function.ENMPE.ToString()];
    public string[] GetCurrentYearFunctions() => []; // CE208 da yillik arxiv yo'q
    public string[] GetLoadProfileFunctions() => [CE208Function.GRAPE.ToString(), CE208Function.VPR25.ToString()];
}
