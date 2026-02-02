# Psxbox.CustomTE73Protocol

**Psxbox.CustomTE73Protocol** - TE73 elektr hisoblagichlari uchun maxsus protokol implementatsiyasi. Bu protokol TE73 modelining o'ziga xos aloqa protokolidan foydalanadi.

## Xususiyatlari

- ?? **TE73 protokol** - Maxsus TE73 hisoblagichlari protokoli
- ?? **Register o'qish** - Turli registerlardan ma'lumot olish
- ?? **BCD encoding** - Binary-Coded Decimal ma'lumot formati
- ?? **Real-time ma'lumotlar** - Joriy energiya va quvvat ko'rsatkichlari
- ? **Checksum validatsiya** - Ma'lumot to'g'riligini tekshirish
- ? **Async/Await** - To'liq asinxron operatsiyalar

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.CustomTE73Protocol/Psxbox.CustomTE73Protocol.csproj
```

## Bog'liqliklar

- `Psxbox.Streams` - Transport qatlami

## Qo'llab-quvvatlanuvchi Modellar

- TE73 (Turli modifikatsiyalar)
- TE73M
- TE73-1
- TE73-3

## Foydalanish

### TE73 Reader Yaratish

```csharp
using Psxbox.CustomTE73Protocol;
using Psxbox.Streams;

// Serial port orqali ulanish
var stream = new SerialStream("COM3,9600,8,N,1");
await stream.ConnectAsync();

var reader = new ReaderTE73(stream);
```

### Register Ma'lumotlarini O'qish

```csharp
// Hisoblagich ID (meter ID)
string meterId = "12345678";

// Register raqami
string register = "0001";  // Energiya registeri

// Ma'lumotni o'qish
string value = await reader.ReadDataAsync(
    id: meterId,
    register: register,
    ignoreLastBit: false
);

Console.WriteLine($"Register {register}: {value}");
```

### Energiya Ma'lumotlarini O'qish

```csharp
// Aktiv energiya
string activeEnergy = await reader.ReadDataAsync(meterId, "0001");
Console.WriteLine($"Aktiv energiya: {activeEnergy} kWh");

// Reaktiv energiya
string reactiveEnergy = await reader.ReadDataAsync(meterId, "0002");
Console.WriteLine($"Reaktiv energiya: {reactiveEnergy} kVArh");

// To'liq energiya
string totalEnergy = await reader.ReadDataAsync(meterId, "0003");
Console.WriteLine($"To'liq energiya: {totalEnergy} kVAh");
```

### Joriy Qiymatlarni O'qish

```csharp
// Kuchlanish
string voltage = await reader.ReadDataAsync(meterId, "0010");
Console.WriteLine($"Kuchlanish: {voltage}V");

// Tok
string current = await reader.ReadDataAsync(meterId, "0011");
Console.WriteLine($"Tok: {current}A");

// Quvvat
string power = await reader.ReadDataAsync(meterId, "0012");
Console.WriteLine($"Quvvat: {power}W");

// Chastota
string frequency = await reader.ReadDataAsync(meterId, "0013");
Console.WriteLine($"Chastota: {frequency}Hz");

// Quvvat faktori
string powerFactor = await reader.ReadDataAsync(meterId, "0014");
Console.WriteLine($"Quvvat faktori: {powerFactor}");
```

### Tarif Ma'lumotlari

```csharp
// Tarif 1 energiya
string tariff1 = await reader.ReadDataAsync(meterId, "0020");
Console.WriteLine($"Tarif 1: {tariff1} kWh");

// Tarif 2 energiya
string tariff2 = await reader.ReadDataAsync(meterId, "0021");
Console.WriteLine($"Tarif 2: {tariff2} kWh");

// Tarif 3 energiya
string tariff3 = await reader.ReadDataAsync(meterId, "0022");
Console.WriteLine($"Tarif 3: {tariff3} kWh");

// Tarif 4 energiya
string tariff4 = await reader.ReadDataAsync(meterId, "0023");
Console.WriteLine($"Tarif 4: {tariff4} kWh");
```

### 3-Fazali Hisoblagichlar

```csharp
// Faza A kuchlanish
string voltageA = await reader.ReadDataAsync(meterId, "0030");

// Faza B kuchlanish
string voltageB = await reader.ReadDataAsync(meterId, "0031");

// Faza C kuchlanish
string voltageC = await reader.ReadDataAsync(meterId, "0032");

Console.WriteLine($"Kuchlanish: A={voltageA}V, B={voltageB}V, C={voltageC}V");

// Faza toklari
string currentA = await reader.ReadDataAsync(meterId, "0033");
string currentB = await reader.ReadDataAsync(meterId, "0034");
string currentC = await reader.ReadDataAsync(meterId, "0035");

Console.WriteLine($"Tok: A={currentA}A, B={currentB}A, C={currentC}A");
```

### Ignore Last Bit

Ba'zi registerlar uchun oxirgi bitni e'tiborsiz qoldirish kerak:

```csharp
// Oxirgi bitni ignore qilish
string value = await reader.ReadDataAsync(
    id: meterId,
    register: "0015",
    ignoreLastBit: true
);
```

## Protokol Tuzilishi

### So'rov Formati

```
0x68 [Meter ID] 0x68 [CMD] [Register] [Checksum] 0x16
```

### Javob Formati

```
0x68 [Meter ID] 0x68 0x91 [Data Length] [Data] [Checksum] 0x16
```

### Meter ID Format

Meter ID BCD formatida kodlangan 4 baytli raqam:
- Misol: `12345678` ? `0x12 0x34 0x56 0x78`
- Byte order: Little Endian (reversed)

### Checksum

TE73 protokoli o'zining maxsus checksum algoritmidan foydalanadi.

## MyGateway Loyihasida Foydalanish

MyGateway.Worker.CustomProtocols tizimida TE73 protokoli quyidagi maqsadlar uchun ishlatiladi:

- ? **Energiya o'lchash** - Elektr energiya iste'molini monitoring
- ?? **Mahalliy tarmoqlar** - Uy va kichik binolar
- ?? **Sub-metering** - Ikkilamchi hisoblagichlar
- ?? **Billing** - Tarif zonalari bo'yicha hisob-kitob

### Device Template Example

```json
{
  "protocol": "te73",
  "transport": "serial",
  "port": "COM3",
  "baudRate": 9600,
  "dataBits": 8,
  "parity": "None",
  "stopBits": 1,
  "meterId": "12345678",
  "cron": "0 */15 * * * ?",
  "telemetry": [
    {
      "name": "active_energy",
      "register": "0001",
      "unit": "kWh"
    },
    {
      "name": "voltage",
      "register": "0010",
      "unit": "V"
    },
    {
      "name": "current",
      "register": "0011",
      "unit": "A"
    },
    {
      "name": "power",
      "register": "0012",
      "unit": "W"
    },
    {
      "name": "power_factor",
      "register": "0014",
      "unit": ""
    }
  ]
}
```

### Worker Service Example

```csharp
public class TE73Worker : BackgroundService
{
    private readonly ILogger<TE73Worker> _logger;
    private ReaderTE73 _reader;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stream = new SerialStream("COM3,9600,8,N,1");
        await stream.ConnectAsync();
        
        _reader = new ReaderTE73(stream);
        string meterId = "12345678";
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Energiyani o'qish
                string energy = await _reader.ReadDataAsync(meterId, "0001");
                string voltage = await _reader.ReadDataAsync(meterId, "0010");
                string current = await _reader.ReadDataAsync(meterId, "0011");
                
                _logger.LogInformation(
                    $"Energiya: {energy}kWh, Kuchlanish: {voltage}V, Tok: {current}A"
                );
                
                // Telemetriya yuborish
                await PublishTelemetryAsync(energy, voltage, current);
            }
            catch (Exception ex)
            {
                _logger.LogError($"TE73 o'qishda xato: {ex.Message}");
            }
            
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
        
        await stream.CloseAsync();
    }
}
```

## Register Ro'yxati

Quyida eng ko'p ishlatiladigan registerlar ro'yxati:

| Register | Tavsif | Birlik |
|----------|--------|--------|
| 0001 | Aktiv energiya (A+) | kWh |
| 0002 | Reaktiv energiya (R+) | kVArh |
| 0003 | To'liq energiya (S) | kVAh |
| 0010 | Kuchlanish | V |
| 0011 | Tok | A |
| 0012 | Aktiv quvvat | W |
| 0013 | Chastota | Hz |
| 0014 | Quvvat faktori | - |
| 0020-0023 | Tarif 1-4 energiyasi | kWh |
| 0030-0032 | Faza A/B/C kuchlanish | V |
| 0033-0035 | Faza A/B/C tok | A |

**Eslatma**: Aniq register ro'yxati hisoblagich modeliga bog'liq. Dokumentatsiyaga qarang.

## Serial Port Konfiguratsiyasi

TE73 hisoblagichlari uchun odatiy port sozlamalari:

```
Baud Rate: 9600 bps
Data Bits: 8
Parity: None (N)
Stop Bits: 1
Flow Control: None
```

## Xatolarni Boshqarish

```csharp
try
{
    string value = await reader.ReadDataAsync(meterId, register);
}
catch (TimeoutException ex)
{
    Console.WriteLine("Hisoblagich javob bermadi");
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Noto'g'ri ma'lumot yoki checksum xatosi: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Meter ID mos kelmadi: {ex.Message}");
}
```

## Litsenziya

MIT License

## Qo'shimcha Ma'lumot

TE73 hisoblagichlari haqida qo'shimcha ma'lumot uchun ishlab chiqaruvchi dokumentatsiyasiga murojaat qiling.
