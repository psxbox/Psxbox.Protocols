using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using System.Globalization;

namespace Psxbox.CE30XProtocol;

public class ReaderCE102M(IStream stream,
                          string id,
                          string password = "777777",
                          ILogger? logger = null) : BaseReader(stream, id, password, logger), IReader
{
    public const string READER_TYPE = "CE102M";
   
    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated active power");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.ET0PE.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double sum = double.Parse(values[0], CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);

        return (sum, t1, t2, t3, t4);
    }

    public Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        throw new NotImplementedException();
    }

    public Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        throw new NotImplementedException();
    }

    public Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        throw new NotImplementedException();
    }

    public Task<(double a, double b, double c)> GetCorIU()
    {
        throw new NotImplementedException();
    }

    public Task<(double ab, double bc, double ca)> GetCorUU()
    {
        throw new NotImplementedException();
    }

    public async Task<(double a, double b, double c)> GetCurrent()
    {
        logger?.LogDebug("Getting current");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE102MFunction.CURRE.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture);

        return (a, 0, 0);
    }

    public async Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(ushort ago,
        string func, params string[] args)
    {
        logger?.LogDebug("Getting {func}, {daysAgo} period ago.", func, ago);

        DateOnly dateOnly = DateOnly.FromDateTime(DateTime.Today);

        string agoStr = Enum.Parse<CE102MFunction>(func, true) switch
        {
            CE102MFunction.ENDPE => dateOnly.AddDays(-ago).ToString("dd.MM.yy"),
            CE102MFunction.ENMPE => dateOnly.AddMonths(-ago).ToString("MM.yy"),
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
        var responceStr = await SendAndGet(CE30XCommand.R1, CE102MFunction.FREQU.ToString(), CommonIEC61107.DEFAULT_END);
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
        var responceStr = await SendAndGet(CE30XCommand.R1, CE102MFunction.POWEP.ToString(), CommonIEC61107.DEFAULT_END);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double sum = double.Parse(values[0], CultureInfo.InvariantCulture);

        return (sum, 0, 0, sum);
    }

    public Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        throw new NotImplementedException();
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
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double a = double.Parse(values[0], CultureInfo.InvariantCulture);

        return (a, 0, 0);
    }

    public async Task<DateTimeOffset> GetWatch()
    {
        logger?.LogDebug("Getting watch");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.WATCH.ToString(), CommonIEC61107.DEFAULT_END);

        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray()[0].Split(',', StringSplitOptions.RemoveEmptyEntries);

        var time = TimeOnly.ParseExact(values[0], "HH:mm:ss");
        var firstDotIndex = values[1].IndexOf('.');
        var date = DateOnly.ParseExact(values[1][(firstDotIndex + 1)..], "dd.MM.yy");

        var result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5));
        return result;
    }

    public string[] GetLoadProfileFunctions()
    {
        throw new NotImplementedException();
    }

    public Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(DateTimeOffset lastReadedDate,
        DateTimeOffset deviceDateTime, string func)
    {
        throw new NotImplementedException();
    }

    public string[] GetEndOfDayFunctions() => [CE102MFunction.ENDPE.ToString()];

    public string[] GetEndOfMonthFunctions () => [CE102MFunction.ENMPE.ToString()];

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
