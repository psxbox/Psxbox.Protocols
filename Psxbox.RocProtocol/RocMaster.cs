using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using Psxbox.Utils;
using Psxbox.Utils.Helpers;
using System.Collections;
using System.Runtime.InteropServices;

namespace Psxbox.RocProtocol;

public class RocMaster
{
    private const int DATETIME_POINT = 254;

    // private readonly IStream _stream;
    private readonly IRocProtocol rocProtocol;
    private readonly CancellationToken? cancellationToken;
    private readonly ILogger? logger;

    public IRocProtocol RocProtocol => rocProtocol;

    public RocMaster(IStream stream, IRocProtocol rocProtocol, CancellationToken? cancellationToken = null, ILogger? logger = null)
    {
        // this._stream = stream;
        this.rocProtocol = rocProtocol;
        this.cancellationToken = cancellationToken;
        this.logger = logger;
        rocProtocol.Stream = stream;
    }

    /// <summary>
    /// Parametrni joriy qiymatini o'qish
    /// </summary>
    /// <typeparam name="T">Qiymat tipi</typeparam>
    /// <param name="rocDeviceSettings">ROC va HOST adreslar</param>
    /// <param name="typeOfPoint"></param>
    /// <param name="point"></param>
    /// <param name="logic"></param>
    /// <param name="param"></param>
    /// <param name="byteOrder"></param>
    /// <returns></returns>
    public async Task<T> GetParameter<T>(RocDeviceSettings rocDeviceSettings,
                             byte point,
                             byte logic,
                             byte param,
                             string? byteOrder = null) where T : struct
    {
        byte[] responce = (await rocProtocol.RequestOpcode180(rocDeviceSettings,
            new List<(byte, byte, byte)>() { (point, logic, param) }))[4..]
            .GetOrdered(byteOrder);

        return responce.ConvertTo<T>(0);
    }

    public async Task<Dictionary<string, object>> GetParameters(RocDeviceSettings rocDeviceSettings,
        IEnumerable<(string name, byte point, byte logic, byte param, string valueType, string? byteOrder, int length)> parameters)
    {
        var request = parameters.Select(p => (p.point, p.logic, p.param));
        var responce = (await rocProtocol.RequestOpcode180(rocDeviceSettings, request))[1..].ToList();

        Dictionary<string, object> result = [];

        foreach (var (name, point, logic, param, valueType, byteOrder, length) in parameters)
        {
            if (cancellationToken?.IsCancellationRequested == true)
            {
                return result;
            }

            if ((responce[0], responce[1], responce[2]) != (point, logic, param))
            {
                throw new Exception("So'ralgan parameter kelgan parameterga mos emas!");
            }

            responce.RemoveRange(0, 3);

            int valueSize = valueType == "ascii" ? length : ValueUtils.GetValueSize(valueType);
            object value = ValueUtils.ConvertValueByType(responce.Take(valueSize).ToArray().GetOrdered(byteOrder), valueType);
            if (!ValueUtils.IsFinite(value))
            {
                result.Add(name, "Infinite");
            }
            else
            {
                result.Add(name, value);
            }
            responce.RemoveRange(0, valueSize);
        }
        return result;
    }

    /// <summary>
    /// Arxivdagi sanalarni olish
    /// </summary>
    /// <param name="rocDeviceSettings">ROC va HOST adreslar</param>
    /// <param name="typeOfHistory">Type of History: 0 = Hourly or Daily (Standard) 1 = Extended</param>
    /// <param name="valuesCount">Number of history values requested (maximum 60)</param>
    /// <param name="fromIndex">Starting history index (0-839 for hourly, 840-874 for daily)</param>
    /// <returns>Collection of requested history values</returns>
    public async Task<IEnumerable<DateTimeOffset>> GetHistoryDateTimes(RocDeviceSettings rocDeviceSettings,
                                                           byte typeOfHistory,
                                                           byte valuesCount,
                                                           short fromIndex)
    {
        var responce = await rocProtocol.RequestOpcode130(rocDeviceSettings,
                                                          typeOfHistory,
                                                          DATETIME_POINT,
                                                          valuesCount,
                                                          fromIndex);
        List<DateTimeOffset> historyDateTimes = [];
        for (int i = 0; i < valuesCount; i++)
        {
            var index = i * 4;
            var month = responce[index + 3];
            var day = responce[index + 2];
            var hour = responce[index + 1];
            var minute = responce[index];

            if (month == 0 || day == 0)
            {
                continue;
            }

            var dt = new DateTimeOffset(DateTimeOffset.Now.Year, month, day, hour, minute, 0, TimeSpan.FromHours(5));
            if (dt > DateTimeOffset.Now)
            {
                dt = dt.AddYears(-1);
            }
            historyDateTimes.Add(dt);
        }

        return historyDateTimes;
    }

    /// <summary>
    /// Arxivdagi ma'lumotlarni o'qish
    /// </summary>
    /// <typeparam name="T">Ma'lumot tipi</typeparam>
    /// <param name="rocDeviceSettings">ROC va HOST adreslar</param>
    /// <param name="typeOfHistory">Type of History: 0 = Hourly or Daily (Standard) 1 = Extended</param>
    /// <param name="historyPointNumber">History Point Number (0-59, for Timestamp specify 254)</param>
    /// <param name="valuesCount">Number of history values requested (maximum 60)</param>
    /// <param name="fromIndex">Starting history index (0-839 for hourly, 840-874 for daily)</param>
    /// <returns>Collection of requested history values</returns>
    public async Task<IEnumerable<T>> GetHistoryValues<T>(RocDeviceSettings rocDeviceSettings,
                                                          byte typeOfHistory,
                                                          byte historyPointNumber,
                                                          byte valuesCount,
                                                          short fromIndex) where T : struct
    {
        byte[] responce = await rocProtocol.RequestOpcode130(rocDeviceSettings,
                                                             typeOfHistory,
                                                             historyPointNumber,
                                                             valuesCount,
                                                             fromIndex);
        List<T> historyValues = [];
        for (int i = 0; i < valuesCount; i++)
        {
            var index = i * Marshal.SizeOf<T>();
            historyValues.Add(responce.ConvertTo<T>(index));
        }
        return historyValues;
    }

    /// <summary>
    /// Arxivlangan parametrlar ro'yxatini olish
    /// </summary>
    /// <param name="rocDeviceSettings"></param>
    /// <returns></returns>
    public async Task<IEnumerable<HistoryParams>> GetHistoryParams(RocDeviceSettings rocDeviceSettings)
    {
        List<HistoryParams> historyParamsList = [];

        for (byte logicalNumber = 0; logicalNumber < 3; logicalNumber++)
        {
            byte[] responce = await rocProtocol.RequestOpcode167(rocDeviceSettings, RocPointType.HistoryParameters,
                logicalNumber: logicalNumber, parametersCount: 60, startingIndex: 0);


            for (int i = 0; i < 15; i++)
            {
                int index = i * 8;
                HistoryParams historyParams = new()
                {
                    PointTagIndentification = (responce[index], responce[index + 1], responce[index + 2]),
                    PointPath = (responce[index + 3], responce[index + 4], responce[index + 5]),
                    ArchiveType = responce[index + 6],
                    AvgOrRate = responce[index + 7],
                };
                historyParamsList.Add(historyParams);
            }
        }

        return historyParamsList;
    }

    /// <summary>
    /// Arxiv ma'lumotlarini olish
    /// </summary>
    /// <param name="rocDeviceSettings"></param>
    /// <param name="typeOfHistory">Arxiv turi (daily, hourly)</param>
    /// <param name="parameters">Olinishi kerak bo'lgan parametrlar ro'yxati</param>
    /// <param name="fromIndex"></param>
    /// <param name="toIndex"></param>
    /// <param name="fullRead"></param>
    /// <returns>(vaqt, teg nomi, qiymati)</returns>
    public async IAsyncEnumerable<(DateTimeOffset, string, object)> GetHistories(
        RocDeviceSettings rocDeviceSettings,
        string typeOfHistory,
        IEnumerable<(string name, byte point, byte logic, byte param, string valueType, string? byteOrder)> parameters,
        short fromIndex, short toIndex, bool fullRead)
    {
        bool historyTimesReaded = false;
        IList<HistoryParams> historyParamsList = (await GetHistoryParams(rocDeviceSettings))
            .Where(x => x.PointTagIndentification != (0, 0, 0))
            .ToList();
        HistoryPointers historyPointers = await rocProtocol.RequestOpcode120(rocDeviceSettings);

        List<DateTimeOffset> dateTimeOffsets = [];

        if (typeOfHistory.Equals("daily", StringComparison.InvariantCultureIgnoreCase))
        {
            if (fromIndex < 0 || toIndex >= historyPointers.DailyHistoryLogsCount)
            {
                throw new IndexOutOfRangeException("daily history indexes out of range");
            }

            var count = fullRead ? historyPointers.DailyHistoryLogsCount
                : toIndex < fromIndex ? (toIndex + historyPointers.DailyHistoryLogsCount) - fromIndex : toIndex - fromIndex;

            foreach (var (name, point, logic, param, valueType, byteOrder) in parameters)
            {
                if (cancellationToken?.IsCancellationRequested == true)
                {
                    yield break;
                }

                if (historyParamsList.All(x => x.PointPath != (point, logic, param)))
                {
                    logger?.LogError("Bu parameter {params} arxivlanmagan!", (point, logic, param));
                    continue;
                }

                var historyParams = historyParamsList.FirstOrDefault(x => x.PointPath == (point, logic, param));
                var historyPointIndex = historyParamsList.IndexOf(historyParams);

                bool ok;
                if (!historyTimesReaded)
                {
                    ok = false;
                    for (int i = 0; i < 2; i++)
                    {
                        try
                        {
                            dateTimeOffsets = (await GetHistoryDateTimes(rocDeviceSettings, 0, (byte)count, (short)(fromIndex + 840)))
                                .ToList();
                            ok = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError("{ex}", ex);
                        }
                    }
                    if (!ok) yield break;
                    historyTimesReaded = true;
                }

                ArrayList responceValues = new();

                ok = false;
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        await GetValuesFromDevice(rocDeviceSettings,
                                                  valueType,
                                                  historyPointIndex,
                                                  (byte)count,
                                                  (short)(fromIndex + 840),
                                                  responceValues);
                        ok = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError("{ex}", ex);
                    }
                }
                if (!ok) yield break;

                for (int i = 0; i < dateTimeOffsets.Count; i++)
                {
                    yield return (dateTimeOffsets[i], name, responceValues[i]!);
                }
            }
        }
        else if (typeOfHistory.Equals("hourly", StringComparison.InvariantCultureIgnoreCase))
        {
            if (fromIndex < 0 || toIndex > 839)
            {
                throw new IndexOutOfRangeException("hourly history indexes out of range");
            }

            short hourlyAllRecords = fullRead ? (short)(historyPointers.HourlyHistoryLogsDays * 24) :
                (short)(toIndex < fromIndex ? (toIndex + 840) - fromIndex : toIndex - fromIndex);

            if (hourlyAllRecords == 0) yield break;

            if (hourlyAllRecords > 840)
            {
                hourlyAllRecords = 840;
            }

            short currentRecordIndex = 0;

            while (cancellationToken?.IsCancellationRequested != true)
            {
                short recCount = (short)(hourlyAllRecords - currentRecordIndex);
                if (recCount > 60)
                {
                    recCount = 60;
                }

                bool ok = false;
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        dateTimeOffsets = (await GetHistoryDateTimes(rocDeviceSettings, 0, (byte)recCount, fromIndex))
                            .ToList();
                        ok = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError("{ex}", ex);
                    }
                }
                if (!ok) yield break;

                foreach (var (name, point, logic, param, valueType, byteOrder) in parameters)
                {
                    if (cancellationToken?.IsCancellationRequested == true)
                    {
                        yield break;
                    }

                    if (historyParamsList.All(x => x.PointPath != (point, logic, param)))
                    {
                        logger?.LogError("Bu parameter {params} arxivlanmagan!", (point, logic, param));
                        continue;
                    }

                    var historyParams = historyParamsList.FirstOrDefault(x => x.PointPath == (point, logic, param));
                    var historyPointIndex = historyParamsList.IndexOf(historyParams);

                    ArrayList responceValues = new();

                    ok = false;
                    for (int i = 0; i < 2; i++)
                    {
                        try
                        {
                            await GetValuesFromDevice(rocDeviceSettings,
                                                      valueType,
                                                      historyPointIndex,
                                                      (byte)recCount,
                                                      fromIndex,
                                                      responceValues);
                            ok = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError("{ex}", ex);
                        }
                    }
                    if (!ok) yield break;

                    for (int i = 0; i < dateTimeOffsets.Count; i++)
                    {
                        yield return (dateTimeOffsets[i], name, responceValues[i]!);
                    }

                }

                currentRecordIndex += recCount;
                fromIndex += recCount;

                if (fromIndex >= 840) fromIndex -= 840;

                if (currentRecordIndex >= hourlyAllRecords)
                {
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// <paramref name="historyPointIndex"/> (arxiv ustuni indeksi) bo'yicha arxiv ma'lumotini o'qish
    /// </summary>
    /// <param name="rocDeviceSettings"></param>
    /// <param name="valueType">Qiymat tipi</param>
    /// <param name="historyPointIndex">Arxivlangan parameter usuni indeki</param>
    /// <param name="valuesCount">O'qilishi kerak bo'lgan qiymatlar soni, max 60</param>
    /// <param name="fromIndex">Qaysi qatordan boshlab o'qish kerak</param>
    /// <param name="responceValues">Javoblar</param>
    /// <exception cref="Exception"></exception>
    private async Task GetValuesFromDevice(RocDeviceSettings rocDeviceSettings, string valueType,
        int historyPointIndex, byte valuesCount, short fromIndex, ArrayList responceValues)
    {
        switch (valueType.ToLower())
        {
            case "int16":
                responceValues.AddRange(
                    (await GetHistoryValues<short>(rocDeviceSettings, 0, (byte)historyPointIndex, valuesCount, fromIndex))
                        .ToList()
                );
                break;
            case "int32":
                responceValues.AddRange(
                    (await GetHistoryValues<int>(rocDeviceSettings, 0, (byte)historyPointIndex, valuesCount, fromIndex))
                        .ToList()
                );
                break;
            case "float":
            case "single":
                responceValues.AddRange(
                    (await GetHistoryValues<float>(rocDeviceSettings, 0, (byte)historyPointIndex, valuesCount, fromIndex))
                        .ToList()
                );
                break;
            default:
                throw new Exception($"Unknown type {valueType}");
        }
    }

    /// <summary>
    /// Alarmlarni olish
    /// </summary>
    /// <param name="rocDeviceSettings"></param>
    /// <param name="lastPoint">O'qilgan oxirgi alarm indeksi</param>
    /// <param name="fullRead">To'liq o'rish kerakmi</param>
    /// <returns></returns>
    public async IAsyncEnumerable<AlarmResult> GetAlarms(RocDeviceSettings rocDeviceSettings,
                                                         short lastPoint,
                                                         bool fullRead)
    {
        HistoryPointers historyPointers = await rocProtocol.RequestOpcode120(rocDeviceSettings);

        //HistoryPointers historyPointers = new HistoryPointers()
        //{
        //    AlarmLogPointer = 110
        //};

        var currentHistoryPoint = historyPointers.AlarmLogPointer;

        if (currentHistoryPoint == lastPoint) yield break;

        short count = 10;
        short readPointer = currentHistoryPoint;
        short endPoint = fullRead ? currentHistoryPoint : lastPoint;


        bool first = true;

        while (!(cancellationToken?.IsCancellationRequested ?? false))
        {
            if (!fullRead)
            {
                int fix;
                if (first)
                {
                    fix = readPointer < lastPoint ? lastPoint - 240 : lastPoint;
                }
                else
                {
                    fix = lastPoint;
                }

                count = (short)(readPointer - fix < 10 ? readPointer - lastPoint : 10);
                if (count < 0)
                {
                    count = (short)(240 + count);
                }
            }

            readPointer -= count;
            if (readPointer < 0)
            {
                readPointer = (short)(240 + readPointer);
                first = false;
            }

            IEnumerable<AlarmRecord> result = await rocProtocol.RequestOpcode121(rocDeviceSettings, (byte)count, readPointer);

            foreach (var item in result)
            {
                if (cancellationToken?.IsCancellationRequested == true) yield break;

                DateTimeOffset dateTimeOffset = new(item.Year + 2000,
                                                    item.Month,
                                                    item.Day,
                                                    item.Hours,
                                                    item.Minutes,
                                                    item.Seconds,
                                                    TimeSpan.FromHours(5));

                (string alarmType, string alarmSetClear) = AlarmTypeCode.GetAlarmTypeString(item.AlarmType);
                string alarmCode = AlarmTypeCode.GetAlarmCodeString(item.AlarmType, item.AlarmCode);

                yield return new AlarmResult(dateTimeOffset, item.Tag, alarmSetClear, item.Value, $"{alarmType} {alarmCode}");
            }


            if (readPointer == endPoint)
                yield break;
        }
    }
}
