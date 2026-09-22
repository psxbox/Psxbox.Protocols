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

    public override int LoadProfilePeriodInMinutes => 30;

    public override int LoadProfileCountPerRequest => 48;

    public virtual async Task<(double a, double b, double c)> GetCorIU()
    {
        logger?.LogDebug("Getting COR UU");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.CORIU.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);

        return (values[0], values[1], values[2]);
    }

    public async Task<(double ab, double bc, double ca)> GetCorUU()
    {
        logger?.LogDebug("Getting COR UU");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.CORUU.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double a, double b, double c)> GetCurrent()
    {
        logger?.LogDebug("Getting current");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.CURRE.ToString(), [CommonIEC61107.ETX]);
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

        string[] values;

        try
        {
            var responseStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], $"{agoStr}");
            values = CommonIEC61107.ParseResponseValues(responseStr).ToArray();
        }
        catch (IecQueryException ex) when (ex.Message.Contains("ERR18", StringComparison.Ordinal))
        {
            logger?.LogWarning("Received ERR18 for {func} with agoStr {agoStr}. Returning empty result.", func, agoStr);
            return (string.Empty, default, default, default, default, default);
        }
        catch (Exception)
        {
            throw;
        }

        if (values.Length == 0)
        {
            return (string.Empty, default, default, default, default, default);
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
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.FREQU.ToString(), [CommonIEC61107.ETX]);
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        double result = double.Parse(values[0], CultureInfo.InvariantCulture);
        return result;
    }

    public Task<IEnumerable<string>> GetListOfArchiveTimes(string func)
    {
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }

    public Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(ushort daysAgo, short fromRecord,
        string func)
    {
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerA()
    {
        logger?.LogDebug("Getting active power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.POWPP.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        double sum = values.Take(3).Sum();

        return (values[0], values[1], values[2], sum);
    }

    public async virtual Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        logger?.LogDebug("Getting reactive power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.POWPQ.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        double sum = values.Take(3).Sum();
        return (values[0], values[1], values[2], sum);
    }

    /// <summary>
    /// Summa aktiv quvvat, kW (POWEP). Javobda 1 ta qiymat keladi.
    /// </summary>
    public virtual async Task<double> GetPowerASum()
    {
        logger?.LogDebug("Getting active power sum, kW");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.POWEP.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        EnsureValueCount(values, 1, CE303Function.POWEP.ToString());
        return values[0];
    }

    /// <summary>
    /// Reaktiv quvvat kirish/chiqish, kVar (POWEQ). Javobda 2 ta qiymat keladi: + va -.
    /// </summary>
    public virtual async Task<(double inValue, double outValue)> GetPowerRInOut()
    {
        logger?.LogDebug("Getting reactive power in/out, kVar");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.POWEQ.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        EnsureValueCount(values, 2, CE303Function.POWEQ.ToString());
        return (values[0], values[1]);
    }

    /// <summary>
    /// Quvvat koeffitsienti (COS_f). Javobda 4 ta qiymat: summa, A, B, C.
    /// </summary>
    public virtual async Task<(double sum, double a, double b, double c)> GetCosF()
    {
        logger?.LogDebug("Getting power factor COS_f");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.COS_f.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        EnsureValueCount(values, 4, CE303Function.COS_f.ToString());
        return (values[0], values[1], values[2], values[3]);
    }

    /// <summary>
    /// Tangens fi (TAN_f). Javobda 4 ta qiymat: summa, A, B, C.
    /// </summary>
    public virtual async Task<(double sum, double a, double b, double c)> GetTanF()
    {
        logger?.LogDebug("Getting tangent TAN_f");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.TAN_f.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        EnsureValueCount(values, 4, CE303Function.TAN_f.ToString());
        return (values[0], values[1], values[2], values[3]);
    }

    /// <summary>
    /// Javobdagi qiymatlar soni kutilganidan farq qilsa tushunarli xato tashlaydi.
    /// </summary>
    private static void EnsureValueCount(double[] values, int expected, string func)
    {
        if (values.Length != expected)
        {
            throw new IecQueryException(
                $"{func}: {expected} ta qiymat kutilgan edi, {values.Length} ta keldi");
        }
    }

    public Task<(double a, double b, double c, double sum)> GetPowerS()
    {
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }

    /// <summary>
    /// CE303 PHASE jurnali sig'imi — rasmiy RE ИНЕС.411152.081 bo'yicha
    /// "Журнал состояния фаз (50 записей)". AdminTools yozuvi (v8/v10) ham tasdiqlaydi.
    /// </summary>
    public const int DefaultPowerStatusCapacity = 50;

    /// <summary>
    /// Joriy reader uchun PHASE jurnali sig'imi. Sig'im model/firmware versiyasiga
    /// bog'liq bo'lishi mumkin (masalan ba'zi CE301 RE larida 200 yozuv) — shuning uchun
    /// meroschi sinflar uni override qilishi mumkin.
    /// </summary>
    protected virtual int PowerStatusCapacity => DefaultPowerStatusCapacity;

    /// <summary>
    /// Bitta PHASE so'rovda olinadigan yozuvlar soni (AdminTools namunasi: 10).
    /// </summary>
    private const int PowerStatusPageSize = 10;

    /// <summary>
    /// CE303 faza holati jurnalini (PHASE) o'qiydi.
    ///
    /// AdminTools yozuvidan qayta terilgan almashuv:
    ///   1) PPHAS() - PPHAS(N)  — kumulyativ recNo (eng yangi yozuvning tartib raqami);
    ///   2) PHASE(from.count) - STX PHASE(dd-MM-yy-HH-mm-SS)...ETX — sahifalar.
    ///
    /// Jurnal ayrlanma (sig'im PowerStatusCapacity), xom recNo monoton emas
    /// (50 - 1 sakraydi), shuning uchun xizmatdagi Readed{func} dedup filtri
    /// (last > recNo) uchun kumulyativ recNo sintezlanadi: u vaqt bo'yicha
    /// qat'iy o'sadi.
    /// </summary>
    public virtual async Task<IEnumerable<(long recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func)
    {
        logger?.LogDebug("Getting {func} journal", func);

        if (func != CE303Function.PHASE.ToString())
        {
            throw new ArgumentException($"Unknown function: {func}", nameof(func));
        }

        // 1) Kumulyativ recNo ni olish: PPHAS() -> PPHAS(N)
        var initStr = await SendAndGet(CE30XCommand.R1, CE303Function.PPHAS.ToString(),
            [CommonIEC61107.ETX]);
        var initValues = CommonIEC61107.ParseResponseValues(initStr).ToArray();
        if (initValues.Length == 0 || !long.TryParse(initValues[0], out var cum) || cum <= 0)
        {
            // Bo'sh jurnal yoki noto'g'ri javob — xavfsiz ravishda bo'sh natija
            logger?.LogWarning("PPHAS noto'g'ri javob: {resp}", initStr);
            return [];
        }

        var total = (int)Math.Min(cum, PowerStatusCapacity);
        var result = new List<(long recNo, DateTimeOffset dateTime, byte status)>(total);

        long cumPos = cum;   // joriy sahifaning eng yangi yozuvi (kumulyativ recNo)
        int remaining = total;

        while (remaining > 0)
        {
            // Ayrlanma chegarada so'rov bo'laklanishi shart (AdminTools ham
            // chegarada 10 ta o'rniga kamroq yozuv so'raydi):
            // take = min(sahifa, qolgan, hiRecNo) — from hech qachon 1 dan past tushmaydi.
            var hiRecNo = (int)(cumPos % PowerStatusCapacity) + 1;
            var take = Math.Min(PowerStatusPageSize, Math.Min(remaining, hiRecNo));
            var loRecNo = hiRecNo - take + 1;
            var firstCum = cumPos - take + 1;

            var respStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX],
                $"{loRecNo}.{take}");
            var values = CommonIEC61107.ParseResponseValues(respStr).ToArray();

            if (values.Length == 0)
            {
                logger?.LogWarning("PHASE({from}.{count}) bo'sh javob, o'qish to'xtatildi",
                    loRecNo, take);
                break;
            }
            if (values.Length != take)
            {
                logger?.LogWarning("PHASE({from}.{count}): kutilgan {expected} yozuv o'rniga {actual} keldi",
                    loRecNo, take, take, values.Length);
            }

            var count = Math.Min(values.Length, take);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i])) continue;
                try
                {
                    var (dateTime, status) = ParsePhaseRecord(values[i]);
                    // Kumulyativ recNo: sahifa ichida eski->yangi, vaqtga qarab monoton
                    result.Add((firstCum + i, dateTime, status));
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error parsing: {item}", values[i]);
                }
            }

            cumPos = firstCum - 1;
            remaining -= take;
        }

        return result;
    }

    /// <summary>
    /// PHASE jurnal yozuvini parse qiladi. Format: "dd-MM-yy-HH-mm-SS" —
    /// 6 maydon, soniya yo'q, oxirgisi status bayti (masalan "16-9-26-19-7-80"
    /// -> 16.09.2026 19:07, status 80).
    /// </summary>
    public static (DateTimeOffset dateTime, byte status) ParsePhaseRecord(string record)
    {
        var parts = record.Split('-');
        if (parts.Length != 6)
        {
            throw new FormatException($"Noto'g'ri PHASE yozuvi: {record}");
        }

        var paddedDate = string.Join('-', parts[0].PadLeft(2, '0'),
            parts[1].PadLeft(2, '0'), parts[2].PadLeft(2, '0'));
        var date = DateOnly.ParseExact(paddedDate, "dd-MM-yy", CultureInfo.InvariantCulture);
        var time = new TimeOnly(
            int.Parse(parts[3], CultureInfo.InvariantCulture),
            int.Parse(parts[4], CultureInfo.InvariantCulture));
        var status = byte.Parse(parts[5], CultureInfo.InvariantCulture);
        return (new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5)), status);
    }

    public virtual string[] GetPowerStatusFunctions() => [CE303Function.PHASE.ToString()];

    public async Task<(double a, double b, double c)> GetVoltage()
    {
        logger?.LogDebug("Getting voltage");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.VOLTA.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<DateTimeOffset> GetWatch()
    {
        logger?.LogDebug("Getting watch");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.DATE_.ToString(), [CommonIEC61107.ETX]);
        var values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
        var date = DateOnly.ParseExact(values[0][3..], "dd.MM.yy");
        responceStr = await SendAndGet(CE30XCommand.R1, CE303Function.TIME_.ToString(), [CommonIEC61107.ETX]);
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
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }

    public async virtual Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        logger?.LogDebug("Getting accumulated reactive power");
        var values = await GetEnergyValues(CE303Function.ET0QI.ToString());

        return values;
    }

    public virtual string[] GetLoadProfileFunctions() => [
        CE303Function.GRAPE.ToString(),
        CE303Function.GRAQE.ToString(),
        CE303Function.GRAQI.ToString()
    ];

    public async Task<IEnumerable<(DateTimeOffset dateTime, double value, short status)>> GetLoadProfiles(DateTimeOffset lastReadedDate,
        DateTimeOffset deviceDateTime, string func)
    {
        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            logger.LogDebug("Getting load profiles {func}, Date: {date}, Device date: {index}", func, lastReadedDate, deviceDateTime);
        }

        var fromRecord = (short)(lastReadedDate.Hour * 2 + (lastReadedDate.Minute / LoadProfilePeriodInMinutes) + 1);
        int recCount = LoadProfileCountPerRequest - (fromRecord - 1);
        var daysAgo = (int)(deviceDateTime.StartOfDay() - lastReadedDate.StartOfDay()).TotalDays;

        if (lastReadedDate > deviceDateTime)
        {
            throw new Exception("Oxirgi o'qilgan vaqt qurilma vaqtidan katta");
        }

        if (daysAgo == 0)
        {
            TimeSpan timeSpan = deviceDateTime - lastReadedDate;
            recCount = (int)timeSpan.TotalMinutes / LoadProfilePeriodInMinutes;
        }
        if (recCount > LoadProfileCountPerRequest) recCount = LoadProfileCountPerRequest;

        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], FormatLoadProfileParams(lastReadedDate, fromRecord, recCount));
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        List<(DateTimeOffset dateTime, double value, short status)> data = [];

        for (int i = 0; i < values.Length; i++)
        {
            var value = double.Parse(values[i], CultureInfo.InvariantCulture);
            var status = (short)0; // Assuming status is not provided in the response
            var recordDateTime = GetRecordDateTime(lastReadedDate, fromRecord, i);

            data.Add((recordDateTime, value, status));
        }

        return data;
    }

    protected virtual string FormatLoadProfileParams(DateTimeOffset date, int fromRecord, int count)
    {
        var result = $"{date:dd.MM.yy}";
        if (fromRecord == 1 && count == LoadProfileCountPerRequest)
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
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }

    public string[] GetCurrentDayFunctions()
    {
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }

    public string[] GetCurrentMonthFunctions()
    {
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }

    public string[] GetCurrentYearFunctions()
    {
        throw new NotImplementedException("This function is not implemented in CE303 reader. Please refer to the manual for more details");
    }
}
