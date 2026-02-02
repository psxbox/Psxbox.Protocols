using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using Psxbox.Utils.Helpers;
using System.Globalization;

namespace Psxbox.CE30XProtocol;

public class ReaderCE303(IStream stream,
                         string id,
                         string password = "777777",
                         ILogger? logger = null) : BaseReader(stream, id, password, logger), IReader
{
    public const string READER_TYPE = "CE303";

    public virtual async Task<(double a, double b, double c)> GetCorIU()
    {
        logger?.LogDebug("Getting COR UU");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.CORIU.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);

        return (values[0], values[1], values[2]);
    }

    public async Task<(double ab, double bc, double ca)> GetCorUU()
    {
        logger?.LogDebug("Getting COR UU");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.CORUU.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double a, double b, double c)> GetCurrent()
    {
        logger?.LogDebug("Getting current");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.CURRE.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(ushort ago,
        string func, params string[] args)
    {
        logger?.LogDebug("Getting {func}, {daysAgo} period ago.", func, ago);

        var dateOnly = DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime);

        string agoStr = Enum.Parse<CE303Function>(func, true) switch
        {
            CE303Function.ENDPE or CE303Function.ENDQE or CE303Function.ENDQI =>
                dateOnly.AddDays(-ago).ToString("d.M.yy"),
            CE303Function.ENMPE => dateOnly.AddMonths(-ago).ToString("M.yy"),
            _ => throw new Exception($"Unknown function: {func}"),
        };

        string responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], $"{agoStr}");
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        if (values.Length == 0 || values[0] == "ERR18")
        {
            return ("", 0, 0, 0, 0, 0);
        }

        string date = agoStr;
        double tSum = double.Parse(values[0], CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);
        return (date, tSum, t1, t2, t3, t4);
    }

    public async Task<double> GetFrequency()
    {
        logger?.LogDebug("Getting feruency");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.FREQU.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double result = double.Parse(values[0], CultureInfo.InvariantCulture);
        return result;
    }

    public Task<IEnumerable<string>> GetListOfArchiveTimes(string func)
    {
        throw new NotImplementedException();
    }

    public Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(ushort daysAgo, short fromRecord,
        string func)
    {
        throw new NotImplementedException();
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerA()
    {
        logger?.LogDebug("Getting active power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.POWPP.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        double sum = values.Take(3).Sum();

        return (values[0], values[1], values[2], sum);
    }

    public async virtual Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        logger?.LogDebug("Getting reactive power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.POWPQ.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        double sum = values.Take(3).Sum();
        return (values[0], values[1], values[2], sum);
    }

    public Task<(double a, double b, double c, double sum)> GetPowerS()
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<(ushort recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func)
    {
        throw new NotImplementedException();
    }

    public async Task<(double a, double b, double c)> GetVoltage()
    {
        logger?.LogDebug("Getting voltage");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.VOLTA.ToString(), CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<DateTimeOffset> GetWatch()
    {
        logger?.LogDebug("Getting watch");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.DATE_.ToString(), CommonIEC61107.DEFAULT_END);
        var values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        var date = DateOnly.ParseExact(values[0][3..], "dd.MM.yy");
        responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.TIME_.ToString(), CommonIEC61107.DEFAULT_END);
        values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        var time = TimeOnly.ParseExact(values[0], "HH:mm:ss");

        var result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5));
        return result;
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated active power");
        var values = await GetEnergyValues(CE303Function.ET0PE.ToString()); 

        return values;
    }

    public async virtual Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated reactive power");
        var values = await GetEnergyValues(CE303Function.ET0QE.ToString());

        return values;
    }

    public Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        throw new NotImplementedException();
    }

    public async virtual Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated active power");
        var values = await GetEnergyValues(CE303Function.ET0QI.ToString());

        return values;
    }

    public virtual string[] GetLoadProfileFunctions() => [
        CE303Function.GRAPE.ToString(),
        CE303Function.GRAQE.ToString(),
        CE303Function.GRAQI.ToString()
    ];

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

        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END, FormatLoadProfileParams(lastReadedDate, fromRecord, recCount));
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        List<(double, short)> data = [];

        foreach (var item in values)
        {
            data.Add((double.Parse(item, CultureInfo.InvariantCulture), 0));
        }

        return (string.Empty, data);
    }

    protected virtual string FormatLoadProfileParams(DateTimeOffset date, int fromRecord, int count)
    {
        var result = $"{date:dd.MM.yy}";
        if (fromRecord == 1 && count == 48)
        {
            return result;
        }

        return $"{result}.{fromRecord}.{count}";
    }

    public virtual string[] GetEndOfDayFunctions() => [
        CE303Function.ENDPE.ToString(),
        CE303Function.ENDQE.ToString(),
        CE303Function.ENDQI.ToString()
    ];

    public string[] GetEndOfMonthFunctions() => [CE303Function.ENMPE.ToString()];

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
