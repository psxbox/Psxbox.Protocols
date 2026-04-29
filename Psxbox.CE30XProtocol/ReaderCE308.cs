using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using Psxbox.Utils;
using Psxbox.Utils.Helpers;
using System.Globalization;

namespace Psxbox.CE30XProtocol;

public class ReaderCE308(IStream stream,
                         string id,
                         string password = "777777",
                         ILogger? logger = null) : BaseReader(stream, id, password, logger), IReader
{
    public const string READER_TYPE = "CE308";


    public async Task<DateTimeOffset> GetWatch()
    {
        logger?.LogDebug("Getting watch");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.WATCH.ToString(), CommonIEC61107.DEFAULT_END);

        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray()[0].Split(',', StringSplitOptions.RemoveEmptyEntries);

        var time = TimeOnly.ParseExact(values[0], "HH:mm:ss");
        var date = DateOnly.ParseExact(values[1][2..], "dd.MM.yy");

        var result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5));
        return result;
    }


    public async Task<double> GetFrequency()
    {
        logger?.LogDebug("Getting frequency");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.FREQU.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return values[0];
    }

    public async Task<(double a, double b, double c)> GetCurrent()
    {
        logger?.LogDebug("Getting current");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.CURRE.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double a, double b, double c)> GetVoltage()
    {
        logger?.LogDebug("Getting voltage");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.VOLTA.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerS()
    {
        logger?.LogDebug("Getting power kVA");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.POWES.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3]);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerA()
    {
        logger?.LogDebug("Getting active power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.POWEP.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3]);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        logger?.LogDebug("Getting reactive power kVar");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.POWEQ.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3]);
    }

    public async Task<(double a, double b, double c)> GetCorIU()
    {
        logger?.LogDebug("Getting COR UI");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.CORIU.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double ab, double bc, double ca)> GetCorUU()
    {
        logger?.LogDebug("Getting COR UU");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.CORUU.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    private Dictionary<ArchiveType, List<DateOnly>> archiveTimesCache = new();

    public async Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(ushort ago,
        string func, params string[] args)
    {
        logger?.LogDebug("Getting {func}, {daysAgo} period ago.", func, ago);

        // ? Agar args[0] == "0" bo'lsa davr oxiridagi umumiy energiya qiymati
        // ? agar args[0] == "1" bo'lsa, davr ichidagi to'plangan energiya qiymati
        // ? Agar args[0] bo'lmasa, "0" deb qabul qilinadi  
        var accPeriod = args.Length > 0 ? args[0] : "0";

        var archiveIndex = ago;
        var archiveType = GetArchiveType(func);

        if (ago > 0)
        {
            // Getting list of archive times to check if requested period is available. If not, return empty result
            var archiveTimesFunc = archiveType switch
            {
                ArchiveType.Day => CE308Function.LST01.ToString(),
                ArchiveType.Month => CE308Function.LST02.ToString(),
                ArchiveType.Year => CE308Function.LST03.ToString(),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (!archiveTimesCache.ContainsKey(archiveType))
            {
                var archiveTimes = await GetListOfArchiveTimes(archiveTimesFunc);
                archiveTimesCache[archiveType] = ParseArchiveTimes(archiveTimes, archiveType);
            }

            var requestedDate = GetRequestDate(archiveType, ago);

            var indexOfRequestedDate = archiveTimesCache[archiveType].IndexOf(requestedDate);

            if (indexOfRequestedDate == -1)
            {
                logger?.LogWarning("Requested date {requestedDate} is not available in archive times for {func}", requestedDate, func);
                return (string.Empty, default, default, default, default, default);
            }

            archiveIndex = (ushort)indexOfRequestedDate;
        }

        string responseStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX],
                    $"{archiveIndex}.{accPeriod}", "F");
        string[] values = CommonIEC61107.ParseResponseValues(responseStr).ToArray();

        if (values.Length == 0 || values[0] == "ERR18") return (string.Empty, default, default, default, default, default);

        try
        {
            return ParseEndOfPeriod(values, archiveType);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error on parsing! Response: {responseStr}", responseStr);
            throw;
        }
    }

    private DateOnly GetRequestDate(ArchiveType archiveType, ushort ago)
    {
        var today = DateTime.Now.StartOfADay();
        var requestedDate = archiveType switch
        {
            ArchiveType.Day => today.AddDays(-ago),
            ArchiveType.Month => today.AddMonths(-ago).StartOfAMonth(),
            ArchiveType.Year => today.AddYears(-ago).StartOfAYear(),
            _ => throw new ArgumentOutOfRangeException(nameof(archiveType), "Invalid archive type")
        };
        return DateOnly.FromDateTime(requestedDate);
    }

    protected virtual List<DateOnly> ParseArchiveTimes(IEnumerable<string> archiveTimes, ArchiveType archiveType)
    {
        // Archive times are in format daily = "16.09.25", monthly = "00.09.25", yearly = "00.00.25", so we need to parse them accordingly
        return archiveTimes.Select(at => ParseArchiveTime(at, archiveType)).ToList();
    }

    protected virtual DateOnly ParseArchiveTime(string archiveTimeStr, ArchiveType archiveType)
    {
        return archiveType switch
        {
            ArchiveType.Day => DateOnly.TryParseExact(archiveTimeStr, "dd.MM.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayDate) ? dayDate : default,
            ArchiveType.Month => DateOnly.TryParseExact(archiveTimeStr, "00.MM.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate) ? monthDate : default,
            ArchiveType.Year => DateOnly.TryParseExact(archiveTimeStr, "00.00.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var yearDate) ? yearDate : default,
            _ => DateOnly.TryParseExact(archiveTimeStr, "dd.MM.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : default,
        };
    }

    private ArchiveType GetArchiveType(string func)
    {
        if (GetCurrentDayFunctions().Contains(func)) return ArchiveType.Day;
        if (GetCurrentMonthFunctions().Contains(func)) return ArchiveType.Month;
        if (GetCurrentYearFunctions().Contains(func)) return ArchiveType.Year;
        throw new ArgumentException("Invalid function for end of period", nameof(func));
    }

    protected virtual (string date, double tSum, double t1, double t2, double t3, double t4) ParseEndOfPeriod(string[] values, ArchiveType archiveType)
    {
        string[] dateAndSum = values[0].Split(',');
        string date = dateAndSum[0];
        double tSum = double.Parse(dateAndSum[1], CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);
        return (date, tSum, t1, t2, t3, t4);
    }

    public async Task<IEnumerable<string>> GetListOfArchiveTimes(string func)
    {
        logger?.LogDebug("Getting {func} times.", func);

        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END);
        var values = CommonIEC61107.ParseResponseValues(responceStr);
        return values;
    }

    public async Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(ushort daysAgo,
        short fromRecord, string func)
    {
        logger?.LogDebug("Getting load profiles {func}. Days ago: {ago}", func, daysAgo);

        int recCount = 48 - (fromRecord - 1);

        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END, daysAgo.ToString(),
            fromRecord.ToString(), recCount.ToString());
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        List<(double, short)> data = [];

        string[] dateAndValues = values[0].Split(',');
        string date = dateAndValues[0];
        data.Add((double.Parse(dateAndValues[1], CultureInfo.InvariantCulture), short.Parse(dateAndValues[2])));

        foreach (var item in values[1..])
        {
            var splitted = item.Split(',');
            data.Add((double.Parse(splitted[0], CultureInfo.InvariantCulture), short.Parse(splitted[1])));
        }

        return (date, data);
    }

    public async Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(DateTimeOffset lastReadedDate,
        DateTimeOffset deviceDateTime, string func)
    {
        logger?.LogDebug("Getting load profiles {func}, Date: {date}, Device date: {index}", func, lastReadedDate, deviceDateTime);

        var fromRecord = (short)(lastReadedDate.Hour * 2 + (lastReadedDate.Minute / 30) + 1);
        int recCount = 48 - (fromRecord - 1);
        var daysAgo = (int)(deviceDateTime.StartOfDay() - lastReadedDate.StartOfDay()).TotalDays;

        if (lastReadedDate > deviceDateTime)
        {
            throw new Exception("Oxirgi o'qilgan vaqt qurilma vaqtidan katta");
        }

        if (daysAgo == 0)
        {
            TimeSpan timeSpan = deviceDateTime - lastReadedDate;
            recCount = (int)timeSpan.TotalMinutes / 30;
        }
        if (recCount > 48) recCount = 48;

        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END, daysAgo.ToString(),
            fromRecord.ToString(), recCount.ToString());
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        List<(double, short)> data = [];

        string[] dateAndValues = values[0].Split(',');
        string dateFromDevice = dateAndValues[0];
        data.Add((double.Parse(dateAndValues[1], CultureInfo.InvariantCulture), short.Parse(dateAndValues[2])));

        foreach (var item in values[1..])
        {
            var splitted = item.Split(',');
            data.Add((double.Parse(splitted[0], CultureInfo.InvariantCulture), short.Parse(splitted[1])));
        }

        return (dateFromDevice, data);
    }

    public async Task<IEnumerable<(ushort recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func)
    {
        logger?.LogDebug("Getting {func} times.", func);
        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END, "0");
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        var result = new List<(ushort recNo, DateTimeOffset dateTime, byte status)>();

        foreach (var item in values)
        {
            try
            {
                string[] splitted = item.Split(',');
                ushort recNo = ushort.Parse(splitted[0]);
                DateOnly date = DateOnly.ParseExact(splitted[1], "dd.MM.yy");
                TimeOnly time = TimeOnly.Parse(splitted[2]);
                DateTimeOffset dateTime = new(date.ToDateTime(time), TimeSpan.FromHours(5));
                byte status = byte.Parse(splitted[3]);
                result.Add((recNo, dateTime, status));
            }
            catch (System.Exception ex)
            {
                logger?.LogError(ex, "Error parsing: {item}", item);
            }
        }
        return result;
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        var accPeriod = forCurrentPeriod.ToString("1", "0");
        var func = GetPeriodFunction(period, 1);
        var (_, tSum, t1, t2, t3, t4) = await GetEndOfPeriod(0, func, accPeriod);
        return (tSum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        var accPeriod = forCurrentPeriod.ToString("1", "0");
        var func = GetPeriodFunction(period, 2);
        var (_, tSum, t1, t2, t3, t4) = await GetEndOfPeriod(0, func, accPeriod);
        return (tSum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        var accPeriod = forCurrentPeriod.ToString("1", "0");
        var func = GetPeriodFunction(period, 3);
        var (_, tSum, t1, t2, t3, t4) = await GetEndOfPeriod(0, func, accPeriod);
        return (tSum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        var accPeriod = forCurrentPeriod.ToString("1", "0");
        var func = GetPeriodFunction(period, 4);
        var (_, tSum, t1, t2, t3, t4) = await GetEndOfPeriod(0, func, accPeriod);
        return (tSum, t1, t2, t3, t4);
    }

    private string GetPeriodFunction(string period, int v)
    {
        if (v < 1 || v > 4)
            throw new ArgumentOutOfRangeException(nameof(v), "Value must be between 1 and 4");

        return period.ToLower() switch
        {
            "day" => $"EMD0{v}",
            "month" => $"EMM0{v}",
            "year" => $"EMY0{v}",
            _ => throw new ArgumentException("Invalid period", nameof(period))
        };
    }

    public string[] GetLoadProfileFunctions() => [
            CE308Function.VPR01.ToString(),
            CE308Function.VPR02.ToString(),
            CE308Function.VPR03.ToString(),
            CE308Function.VPR04.ToString(),
        ];

    public string[] GetEndOfDayFunctions() => [
            CE308Function.EMD01.ToString(), // End of day active +
            CE308Function.EMD02.ToString(), // End of day active -
            CE308Function.EMD03.ToString(), // End of day reactive +
            CE308Function.EMD04.ToString(), // End of day reactive -
        ];
    public string[] GetEndOfMonthFunctions() => [
            CE308Function.EMM01.ToString(), // End of month active +
            CE308Function.EMM02.ToString(), // End of month active -
            CE308Function.EMM03.ToString(), // End of month reactive +
            CE308Function.EMM04.ToString(), // End of month reactive -
        ];

    public string[] GetEndOfYearFunctions() => [
            CE308Function.EMY01.ToString(), // End of year active +
            CE308Function.EMY02.ToString(), // End of year active -
            CE308Function.EMY03.ToString(), // End of year reactive +
            CE308Function.EMY04.ToString(), // End of year reactive -
        ];

    public string[] GetCurrentDayFunctions() => GetEndOfDayFunctions();

    public string[] GetCurrentMonthFunctions() => GetEndOfMonthFunctions();

    public string[] GetCurrentYearFunctions() => GetEndOfYearFunctions();
}
