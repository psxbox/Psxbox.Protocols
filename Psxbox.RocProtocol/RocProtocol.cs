using Psxbox.Streams;
using System.Data.HashFunction.CRC;
using System.Text;

namespace Psxbox.RocProtocol;

public class RocProtocol : IRocProtocol
{
    public IStream? Stream { get; set; }

    private readonly ICRC _crc = CRCFactory.Instance.Create(CRCConfig.ARC);

    public async Task<byte[]> Requests(ROCAddress rocAddress, ROCAddress hostAddress, RocOpcode opcode, byte[] requestData)
    {
        if (Stream is null)
            throw new Exception($"{nameof(Stream)} null bo'lmasligi kerak!");

        List<byte> payload =
        [
            .. rocAddress.GetAddress,
            .. hostAddress.GetAddress,
            (byte)opcode,
            (byte)requestData.Length,
            .. requestData,
        ];

        var hashValue = _crc.ComputeHash(payload.ToArray());
        payload.AddRange(hashValue.Hash);

        Stream.Flush();
        await Stream.WriteAsync(payload.ToArray());

        var head = new byte[6];
        var readed = await Stream.ReadAsync(head);

        if (readed != head.Length)
            throw new($"Boshlang'ich javob baytlari kam, kutilgan: {head.Length}, kelgan: {readed}");

        var respHostAddress = new ROCAddress(head[0], head[1]);
        var respRocAddress = new ROCAddress(head[2], head[3]);

        if (!rocAddress.Equals(respRocAddress))
            throw new($"ROC adres noto'g'ri, so'rov: {rocAddress}, javob: {respRocAddress}");

        if (!hostAddress.Equals(respHostAddress))
            throw new($"HOST adres noto'g'ri, so'rov: {hostAddress}, javob: {respHostAddress}");

        var respOpcode = (RocOpcode)head[4];

        if (respOpcode == RocOpcode.Opcode255)
        {
            throw new("Transmits error messages by FB Series in response to a request with invalid parameters or format!");
        }
        else if (respOpcode != opcode)
        {
            throw new($"Opcode noto'g'ri keldi, so'rov: {opcode}, javob: {respOpcode}");
        }

        var respDataLen = head[5] + 2;
        var responceData = new byte[respDataLen];
        readed = await Stream.ReadAsync(responceData);

        if (readed != respDataLen)
        {
            throw new($"Javob to'liq kelmadi, kutilgan: {respDataLen}, kelgan: {readed}");
        }

        List<byte> allResponce = [.. head, .. responceData];

        var computedHash = _crc.ComputeHash(allResponce.ToArray()[0..^2]).Hash;
        if (!computedHash.SequenceEqual(responceData[^2..]))
        {
            throw new("CRC mos emas!");
        }

        // if (requestData.Length > 0 && !requestData.SequenceEqual(responceData[0..requestData.Length]))
        // {
        //     throw new("So'rov parametrlari javob parametrlariga mos emas");
        // }

        return responceData;
    }

    public async Task<byte[]> RequestOpcode180(RocDeviceSettings rocDeviceSettings,
                                   IEnumerable<(byte point, byte logic, byte param)> parameters)
    {
        List<byte> request =
        [
            (byte)parameters.Count()
        ];

        foreach (var (point, logic, param) in parameters)
        {
            request.Add(point);
            request.Add(logic);
            request.Add(param);
        }

        var responce = await Requests(rocDeviceSettings.RocAddress, rocDeviceSettings.HostAddress,
            RocOpcode.Opcode180, request.ToArray());

        if (parameters.Count() != responce[0])
        {
            throw new("So'ralgan paramertlar soni kelgan javob bilan bir xil emas");
        }

        return responce;
    }

    public async Task<HistoryPointers> RequestOpcode120(RocDeviceSettings rocDeviceSettings)
    {
        var responce = await Requests(rocDeviceSettings.RocAddress, rocDeviceSettings.HostAddress,
            RocOpcode.Opcode120, []);

        var historyData = new HistoryPointers
        {
            AlarmLogPointer = BitConverter.ToInt16(responce, 0),
            EventLogPointer = BitConverter.ToInt16(responce, 2),
            StationHourlyHistoryIndex = BitConverter.ToInt16(responce, 4),
            UserPeriodicHourlyHistoryIndex = BitConverter.ToInt16(responce, 6),
            UserPeriodicHourlyHistoryLogsCount = BitConverter.ToInt16(responce, 8),
            StationDailyHistoryIndex = responce[12],
            DailyHistoryLogsCount = responce[20],
            HourlyHistoryLogsDays = responce[21],
            UserPeriodicHistoryLogsDays = responce[22]
        };

        return historyData;
    }

    public async Task<byte[]> RequestOpcode130(RocDeviceSettings rocDeviceSettings,
                                   byte typeOfHistory,
                                   byte historyPointNumber,
                                   byte count,
                                   short index)
    {
        byte[] indexBytes = BitConverter.GetBytes(index);

        var responce = await Requests(rocDeviceSettings.RocAddress,
                 rocDeviceSettings.HostAddress,
                 RocOpcode.Opcode130,
                 [typeOfHistory, historyPointNumber, count, indexBytes[0], indexBytes[1]]);

        if (typeOfHistory != responce[0])
        {
            throw new($"Type of History is not equal: {typeOfHistory} != {responce[0]}");
        }
        if (historyPointNumber != responce[1])
        {
            throw new($"History Point Number is not eqial: {historyPointNumber} != {responce[1]}");
        }

        return responce[3..^2];
    }

    public async Task<byte[]> RequestOpcode167(RocDeviceSettings rocDeviceSettings,
                                               RocPointType pointType,
                                               byte logicalNumber,
                                               byte parametersCount,
                                               byte startingIndex)
    {
        var responce = await Requests(rocDeviceSettings.RocAddress,
                                      rocDeviceSettings.HostAddress,
                                      RocOpcode.Opcode167,
                                      [(byte)pointType, logicalNumber, parametersCount, startingIndex]);

        if ((byte)pointType != responce[0])
        {
            throw new($"Point type is not equal: {pointType} != {responce[0]}");
        }
        if (logicalNumber != responce[1])
        {
            throw new($"Logical Number is not eqial: {logicalNumber} != {responce[1]}");
        }
        if (parametersCount != responce[2])
        {
            throw new($"Parameters count is not eqial: {parametersCount} != {responce[1]}");
        }
        if (startingIndex != responce[3])
        {
            throw new($"Starting index is not eqial: {startingIndex} != {responce[1]}");
        }

        return responce[4..^2];
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stream = null;
        }
    }

    public async Task<IEnumerable<AlarmRecord>> RequestOpcode121(RocDeviceSettings rocDeviceSettings,
                                                                 byte numberOfAlarms,
                                                                 short startingPointer)
    {
        var startPointerBytes = BitConverter.GetBytes(startingPointer);

        var responce = await Requests(rocDeviceSettings.RocAddress,
                                      rocDeviceSettings.HostAddress,
                                      RocOpcode.Opcode121,
                                      [numberOfAlarms, startPointerBytes[0], startPointerBytes[1]]);

        var responsedAlarmsCount = responce[0];

        List<AlarmRecord> records = [];

        for (int i = 0; i < responsedAlarmsCount; i++)
        {
            var index = i * 22 + 5;
            AlarmRecord alarmRecord = new AlarmRecord
            {
                AlarmType = responce[index],
                AlarmCode = responce[index + 1],
                Seconds = responce[index + 2],
                Minutes = responce[index + 3],
                Hours = responce[index + 4],
                Day = responce[index + 5],
                Month = responce[index + 6],
                Year = responce[index + 7],
                Tag = Encoding.ASCII.GetString(responce[(index + 8)..(index + 18)]),
                Value = BitConverter.ToSingle(responce, index + 18)
            };
            records.Add(alarmRecord);
        }
        return records;
    }
}