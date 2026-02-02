# Psxbox.RocProtocol

**Psxbox.RocProtocol** - Fisher ROC (Remote Operations Controller) protokoli implementatsiyasi. ROC protokoli neft va gaz sanoatida keng qo'llaniladigan Fisher ROC qurilmalari bilan aloqa qilish uchun mo'ljallangan.

## Xususiyatlari

- ?? **ROC Protocol** - Fisher ROC qurilmalari uchun protokol
- ?? **Opcode qo'llab-quvvatlash** - Opcode 180, 120, 130, 181 va boshqalar
- ?? **Parametrlarni o'qish** - Analog, diskret, akkumulyator parametrlar
- ?? **History ma'lumotlari** - Soatlik va kunlik arxiv
- ?? **Alarm ma'lumotlari** - Hodisalar va alarmlar
- ?? **CRC tekshiruvi** - CRC-16 (DNP3) algoritmidan foydalanish
- ? **Async/Await** - To'liq asinxron operatsiyalar

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.RocProtocol/Psxbox.RocProtocol.csproj
```

## Bog'liqliklar

- `Psxbox.Streams` - Transport qatlami
- `Psxbox.Utils` - Yordamchi funksiyalar
- `Microsoft.Extensions.Logging.Abstractions` (v10.0.0) - Logging
- `System.Data.HashFunction.CRC` (v2.0.0) - CRC hisoblash

## Qo'llab-quvvatlanuvchi Qurilmalar

- Fisher ROC809
- Fisher ROC827
- Fisher ROC300-Series
- Fisher ROC800-Series
- Fisher FloBoss 107
- Fisher FloBoss 103/104

## Foydalanish

### ROC Protokolni Yaratish

```csharp
using Psxbox.RocProtocol;
using Psxbox.Streams;
using Microsoft.Extensions.Logging;

// Serial yoki TCP stream yaratish
var stream = new SerialStream("COM3,19200,8,N,1");
await stream.ConnectAsync();

// ROC protokol obyektini yaratish
var logger = loggerFactory.CreateLogger<RocProtocol>();
var rocProtocol = new RocProtocol(stream, logger);
```

### ROC Manzillari

```csharp
// Qurilma manzillari
var rocAddress = new ROCAddress
{
    Group = 1,      // Group (0-255)
    Unit = 1        // Unit (0-255)
};

var hostAddress = new ROCAddress
{
    Group = 240,    // Host group (odatda 240)
    Unit = 240      // Host unit (odatda 240)
};
```

### Opcode 180 - Parametrlarni O'qish

Opcode 180 bir nechta parametrlarni bitta so'rovda o'qish imkonini beradi:

```csharp
var settings = new RocDeviceSettings
{
    Group = 1,
    Unit = 1,
    HostGroup = 240,
    HostUnit = 240
};

// Parametrlar ro'yxati: (Point, Logic, Parameter)
var parameters = new List<(byte, byte, byte)>
{
    (0, 0, 1),   // Point 0, Logic 0, Parameter 1
    (0, 0, 2),   // Point 0, Logic 0, Parameter 2
    (1, 0, 1),   // Point 1, Logic 0, Parameter 1
};

byte[] response = await rocProtocol.RequestOpcode180(settings, parameters);

// Response parsing
int offset = 0;
foreach (var param in parameters)
{
    float value = BitConverter.ToSingle(response, offset);
    Console.WriteLine($"Point {param.Item1}, Logic {param.Item2}, Param {param.Item3}: {value}");
    offset += 4;
}
```

### Opcode 120 - History Pointerlarni O'qish

```csharp
// History pointerlarni olish
HistoryPointers pointers = await rocProtocol.RequestOpcode120(settings);

Console.WriteLine($"Group 1 Hour Pointer: {pointers.Group1HourPointer}");
Console.WriteLine($"Group 1 Day Pointer: {pointers.Group1DayPointer}");
Console.WriteLine($"Max Hour Logs: {pointers.MaxHourLogs}");
Console.WriteLine($"Max Day Logs: {pointers.MaxDayLogs}");
```

### Opcode 130 - History Ma'lumotlarini O'qish

```csharp
// Soatlik ma'lumotlarni o'qish
byte[] hourlyData = await rocProtocol.RequestOpcode130(
    rocDeviceSettings: settings,
    typeOfHistory: 0,           // 0 = Standard (hourly/daily)
    historyPointNumber: 0,      // History point raqami (0-59)
    count: 24,                  // 24 soatlik ma'lumot
    index: 0                    // Boshlanish indexi (0-839 soatlik uchun)
);

// Kunlik ma'lumotlarni o'qish
byte[] dailyData = await rocProtocol.RequestOpcode130(
    rocDeviceSettings: settings,
    typeOfHistory: 0,
    historyPointNumber: 0,
    count: 30,                  // 30 kunlik ma'lumot
    index: 840                  // 840-874 kunlik uchun
);

// Ma'lumotlarni parse qilish
for (int i = 0; i < hourlyData.Length; i += 4)
{
    float value = BitConverter.ToSingle(hourlyData, i);
    Console.WriteLine($"Soat {i/4}: {value}");
}
```

### RocMaster - Yuqori Darajali API

```csharp
var rocMaster = new RocMaster(stream, settings, logger);

// Parametrlarni o'qish
var values = await rocMaster.ReadParametersAsync(new[]
{
    (point: 0, logic: 0, param: 1),
    (point: 0, logic: 0, param: 2),
    (point: 1, logic: 0, param: 1)
});

// Natijalarni ko'rish
foreach (var (param, value) in values)
{
    Console.WriteLine($"P{param.point}L{param.logic}P{param.param}: {value}");
}

// History ma'lumotlarni olish
var history = await rocMaster.ReadHistoryAsync(
    pointNumber: 0,
    count: 24,
    startIndex: 0
);
```

### Alarmlar bilan Ishlash

```csharp
// Alarmlarni o'qish (Opcode 121)
byte[] alarmData = await rocProtocol.Requests(
    rocAddress: rocAddress,
    hostAddress: hostAddress,
    opcode: RocOpcode.Opcode121,
    requestData: new byte[] { /* request data */ }
);

// Alarm ma'lumotlarini parse qilish
var alarmRecord = new AlarmRecord
{
    AlarmCode = alarmData[0],
    PointNumber = alarmData[1],
    ParameterNumber = alarmData[2],
    Timestamp = ParseTimestamp(alarmData, 3),
    Value = BitConverter.ToSingle(alarmData, 11)
};

Console.WriteLine($"Alarm: Code={alarmRecord.AlarmCode}, Point={alarmRecord.PointNumber}");
```

## ROC Opcode'lar

| Opcode | Tavsif |
|--------|--------|
| 6 | Single Parameter Read |
| 7 | Multiple Parameter Read |
| 10 | Single Parameter Write |
| 11 | Multiple Parameter Write |
| 17 | Set Date and Time |
| 120 | History Pointer Information |
| 121 | Current Alarm Record |
| 122 | Alarm History Retrieve |
| 126 | Continuous History Segment |
| 130 | History Retrieve |
| 131 | Extended History Retrieve |
| 180 | User Opcode 180 (Multiple Read) |
| 181 | User Opcode 181 (Multiple Write) |

## ROC Point Turlari

```csharp
public enum RocPointType
{
    Analog = 0,           // Analog kirish
    Discrete = 1,         // Diskret kirish
    Accumulator = 2,      // Akkumulyator
    Calculated = 3,       // Hisoblangan
    Station = 4,          // Stansiya parametrlari
    DateTime = 5          // Sana va vaqt
}
```

## MyGateway Loyihasida Foydalanish

MyGateway.Worker.Roc tizimida ROC protokoli quyidagi maqsadlar uchun ishlatiladi:

- ??? **Neft va gaz** - Quvur liniyalari va quduqlar monitoringi
- ?? **Flow measurement** - Gaz va neft oqimini o'lchash
- ??? **Sensor monitoring** - Bosim, harorat, oqim sensorlari
- ?? **Historical data** - Soatlik va kunlik statistika
- ?? **Alarms** - Kritik hodisalar haqida xabardorlik

### Device Template Example

```json
{
  "protocol": "roc",
  "transport": "serial",
  "port": "COM3",
  "baudRate": 19200,
  "group": 1,
  "unit": 1,
  "hostGroup": 240,
  "hostUnit": 240,
  "cron": "0 */1 * * * ?",
  "telemetry": [
    {
      "name": "flow_rate",
      "opcode": 180,
      "point": 0,
      "logic": 0,
      "parameter": 1,
      "dataType": "float"
    },
    {
      "name": "pressure",
      "opcode": 180,
      "point": 1,
      "logic": 0,
      "parameter": 1,
      "dataType": "float"
    }
  ],
  "history": {
    "enabled": true,
    "points": [0, 1, 2],
    "interval": "hourly"
  }
}
```

## Xatolarni Boshqarish

```csharp
try
{
    byte[] data = await rocProtocol.RequestOpcode180(settings, parameters);
}
catch (TimeoutException ex)
{
    logger.LogError("ROC qurilma javob bermadi");
}
catch (InvalidDataException ex)
{
    logger.LogError($"CRC xatosi: {ex.Message}");
}
catch (Exception ex)
{
    logger.LogError($"Umumiy xato: {ex.Message}");
}
```

## CRC Hisoblash

ROC protokoli CRC-16 (DNP3) algoritmidan foydalanadi:

```csharp
// CRC hisoblash (avtomatik bajariladi)
byte[] message = new byte[] { /* ROC message */ };
ushort crc = CalculateCRC16(message);
```

## Litsenziya

MIT License

## Qo'shimcha Ma'lumot

ROC protokol spetsifikatsiyasi: Fisher ROC Protocol User Manual
