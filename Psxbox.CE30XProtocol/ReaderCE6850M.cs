using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using Psxbox.Utils;
using Psxbox.Utils.Helpers;
using System.Globalization;

namespace Psxbox.CE30XProtocol;

public class ReaderCE6850M(IStream stream,
                           string id,
                           string password = "777777",
                           ILogger? logger = null) : BaseReader(stream, id, password, logger), IReader
{
    public const string READER_TYPE = "CE6850M";
    private readonly List<DateOnly> _dayArchiveDates = [];
    private readonly List<DateOnly> _monthArchiveDates = [];

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated active input energy");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.ET0PE.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double sum = double.Parse(values[0], CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);

        return (sum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated active output energy");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.ET0PI.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double sum = double.Parse(values[0], CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);

        return (sum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated reactive input energy");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.ET0QE.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double sum = double.Parse(values[0], CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);

        return (sum, t1, t2, t3, t4);
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated reactive output energy");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.ET0QI.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double sum = double.Parse(values[0], CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);

        return (sum, t1, t2, t3, t4);
    }

    public async Task<(double a, double b, double c)> GetCorIU()
    {
        logger?.LogDebug("Getting angle of I and U");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.CORIU.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture);
        double b = double.Parse(values[1], CultureInfo.InvariantCulture);
        double c = double.Parse(values[2], CultureInfo.InvariantCulture);

        return (a, b, c);
    }

    public async Task<(double ab, double bc, double ca)> GetCorUU()
    {
        logger?.LogDebug("Getting COR UU");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.CORUU.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture);
        double b = double.Parse(values[1], CultureInfo.InvariantCulture);
        double c = double.Parse(values[2], CultureInfo.InvariantCulture);

        return (a, b, c);
    }

    public async Task<(double a, double b, double c)> GetCurrent()
    {
        logger?.LogDebug("Getting current");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.CURRE.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture);
        double b = double.Parse(values[1], CultureInfo.InvariantCulture);
        double c = double.Parse(values[2], CultureInfo.InvariantCulture);

        return (a, b, c);
    }

    public virtual async Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(
        ushort ago, string func, params string[] args)
    {
        string[] funcs = [
            ..GetEndOfDayFunctions(),
            ..GetEndOfMonthFunctions(),
        ];

        if (!funcs.Contains(func))
        {
            throw new ArgumentException("Unknown function", nameof(func));
        }

        bool daily = func.StartsWith("ED");
        bool isDatesReaded = daily ? _dayArchiveDates.Count > 0 : _monthArchiveDates.Count > 0;

        if (!isDatesReaded)
        {
            await GetListOfArchiveTimes(daily ? "DATED" : "DATEM");
        }

        var today = DateTime.Today;
        var agoDay = DateOnly.FromDateTime(daily ? today.AddDays(-ago) : today.StartOfAMonth().AddMonths(-ago));
        var index = daily ? _dayArchiveDates.IndexOf(agoDay) : _monthArchiveDates.IndexOf(agoDay);

        if (index < 0) return ("", 0, 0, 0, 0, 0);
        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END, $"{index + 1}");
        var values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        return (
            agoDay.ToString("d.M.yy"),
            double.Parse(values[0], CultureInfo.InvariantCulture),
            double.Parse(values[1], CultureInfo.InvariantCulture),
            double.Parse(values[2], CultureInfo.InvariantCulture),
            double.Parse(values[3], CultureInfo.InvariantCulture),
            double.Parse(values[4], CultureInfo.InvariantCulture)
        );
    }

    public async Task<double> GetFrequency()
    {
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.FREQU.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        return double.Parse(values[0], CultureInfo.InvariantCulture);
    }

    public async Task<IEnumerable<string>> GetListOfArchiveTimes(string func)
    {
        string[] funcs = [CE6850MFunction.DATED.ToString(), CE6850MFunction.DATEM.ToString()];

        if (!funcs.Contains(func))
        {
            throw new ArgumentException("Unknown function", nameof(func));
        }

        int delta = func == CE6850MFunction.DATED.ToString() ? 15 : 12;
        int end = func == CE6850MFunction.DATED.ToString() ? 45 : 24;

        List<string> result = new(end);

        for (int i = 1; i < end; i += delta)
        {
            var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END, $"{i}.{delta}");
            var values = CommonIEC61107.ParseResponseValues(responceStr);
            result.AddRange(values);
        }

        switch (func)
        {
            case "DATED":
                _dayArchiveDates.AddRange(result.Select(x => DateOnly.ParseExact(x, "d.M.yy")));
                break;

            case "DATEM":
                _monthArchiveDates.AddRange(result.Select(x => DateOnly.ParseExact(x, "M.yy")));
                break;
        }

        return result;
    }

    public Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(ushort daysAgo, short fromRecord,
        string func)
    {
        throw new NotImplementedException();
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerA()
    {
        logger?.LogDebug("Getting active power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.POWEP.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture) / 1000; //Hisoblagich qiymatni W ko'rinishida qaytaradi, shuning uchun 1000 ga bo'ldik
        double b = double.Parse(values[1], CultureInfo.InvariantCulture) / 1000;
        double c = double.Parse(values[2], CultureInfo.InvariantCulture) / 1000;
        double sum = double.Parse(values[3], CultureInfo.InvariantCulture) / 1000;

        return (a, b, c, sum);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        logger?.LogDebug("Getting reactive power kVar");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.POWEQ.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture) / 1000;
        double b = double.Parse(values[1], CultureInfo.InvariantCulture) / 1000;
        double c = double.Parse(values[2], CultureInfo.InvariantCulture) / 1000;
        double sum = double.Parse(values[3], CultureInfo.InvariantCulture) / 1000;

        return (a, b, c, sum);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerS()
    {
        logger?.LogDebug("Getting power kVA");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.POWES.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture) / 1000;
        double b = double.Parse(values[1], CultureInfo.InvariantCulture) / 1000;
        double c = double.Parse(values[2], CultureInfo.InvariantCulture) / 1000;
        double sum = double.Parse(values[3], CultureInfo.InvariantCulture) / 1000;

        return (a, b, c, sum);
    }

    public Task<IEnumerable<(ushort recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func)
    {
        throw new NotImplementedException();
    }

    public async Task<(double a, double b, double c)> GetVoltage()
    {
        logger?.LogDebug("Getting voltage");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.VOLTA.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture);
        double b = double.Parse(values[1], CultureInfo.InvariantCulture);
        double c = double.Parse(values[2], CultureInfo.InvariantCulture);

        return (a, b, c);
    }

    public virtual async Task<DateTimeOffset> GetWatch()
    {
        logger?.LogDebug("Getting watch");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.DATE_.ToString(), CommonIEC61107.DEFAULT_END);
        var values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        var date = DateOnly.ParseExact(values[0][3..], "dd-MM-yy");
        responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.TIME_.ToString(), CommonIEC61107.DEFAULT_END);
        values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        var time = TimeOnly.ParseExact(values[0], "HH:mm:ss");

        var result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5));
        return result;
    }

    public string[] GetLoadProfileFunctions() =>
    [
        CE6850MFunction.GRAPE.ToString(),
        CE6850MFunction.GRAPI.ToString(),
        CE6850MFunction.GRAQE.ToString(),
        CE6850MFunction.GRAQI.ToString()
    ];

    protected virtual int ReadCount => 24; // Default read count for load profiles, can be overridden
    protected virtual string FormatLoadProfileParams(DateTimeOffset date, int fromRecord, int count)
    {
        return $"{date:d.M.yy}.{fromRecord}.{count}";
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
        if (recCount > ReadCount) recCount = ReadCount;

        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END, FormatLoadProfileParams(lastReadedDate, fromRecord, recCount));
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        List<(double, short)> data = [];

        foreach (var item in values)
        {
            data.Add((double.Parse(item, CultureInfo.InvariantCulture), 0));
        }

        return (string.Empty, data);
    }

    public string[] GetEndOfDayFunctions() =>
    [
        CE6850MFunction.ED0PE.ToString(),
        CE6850MFunction.ED0PI.ToString(),
        CE6850MFunction.ED0QE.ToString(),
        CE6850MFunction.ED0QI.ToString()
    ];

    public string[] GetEndOfMonthFunctions() =>
    [
        CE6850MFunction.EM0PE.ToString(),
        CE6850MFunction.EM0PI.ToString(),
        CE6850MFunction.EM0QE.ToString(),
        CE6850MFunction.EM0QI.ToString()
    ];


    public string[] GetEndOfYearFunctions()
    {
        throw new NotImplementedException();
    }

    public string[] GetCurrentDayFunctions()
    {
        throw new NotImplementedException();
    }

    public string[] GetCurrentMonthFunctions()
    {
        throw new NotImplementedException();
    }

    public string[] GetCurrentYearFunctions()
    {
        throw new NotImplementedException();
    }
}
