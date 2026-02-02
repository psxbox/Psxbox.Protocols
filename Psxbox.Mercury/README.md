# Psxbox.Mercury

**Psxbox.Mercury** - Mercury 230, 231, 234 va boshqa Mercury elektr hisoblagichlari bilan ishlash uchun protokol kutubxonasi.

## Xususiyatlari

- ? **Real-time ma'lumotlar** - Joriy quvvat, kuchlanish, tok, chastota
- ?? **Quvvat ko'rsatkichlari** - Aktiv (P), Reaktiv (Q), To'liq (S) quvvat
- ?? **Arxiv ma'lumotlari** - Kunlik, oylik energiya iste'moli
- ?? **Parollar bilan ishlash** - Level 1/2 autentifikatsiya
- ?? **Vaqt sinxronizatsiyasi** - Hisoblagich soatini o'qish
- ?? **Tarif rejimlari** - Bir nechta tarif zonalari qo'llab-quvvatlanadi
- ? **Async/Await** - To'liq asinxron operatsiyalar

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.Mercury/Psxbox.Mercury.csproj
```

## Bog'liqliklar

- `Psxbox.Streams` - Transport qatlami (Serial/TCP)

## Qo'llab-quvvatlanuvchi Qurilmalar

- Mercury 230 (1-fazali)
- Mercury 231 (3-fazali)
- Mercury 234 (3-fazali, arxiv bilan)
- Mercury 236
- Mercury 203.2T

## Foydalanish

### Ulanish va Sessiyani Ochish

```csharp
using Psxbox.Mercury;
using Psxbox.Streams;

// Serial port orqali ulanish
var serialStream = new SerialStream("COM3,9600,8,N,1");
await serialStream.ConnectAsync();

var mercury = new Mercury230Reader(serialStream);

// Parolsiz (1-level)
bool opened = await mercury.Open(
    address: 123,           // Hisoblagich manzili
    level: 1,               // Access level
    password: ""            // Bo'sh parol
);

if (!opened)
{
    Console.WriteLine("Hisoblagichga ulanib bo'lmadi!");
    return;
}
```

### Parol bilan Ulanish (Level 2)

```csharp
// Level 2 parol bilan ulanish
bool opened = await mercury.Open(
    address: 123,
    level: 2,
    password: "123456",     // Parol
    passwordIsHex: false    // Oddiy string parol
);

// Hex formatdagi parol
bool openedHex = await mercury.Open(
    address: 123,
    level: 2,
    password: "111111",
    passwordIsHex: true
);
```

### Joriy Parametrlarni O'qish

```csharp
// Kuchlanish (Voltaj)
var (a, b, c) = await mercury.GetVoltages(address: 123);
Console.WriteLine($"Kuchlanish: A={a}V, B={b}V, C={c}V");

// Tok kuchi (Amper)
var (ia, ib, ic) = await mercury.GetCurrents(address: 123);
Console.WriteLine($"Tok: A={ia}A, B={ib}A, C={ic}A");

// Chastota
float frequency = await mercury.GetFrequency(address: 123);
Console.WriteLine($"Chastota: {frequency} Hz");

// Fazalar orasidagi burchaklar
var (ab, ac, bc) = await mercury.GetAngleOfUU(address: 123);
Console.WriteLine($"Burchaklar: AB={ab}°, AC={ac}°, BC={bc}°");
```

### Quvvat Ma'lumotlari

```csharp
// Aktiv quvvat (kW)
var (sumP, aP, bP, cP) = await mercury.GetPowerP(address: 123);
Console.WriteLine($"Aktiv quvvat: Jami={sumP}kW, A={aP}kW, B={bP}kW, C={cP}kW");

// Reaktiv quvvat (kVAr)
var (sumQ, aQ, bQ, cQ) = await mercury.GetPowerQ(address: 123);
Console.WriteLine($"Reaktiv quvvat: Jami={sumQ}kVAr");

// To'liq quvvat (kVA)
var (sumS, aS, bS, cS) = await mercury.GetPowerS(address: 123);
Console.WriteLine($"To'liq quvvat: Jami={sumS}kVA");

// Quvvat faktori (cos ?)
var (avgPF, aPF, bPF, cPF) = await mercury.GetPowerFactor(address: 123);
Console.WriteLine($"Quvvat faktori: O'rtacha={avgPF}");
```

### Energiya Iste'molini O'qish

```csharp
// Oxirgi energiya ko'rsatkichlari
await foreach (var energy in mercury.GetLastEnergy(address: 123))
{
    var (tarif, a1, a2, r1, r2) = energy;
    Console.WriteLine($"Tarif {tarif}: A+={a1}kWh, A-={a2}kWh, R+={r1}kVArh, R-={r2}kVArh");
}
```

### Arxiv Ma'lumotlarini O'qish

```csharp
// Kunlik arxivni o'qish
var fromDate = new DateOnly(2024, 1, 1);
var toDate = new DateOnly(2024, 1, 31);

await foreach (var record in mercury.GetArchive(
    address: 123,
    archiveType: ArchiveType.Daily,
    from: fromDate,
    to: toDate))
{
    var (date, tarif, v1, v2, v3, v4) = record;
    Console.WriteLine($"{date}: Tarif={tarif}, A+={v1}kWh, A-={v2}kWh, R+={v3}kVArh, R-={v4}kVArh");
}

// Oylik arxivni o'qish
await foreach (var record in mercury.GetArchive(
    address: 123,
    archiveType: ArchiveType.Monthly,
    from: new DateOnly(2024, 1, 1),
    to: new DateOnly(2024, 12, 31)))
{
    // Oylik ma'lumotlarni qayta ishlash
}
```

### Hisoblagich Soatini O'qish

```csharp
// Soatni o'qish
await mercury.ReadWatch(address: 123);
Console.WriteLine("Hisoblagich soati o'qildi");
```

### Sessiyani Yopish

```csharp
// Sessiyani to'g'ri yopish
await mercury.Close(address: 123);
await serialStream.CloseAsync();
```

## IReader Interfeysi

```csharp
public interface IReader
{
    Task<bool> Open(byte address, byte level, string password, bool passwordIsHex = false);
    Task Close(byte address);
    Task<(float ab, float ac, float bc)> GetAngleOfUU(byte address);
    Task<(float a, float b, float c)> GetVoltages(byte address);
    Task<(float a, float b, float c)> GetCurrents(byte address);
    Task<float> GetFrequency(byte address);
    Task<(float sum, float a, float b, float c)> GetPowerP(byte address);
    Task<(float sum, float a, float b, float c)> GetPowerQ(byte address);
    Task<(float sum, float a, float b, float c)> GetPowerS(byte address);
    Task<(float avg, float a, float b, float c)> GetPowerFactor(byte address);
    IAsyncEnumerable<(DateOnly date, byte tarif, float v1, float v2, float v3, float v4)> 
        GetArchive(byte address, ArchiveType archiveType, DateOnly from, DateOnly to);
    Task ReadWatch(byte address);
    IAsyncEnumerable<(byte tarif, float a1, float a2, float r1, float r2)> 
        GetLastEnergy(byte address);
}
```

## Arxiv Turlari

```csharp
public enum ArchiveType
{
    Hourly = 0,     // Soatlik
    Daily = 1,      // Kunlik
    Monthly = 2,    // Oylik
    None = 3        // Arxiv yo'q
}
```

## Quvvat Turlari

```csharp
public enum PowerType
{
    ActiveImport = 0,    // A+ (Aktiv, import)
    ActiveExport = 1,    // A- (Aktiv, export)
    ReactiveImport = 2,  // R+ (Reaktiv, import)
    ReactiveExport = 3   // R- (Reaktiv, export)
}
```

## MyGateway Loyihasida Foydalanish

MyGateway tizimida Mercury hisoblagichlar quyidagi maqsadlarda ishlatiladi:

- ? **Energiya monitoringi** - Real-time quvvat va energiya o'lchash
- ?? **Billing** - Tarif zonalari bo'yicha iste'mol hisobi
- ?? **Arxiv** - Tarixiy ma'lumotlarni saqlash va tahlil qilish
- ?? **Binolar** - Ko'p tarmoqli binolarda energiya boshqaruvi
- ?? **Sanoat** - Zavod va fabrikalarda elektr energiya hisobi

### Device Template Example

```json
{
  "protocol": "mercury",
  "transport": "serial",
  "port": "COM3",
  "baudRate": 9600,
  "address": 123,
  "password": "111111",
  "level": 1,
  "cron": "0 */5 * * * ?",
  "telemetry": [
    {"name": "voltage_a", "method": "GetVoltages", "phase": "a"},
    {"name": "current_a", "method": "GetCurrents", "phase": "a"},
    {"name": "power_p", "method": "GetPowerP", "phase": "sum"},
    {"name": "frequency", "method": "GetFrequency"}
  ]
}
```

## Xatolarni Boshqarish

```csharp
try
{
    var voltages = await mercury.GetVoltages(123);
}
catch (TimeoutException ex)
{
    Console.WriteLine("Hisoblagich javob bermadi");
}
catch (Exception ex)
{
    Console.WriteLine($"Xato: {ex.Message}");
}
finally
{
    await mercury.Close(123);
}
```

## Litsenziya

MIT License
