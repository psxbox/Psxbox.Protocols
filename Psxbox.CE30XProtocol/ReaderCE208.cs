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

    // === Keyingi tasklarda to'ldiriladigan metodlar (hozircha stub) ===

    public Task<DateTimeOffset> GetWatch() => throw new NotImplementedException();
    public Task<double> GetFrequency() => throw new NotImplementedException();
    public Task<(double a, double b, double c)> GetCurrent() => throw new NotImplementedException();
    public Task<(double a, double b, double c)> GetVoltage() => throw new NotImplementedException();
    public Task<(double a, double b, double c, double sum)> GetPowerS() => throw new NotImplementedException();
    public Task<(double a, double b, double c, double sum)> GetPowerA() => throw new NotImplementedException();
    public Task<(double a, double b, double c, double sum)> GetPowerR() => throw new NotImplementedException();

    public Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day") => throw new NotImplementedException();
    public Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day") => throw new NotImplementedException();
    public Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day") => throw new NotImplementedException();

    public Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(
        ushort ago, string func, params string[] args) => throw new NotImplementedException();
    public Task<IEnumerable<string>> GetListOfArchiveTimes(string func) => throw new NotImplementedException();
    public Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(
        ushort daysAgo, short fromRecord, string func) => throw new NotImplementedException();
    public Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(
        DateTimeOffset lastReadedDate, DateTimeOffset deviceDateTime, string func) => throw new NotImplementedException();
    public Task<IEnumerable<(ushort recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(
        string func) => throw new NotImplementedException();

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

    // === Funksiya ro'yxatlari ===

    public string[] GetEndOfDayFunctions() => [CE208Function.ENDPE.ToString()];
    public string[] GetEndOfMonthFunctions() => [CE208Function.ENMPE.ToString()];
    public string[] GetEndOfYearFunctions() => []; // CE208 da yillik arxiv yo'q
    public string[] GetCurrentDayFunctions() => [CE208Function.ENDPE.ToString()];
    public string[] GetCurrentMonthFunctions() => [CE208Function.ENMPE.ToString()];
    public string[] GetCurrentYearFunctions() => []; // CE208 da yillik arxiv yo'q
    public string[] GetLoadProfileFunctions() => [CE208Function.GRAPE.ToString(), CE208Function.VPR25.ToString()];
}
