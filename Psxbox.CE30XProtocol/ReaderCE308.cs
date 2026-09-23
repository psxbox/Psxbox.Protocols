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
    private Dictionary<ArchiveType, List<DateOnly>> archiveTimesCache = new();
    private List<DateOnly>? profileDatesCache;
    private int? averagingIntervalMinutes;

    /// <summary>PROFI o'qilmasa ishlatiladigan profil intervali (daqiqalarda).</summary>
    public const int DefaultLoadProfilePeriodInMinutes = 30;

    /// <summary>
    /// Profil o'rtalash intervali (daqiqalarda) — PROFI() dan (HEX, keshlanadi).
    /// TODO: sinxron property o'rniga asinxron qilish uchun IReader/BaseReader ni
    /// o'zgartirish kerak — worker (`ReaderFunctions`) shu propertyni synxron o'qiydi
    /// (CE208/CE303 dagi kabi yechim ishlatilgan).
    /// </summary>
    public override int LoadProfilePeriodInMinutes =>
        averagingIntervalMinutes ?? GetProfileIntervalAsync().GetAwaiter().GetResult();

    /// <summary>Kunlik profil yozuvlari soni = 1440 / PROFI.</summary>
    public override int LoadProfileCountPerRequest => 1440 / LoadProfilePeriodInMinutes;

    public async Task<DateTimeOffset> GetWatch()
    {
        logger?.LogDebug("Getting watch");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.WATCH.ToString(), [CommonIEC61107.ETX]);

        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray()[0].Split(',', StringSplitOptions.RemoveEmptyEntries);

        var time = TimeOnly.ParseExact(values[0], "HH:mm:ss");
        var date = DateOnly.ParseExact(values[1][2..], "dd.MM.yy");

        var result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5));
        return result;
    }


    public async Task<double> GetFrequency()
    {
        logger?.LogDebug("Getting frequency");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.FREQU.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return values[0];
    }

    public async Task<(double a, double b, double c)> GetCurrent()
    {
        logger?.LogDebug("Getting current");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.CURRE.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double a, double b, double c)> GetVoltage()
    {
        logger?.LogDebug("Getting voltage");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.VOLTA.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerS()
    {
        logger?.LogDebug("Getting power kVA");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.POWES.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3]);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerA()
    {
        logger?.LogDebug("Getting active power kWt");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.POWEP.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3]);
    }

    public async Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        logger?.LogDebug("Getting reactive power kVar");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.POWEQ.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3]);
    }

    public async Task<(double a, double b, double c)> GetCorIU()
    {
        logger?.LogDebug("Getting COR UI");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.CORIU.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

    public async Task<(double ab, double bc, double ca)> GetCorUU()
    {
        logger?.LogDebug("Getting COR UU");
        var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.CORUU.ToString(), [CommonIEC61107.ETX]);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2]);
    }

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
                // Stale kesh himoyasi: bir marta majburan yangilab qayta ko'ramiz
                var refreshedTimes = await GetListOfArchiveTimes(archiveTimesFunc);
                archiveTimesCache[archiveType] = ParseArchiveTimes(refreshedTimes, archiveType);
                indexOfRequestedDate = archiveTimesCache[archiveType].IndexOf(requestedDate);
            }

            if (indexOfRequestedDate == -1)
            {
                logger?.LogWarning("Requested date {requestedDate} is not available in archive times for {func}", requestedDate, func);
                return (string.Empty, default, default, default, default, default);
            }

            archiveIndex = (ushort)indexOfRequestedDate;
        }

        string responseStr;
        string[] values;

        try
        {
            responseStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX],
                        $"{archiveIndex}.{accPeriod}", "F");
            values = CommonIEC61107.ParseResponseValues(responseStr).ToArray();
        }
        catch (IecQueryException ex) when (ex.Message.Contains("ERR18", StringComparison.Ordinal))
        {
            logger?.LogWarning("Received ERR18 for {func} with archiveIndex {archiveIndex} and accPeriod {accPeriod}. Returning empty result.", func, archiveIndex, accPeriod);
            return (string.Empty, default, default, default, default, default);
        }
        catch (Exception)
        {
            throw;
        }

        if (values.Length == 0) return (string.Empty, default, default, default, default, default);

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
        // Hujjat formatlari: LST01 "dd.mm.yy", LST02 "bb.mm.yy" (bb = hisob-kun,
        // 0 = oy oxiri), LST03 "bb.00.yy". Maydonlar nuqta bilan ajratilgan holda
        // parse qilinadi ("0.09.25", "00.09.25", "15.09.25" — hammasi qabul qilinadi):
        // oy/yil identifikatorida hisob-kun e'tiborga olinmaydi, kalit sifatida
        // oy/yil boshi ishlatiladi (GetRequestDate bilan mos keladi).
        var parts = archiveTimeStr.Split('.');
        if (parts.Length != 3) return default;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var second)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var twoDigitYear))
        {
            return default;
        }

        var year = 2000 + twoDigitYear;
        try
        {
            return archiveType switch
            {
                ArchiveType.Day => new DateOnly(year, second, first),
                ArchiveType.Month => new DateOnly(year, second, 1),
                ArchiveType.Year => new DateOnly(year, 1, 1),
                _ => default,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return default;
        }
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

        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX]);
        var values = CommonIEC61107.ParseResponseValues(responceStr);
        return values;
    }

    public async Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(ushort daysAgo,
        short fromRecord, string func)
    {
        logger?.LogDebug("Getting load profiles {func}. Days ago: {ago}", func, daysAgo);

        // VPR i — fiksatsiya indeksi (0 = joriy sutka); fiksatsiyalar tushib qolganda
        // indeks siljiydi — LST04 orqali aniqlanadi, topilmasa eski xulq saqlanadi.
        var profileIndex = (int)daysAgo;
        try
        {
            profileIndex = await ResolveProfileFixationIndexAsync(GetRequestDate(ArchiveType.Day, daysAgo));
        }
        catch (IecQueryException ex)
        {
            logger?.LogWarning(ex, "Profil sanasi topilmadi, {daysAgo} indeks ishlatiladi", daysAgo);
        }

        int recCount = LoadProfileCountPerRequest - (fromRecord - 1);

        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], profileIndex.ToString(),
            fromRecord.ToString(), recCount.ToString());
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        List<(double, short)> data = [];

        // Birinchi qator "dd.mm.yy,X.X,hex", qolganlari "X.X,hex" — oxirgi ikki maydon
        // bir xil: qiymat va status belgisi (uStatRec_TypeDef, HEX).
        string date = values.Length > 0 ? values[0].Split(',')[0] : string.Empty;
        foreach (var item in values)
        {
            var splitted = item.Split(',');
            try
            {
                data.Add((double.Parse(splitted[^2], CultureInfo.InvariantCulture),
                    ParseProfileStatus(splitted[^1])));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error parsing load profile record: {item}", item);
            }
        }

        return (date, data);
    }

    /// <summary>
    /// Yuklama profilini o'qiydi. Standart usul — <b>VPIzz(dd.mm.yy,k,n)</b>
    /// (sana identifikatori bo'yicha, hujjat "4б" bo'limi): fiksatsiya indeksi
    /// talab qilinmaydi, kun to'g'ridan-to'g'ri sanasi bilan so'raladi.
    /// Eski firmware VPI ni qo'llab-quvvatlamasa (ERR12) —
    /// <see cref="GetLoadProfilesByFixationIndexAsync"/> (VPRzz(i,k,n)) ga o'tadi.
    /// </summary>
    public virtual async Task<IEnumerable<(DateTimeOffset dateTime, double value, short status)>> GetLoadProfiles(DateTimeOffset lastReadedDate,
        DateTimeOffset deviceDateTime, string func)
    {
        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            logger.LogDebug("Getting load profiles {func}, Date: {date}, Device date: {deviceDate}",
                func, lastReadedDate, deviceDateTime);
        }

        if (lastReadedDate > deviceDateTime)
        {
            throw new Exception("Oxirgi o'qilgan vaqt qurilma vaqtidan katta");
        }

        try
        {
            return await GetLoadProfilesByDateAsync(lastReadedDate, deviceDateTime, func);
        }
        catch (IecQueryException ex) when (ex.Message.Contains("ERR12", StringComparison.Ordinal))
        {
            // Eski firmware VPI so'rovini bilmaydi — VPR (fiksatsiya indeksi) usuli
            logger?.LogWarning(ex, "VPI qo'llab-quvvatlanmaydi ({func}), VPR usuliga o'tiladi", func);
            return await GetLoadProfilesByFixationIndexAsync(lastReadedDate, deviceDateTime, func);
        }
    }

    /// <summary>
    /// VPIzz(dd.mm.yy,k,n) — sana identifikatori bo'yicha profil o'qish (hujjat:
    /// "4б. Запросы данных профиля по идентификатору фиксации суток в архиве").
    /// Javob identifikatori so'ralgan kunga teng bo'lmasa (qurilma yaqin orqadagi
    /// kunni qaytaradi) — ERR18 uzatiladi, worker shu bo'yicha kunni o'tkazadi.
    /// </summary>
    protected virtual async Task<IEnumerable<(DateTimeOffset dateTime, double value, short status)>> GetLoadProfilesByDateAsync(
        DateTimeOffset lastReadedDate, DateTimeOffset deviceDateTime, string func)
    {
        var queryFunc = ToDateQueryFunction(func);
        var profileDate = DateOnly.FromDateTime(lastReadedDate.Date);

        var interval = await GetProfileIntervalAsync();
        var recordsPerDay = 1440 / interval;
        var (fromRecord, recCount) = ComputeProfileWindow(lastReadedDate, deviceDateTime, interval, recordsPerDay);

        var responceStr = await SendAndGet(CE30XCommand.R1, queryFunc, [CommonIEC61107.ETX],
            profileDate.ToString("dd.MM.yy", CultureInfo.InvariantCulture),
            fromRecord.ToString(), recCount.ToString());
        string[] values = [.. CommonIEC61107.ParseResponseValues(responceStr)];

        if (values.Length == 0) return [];

        // Birinchi qatorda qaytarilgan sana identifikatori bor (hujjat: javob
        // to'liq mos yoki yaqin orqadagi identifikator bo'yicha keladi)
        var first = values[0].Split(',');
        if (first.Length < 3
            || !DateOnly.TryParseExact(first[0], "dd.MM.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var returnedDate)
            || returnedDate != profileDate)
        {
            throw new IecQueryException(
                $"ERR18: {profileDate:dd.MM.yy} kuni uchun profil topilmadi (qaytarilgan identifikator: {first[0]})");
        }

        var dayStart = new DateTimeOffset(profileDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));
        return ParseProfileRecords(values, dayStart, fromRecord);
    }

    /// <summary>
    /// VPRzz(i,k,n) — fiksatsiya indeksi bo'yicha profil o'qish (eski usul).
    /// VPI so'rovini bilmaydigan eski firmware uchun saqlab qolingan
    /// (<see cref="GetLoadProfiles"/> ERR12 da shu yerga o'tadi).
    /// VPR i — fiksatsiya indeksi (0 = joriy sutka); fiksatsiyalar tushib qolganda
    /// indeks siljiydi — LST04 orqali aniqlanadi. Joriy sutka doimo 0; o'tgan kun
    /// topilmasa ERR18 uzatiladi (worker kunni o'tkazib yuboradi).
    /// </summary>
    protected virtual async Task<IEnumerable<(DateTimeOffset dateTime, double value, short status)>> GetLoadProfilesByFixationIndexAsync(
        DateTimeOffset lastReadedDate, DateTimeOffset deviceDateTime, string func)
    {
        var profileDate = DateOnly.FromDateTime(lastReadedDate.Date);
        var isCurrentDay = profileDate == DateOnly.FromDateTime(deviceDateTime.Date);
        var profileIndex = isCurrentDay
            ? 0
            : await ResolveProfileFixationIndexAsync(profileDate);

        var interval = await GetProfileIntervalAsync();
        var recordsPerDay = 1440 / interval;
        var (fromRecord, recCount) = ComputeProfileWindow(lastReadedDate, deviceDateTime, interval, recordsPerDay);

        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], profileIndex.ToString(),
            fromRecord.ToString(), recCount.ToString());
        string[] values = [.. CommonIEC61107.ParseResponseValues(responceStr)];

        if (values.Length == 0) return [];

        // Birinchi qator "dd.mm.yy,X.X,hex" (sana identifikatori bilan),
        // qolganlari "X.X,hex". Oxirgi ikki maydon doim qiymat va status.
        var dayStart = lastReadedDate.StartOfDay();
        var first = values[0].Split(',');
        if (first.Length >= 3 && DateOnly.TryParseExact(first[0], "dd.MM.yy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateFromDevice))
        {
            dayStart = new DateTimeOffset(dateFromDevice.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));
        }

        return ParseProfileRecords(values, dayStart, fromRecord);
    }

    /// <summary>
    /// VPR nomini sana bo'yicha so'rov nomiga (VPI) o'tkazadi ("VPR01" -> "VPI01").
    /// Telemetriya kalitlari VPR nomi bilan qolishi uchun GetLoadProfileFunctions()
    /// o'zgartirilmagan — faqat simdagi so'rov nomi almashadi.
    /// </summary>
    protected static string ToDateQueryFunction(string func) =>
        func.StartsWith("VPR", StringComparison.Ordinal) ? "VPI" + func[3..] : func;

    /// <summary>
    /// So'ralgan kun oralig'i: fromRecord (1 dan, interval bo'yicha) va
    /// qolgan intervalar soni (joriy sutkada — oxirigacha).
    /// </summary>
    private (short fromRecord, int recCount) ComputeProfileWindow(
        DateTimeOffset lastReadedDate, DateTimeOffset deviceDateTime, int interval, int recordsPerDay)
    {
        var fromRecord = (short)((lastReadedDate.Hour * 60 + lastReadedDate.Minute) / interval + 1);
        int recCount = recordsPerDay - (fromRecord - 1);
        var daysAgo = (int)(deviceDateTime.StartOfDay() - lastReadedDate.StartOfDay()).TotalDays;

        if (daysAgo == 0)
        {
            TimeSpan timeSpan = deviceDateTime - lastReadedDate;
            recCount = (int)timeSpan.TotalMinutes / interval;
        }
        if (recCount > recordsPerDay) recCount = recordsPerDay;
        return (fromRecord, recCount);
    }

    /// <summary>
    /// Profil yozuvlarini parse qiladi: birinchi qator "dd.mm.yy,X.X,hex",
    /// qolganlari "X.X,hex" (status — uStatRec_TypeDef, HEX). Buzilgan yozuv
    /// o'tkazib yuboriladi.
    /// </summary>
    private List<(DateTimeOffset dateTime, double value, short status)> ParseProfileRecords(
        string[] values, DateTimeOffset dayStart, short fromRecord)
    {
        List<(DateTimeOffset dateTime, double value, short status)> data = new(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            var splitted = values[i].Split(',');
            var recordDateTime = GetRecordDateTime(dayStart, fromRecord, i);

            try
            {
                var value = double.Parse(splitted[^2], CultureInfo.InvariantCulture);
                var status = ParseProfileStatus(splitted[^1]);
                data.Add((recordDateTime, value, status));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error parsing load profile record: {item}", values[i]);
            }
        }

        return data;
    }

    public async Task<IEnumerable<(long recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func)
    {
        logger?.LogDebug("Getting {func} times.", func);
        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], "0");
        string[] values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();

        var result = new List<(long recNo, DateTimeOffset dateTime, byte status)>();

        foreach (var item in values)
        {
            try
            {
                string[] splitted = item.Split(',');
                long recNo = long.Parse(splitted[0]);
                if (recNo == 0)
                {
                    // Bo'sh jurnal belgisi (hujjat: "При отсутствии записей в журнале
                    // будет выдана одна запись с номером 0") — jimgina o'tkaziladi
                    logger?.LogDebug("Bo'sh jurnal yozuvi (rec=0) o'tkazildi: {func}", func);
                    continue;
                }

                DateOnly date = DateOnly.ParseExact(splitted[1], "dd.MM.yy", CultureInfo.InvariantCulture);
                TimeOnly time = TimeOnly.Parse(splitted[2], CultureInfo.InvariantCulture);
                DateTimeOffset dateTime = new(date.ToDateTime(time), TimeSpan.FromHours(5));
                byte status = ParseJournalStatus(splitted[3]);
                result.Add((recNo, dateTime, status));
            }
            catch (System.Exception ex)
            {
                logger?.LogError(ex, "Error parsing: {item}", item);
            }
        }
        return result;
    }

    /// <summary>
    /// LNE jurnali yozuvidagi hodisa kodini (hex8, HEX) parse qiladi.
    /// LNE04: 0=вкл/1=выкл; LNE05: факт полного пропадания; LNE22: 0=OK, 1=плохое,
    /// 2=отсутствует (rasmiy МЭК protokol tavsifi, eNameLogEvent_TypeDef).
    /// </summary>
    public static byte ParseJournalStatus(string hexStatus)
    {
        var value = Convert.ToInt32(hexStatus.Trim(), 16);
        if (value is < 0 or > byte.MaxValue)
        {
            throw new FormatException($"Hodisa kodi byte chegarasidan tashqari: {hexStatus}");
        }
        return (byte)value;
    }

    /// <summary>
    /// Profil status belgisini (uStatRec_TypeDef, HEX) parse qiladi.
    /// Bitlar ma'nosi: <see cref="ProfileStatusFlags"/>.
    /// </summary>
    public static short ParseProfileStatus(string hexStatus) =>
        (short)(byte)Convert.ToInt32(hexStatus.Trim(), 16);

    /// <summary>
    /// PROFI — profil o'rtalash intervalini o'qiydi (daqiqalarda, HEX; keshlanadi).
    /// Format (hujjat): "PROFI(ht:h1,...)" — ht = interval (hex), qolganlari profil
    /// identifikatorlari. O'qib bo'lmasa <see cref="DefaultLoadProfilePeriodInMinutes"/>.
    /// </summary>
    protected virtual async Task<int> GetProfileIntervalAsync()
    {
        if (averagingIntervalMinutes is int cached) return cached;

        var interval = DefaultLoadProfilePeriodInMinutes;
        try
        {
            var responceStr = await SendAndGet(CE30XCommand.R1, CE308Function.PROFI.ToString(),
                [CommonIEC61107.ETX]);
            var value = CommonIEC61107.ParseResponseValues(responceStr).FirstOrDefault() ?? string.Empty;
            interval = Convert.ToInt32(value.Split(':')[0].Trim(), 16);
            if (interval <= 0 || interval > 1440)
            {
                throw new FormatException($"PROFI qabul qilib bo'lmadi: {value}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "PROFI o'qilmadi, interval {interval} daqiqa deb olinadi",
                DefaultLoadProfilePeriodInMinutes);
            interval = DefaultLoadProfilePeriodInMinutes;
        }

        averagingIntervalMinutes = interval;
        logger?.LogDebug("PROFI = {interval} min, kunlik yozuvlar = {records}", interval, 1440 / interval);
        return interval;
    }

    /// <summary>
    /// LST04 — kunlik profil sanalari (keshlanadi). LST04[0] = joriy sutka
    /// identifikatori, ro'yxatdagi indeks = fiksatsiya indeksi (VPR i).
    /// `forceRefresh: true` keshni majburan yangilaydi (stale kesh himoyasi).
    /// </summary>
    protected virtual async Task<List<DateOnly>> GetProfileDatesAsync(bool forceRefresh = false)
    {
        if (profileDatesCache is not null && !forceRefresh) return profileDatesCache;

        var times = await GetListOfArchiveTimes(CE308Function.LST04.ToString());
        profileDatesCache = ParseArchiveTimes(times, ArchiveType.Day);
        return profileDatesCache;
    }

    /// <summary>
    /// Profil sanasi uchun fiksatsiya indeksini (VPR so'rovidagi i) aniqlaydi.
    /// Keshda topilmasa — bir marta majburan yangilanadi; baribir topilmasa
    /// IecQueryException("ERR18 ...") — worker shu bo'yicha kunni o'tkazib yuboradi.
    /// </summary>
    protected virtual async Task<int> ResolveProfileFixationIndexAsync(DateOnly profileDate)
    {
        var dates = await GetProfileDatesAsync();
        var index = dates.IndexOf(profileDate);

        if (index < 0)
        {
            dates = await GetProfileDatesAsync(forceRefresh: true);
            index = dates.IndexOf(profileDate);
        }

        if (index < 0)
        {
            throw new IecQueryException($"ERR18: {profileDate:dd.MM.yy} kuni LST04 da profil mavjud emas");
        }
        return index;
    }

    public string[] GetPowerStatusFunctions() => [
        CE308Function.LNE04.ToString(),
        CE308Function.LNE05.ToString(),
        CE308Function.LNE22.ToString(),
    ];

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
