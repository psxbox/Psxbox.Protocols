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

        throw new NotImplementedException(); // Task 8 da davr-oxiri tarmog'i qo'shiladi
    }

    public async Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated reactive input energy");
        if (forCurrentPeriod)
        {
            return await GetEnergyValues(CE208Function.ET0QI.ToString());
        }

        throw new NotImplementedException(); // Task 8 da davr-oxiri tarmog'i qo'shiladi
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
