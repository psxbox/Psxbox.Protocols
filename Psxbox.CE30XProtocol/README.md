# Psxbox.CE30XProtocol

**Psxbox.CE30XProtocol** - Energomera CE30X seriyali elektr hisoblagichlari bilan IEC 61107 protokoli orqali ishlash uchun kutubxona.

## Xususiyatlari

- ? **IEC 61107** - Xalqaro elektr hisoblagichlari protokoli
- ?? **Bir nechta model** - CE301, CE303, CE308, CE102M, CE6850 qo'llab-quvvatlanadi
- ?? **Autentifikatsiya** - Parollar bilan kirish huquqi
- ?? **Real-time ma'lumotlar** - Joriy quvvat, kuchlanish, tok, chastota
- ?? **Energiya profillari** - Soatlik va kunlik profil ma'lumotlari
- ?? **Vaqt sinxronizatsiyasi** - Hisoblagich soatini o'rnatish va o'qish
- ? **Async/Await** - To'liq asinxron operatsiyalar

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.CE308Protocol/Psxbox.CE30XProtocol.csproj
```

## Bog'liqliklar

- `Psxbox.Utils` - Yordamchi funksiyalar
- `Psxbox.Streams` - Transport qatlami
- `Microsoft.Extensions.Logging.Abstractions` (v10.0.0) - Logging

## Qo'llab-quvvatlanuvchi Modellar

- **CE301** - 1-fazali elektr hisoblagich
- **CE303** - 3-fazali elektr hisoblagich
- **CE308** - 3-fazali, ko'p tarifli hisoblagich
- **CE102M** - Zamonaviy 1-fazali hisoblagich
- **CE6850** - Sanoat hisoblagichi
- **CE6850M** - Zamonaviy sanoat hisoblagichi

## Foydalanish

### CE308 Hisoblagich

```csharp
using Psxbox.CE30XProtocol;
using Psxbox.Streams;

// Serial port orqali ulanish
var stream = new SerialStream("COM3,9600,7,E,1");
await stream.ConnectAsync();

var logger = loggerFactory.CreateLogger<ReaderCE308>();
var reader = new ReaderCE308(stream, logger);

// Bog'lanish
bool connected = await reader.Connect();
if (!connected)
{
    Console.WriteLine("Hisoblagichga ulanib bo'lmadi!");
    return;
}

Console.WriteLine($"Hisoblagich ID: {reader.ID}");
```

### Joriy Parametrlarni O'qish

```csharp
// Kuchlanish (Voltaj)
var (voltA, voltB, voltC) = await reader.GetVoltage();
Console.WriteLine($"Kuchlanish: A={voltA}V, B={voltB}V, C={voltC}V");

// Tok kuchi (Amper)
var (currA, currB, currC) = await reader.GetCurrent();
Console.WriteLine($"Tok: A={currA}A, B={currB}A, C={currC}A");

// Chastota
double frequency = await reader.GetFrequency();
Console.WriteLine($"Chastota: {frequency} Hz");

// Hisoblagich soati
DateTimeOffset watch = await reader.GetWatch();
Console.WriteLine($"Hisoblagich vaqti: {watch}");
```

### Quvvat Ma'lumotlari

```csharp
// Aktiv quvvat (kW)
var (pA, pB, pC, pSum) = await reader.GetPowerP();
Console.WriteLine($"Aktiv quvvat: Jami={pSum}kW");

// Reaktiv quvvat (kVAr)
var (qA, qB, qC, qSum) = await reader.GetPowerQ();
Console.WriteLine($"Reaktiv quvvat: Jami={qSum}kVAr");

// To'liq quvvat (kVA)
var (sA, sB, sC, sSum) = await reader.GetPowerS();
Console.WriteLine($"To'liq quvvat: Jami={sSum}kVA");

// Quvvat faktori (cos ?)
var (pfA, pfB, pfC, pfAvg) = await reader.GetPowerFactor();
Console.WriteLine($"Quvvat faktori: {pfAvg}");
```

### Energiya Iste'molini O'qish

```csharp
// Aktiv energiya (A+)
var (t1, t2, t3, t4, total) = await reader.GetEnergyAP();
Console.WriteLine($"Energiya A+: T1={t1}kWh, T2={t2}kWh, T3={t3}kWh, T4={t4}kWh, Jami={total}kWh");

// Reaktiv energiya (R+)
var energyRP = await reader.GetEnergyRP();
Console.WriteLine($"Energiya R+: {energyRP.total}kVArh");

// Boshqa tarmoqlar
var energyAM = await reader.GetEnergyAM();  // A- (eksport)
var energyRM = await reader.GetEnergyRM();  // R- (eksport)
```

### Maksimal Qiymatlar

```csharp
// Maksimal quvvat (demand)
var maxPower = await reader.GetMaxPower();
Console.WriteLine($"Maksimal quvvat: {maxPower.max}kW vaqtida {maxPower.timestamp}");

// Kun ichidagi maksimal quvvat
var maxPowerToday = await reader.GetMaxPowerToday();
```

### Profil Ma'lumotlarini O'qish

```csharp
// Soatlik profil
var profile = await reader.GetHourlyProfile(
    startDate: new DateTime(2024, 1, 1),
    endDate: new DateTime(2024, 1, 31)
);

foreach (var record in profile)
{
    Console.WriteLine($"{record.Timestamp}: {record.Energy}kWh, {record.Power}kW");
}

// Kunlik profil
var dailyProfile = await reader.GetDailyProfile(
    startDate: new DateTime(2024, 1, 1),
    endDate: new DateTime(2024, 12, 31)
);
```

### CE301 Hisoblagich (1-fazali)

```csharp
var reader301 = new ReaderCE301(stream, logger);

await reader301.Connect();

// 1-fazali parametrlar
double voltage = await reader301.GetVoltage();     // Bitta faza
double current = await reader301.GetCurrent();     // Bitta faza
double power = await reader301.GetPowerP();        // Bitta qiymat

var (t1, t2, total) = await reader301.GetEnergyAP();  // Ikki tarif
```

### CE303 Hisoblagich (3-fazali)

```csharp
var reader303 = new ReaderCE303(stream, logger);

await reader303.Connect();

// 3-fazali parametrlar
var voltages = await reader303.GetVoltages();
var currents = await reader303.GetCurrents();
var powers = await reader303.GetPowerP();
```

### CE102M Hisoblagich (Zamonaviy)

```csharp
var readerCE102M = new ReaderCE102M(stream, logger);

await readerCE102M.Connect();

// Kengaytirilgan funksiyalar
var events = await readerCE102M.GetEventLog();
var quality = await readerCE102M.GetPowerQuality();
var harmonics = await readerCE102M.GetHarmonics();
```

### CE6850 Sanoat Hisoblagichi

```csharp
var reader6850 = new ReaderCE6850(stream, logger);

await reader6850.Connect();

// Sanoat parametrlari
var multipoint = await reader6850.GetMultiPointData();
var loadProfile = await reader6850.GetLoadProfile();
```

## IReader Interfeysi

```csharp
public interface IReader : IDisposable
{
    string ID { get; }
    Task<bool> Connect();
    Task Disconnect();
    Task<DateTimeOffset> GetWatch();
    Task<double> GetFrequency();
    Task<(double a, double b, double c)> GetCurrent();
    Task<(double a, double b, double c)> GetVoltage();
    Task<(double a, double b, double c, double sum)> GetPowerS();
    Task<(double a, double b, double c, double sum)> GetPowerP();
    Task<(double a, double b, double c, double sum)> GetPowerQ();
    Task<(double a, double b, double c, double avg)> GetPowerFactor();
    Task<(double t1, double t2, double t3, double t4, double total)> GetEnergyAP();
    // ...
}
```

## IEC 61107 Protokol Xususiyatlari

### Ulanish Rejimi

```
Host ? Meter:  /?12345678!\r\n      (ID so'rovi)
Meter ? Host:  /EGM5CE308...\r\n    (Identifikatsiya)
Host ? Meter:  ACK 0 5 1\r\n        (Acknowledgment)
Meter ? Host:  DATA...              (Ma'lumotlar)
```

### Buyruqlar

```csharp
// Energiyani o'qish
string command = "R1\r\nET0PE()\r\n";  // Aktiv energiya

// Joriy quvvatni o'qish
string command = "R1\r\nPOWPP()\r\n";  // Aktiv quvvat

// Kuchlanishni o'qish
string command = "R1\r\nVOLTA()\r\n";  // Fazalar kuchlanishi
```

## MyGateway Loyihasida Foydalanish

MyGateway.Worker.CE30X tizimida Energomera hisoblagichlari quyidagi vazifalar uchun ishlatiladi:

- ? **Energiya o'lchash** - Elektr energiya iste'molini monitoring
- ?? **Binolar** - Ko'p kvartirali uylar va ofislar
- ?? **Sanoat** - Zavod va ishlab chiqarish korxonalari
- ?? **Billing** - Tarif zonalari bo'yicha hisob-kitob
- ?? **AMR** - Avtomatik hisoblagichlarni o'qish tizimi

### Device Template Example

```json
{
  "protocol": "ce30x",
  "model": "CE308",
  "transport": "serial",
  "port": "COM3",
  "baudRate": 9600,
  "dataBits": 7,
  "parity": "Even",
  "stopBits": 1,
  "address": "12345678",
  "password": "",
  "cron": "0 */15 * * * ?",
  "telemetry": [
    {"name": "voltage_a", "method": "GetVoltage", "phase": "a"},
    {"name": "current_a", "method": "GetCurrent", "phase": "a"},
    {"name": "power_p", "method": "GetPowerP", "phase": "sum"},
    {"name": "energy_ap", "method": "GetEnergyAP", "tariff": "total"}
  ]
}
```

## Serial Port Konfiguratsiyasi

CE30X hisoblagichlari uchun odatiy port sozlamalari:

```
Baud Rate: 9600 bps
Data Bits: 7
Parity: Even (E)
Stop Bits: 1
Flow Control: None
```

## Xatolarni Boshqarish

```csharp
try
{
    var data = await reader.GetPowerP();
}
catch (TimeoutException ex)
{
    logger.LogError("Hisoblagich javob bermadi");
}
catch (InvalidDataException ex)
{
    logger.LogError($"Noto'g'ri ma'lumot: {ex.Message}");
}
catch (Exception ex)
{
    logger.LogError($"Xato: {ex.Message}");
}
finally
{
    await reader.Disconnect();
}
```

## Litsenziya

MIT License
