# Psxbox.GuruxDLMS

**Psxbox.GuruxDLMS** - DLMS/COSEM (Device Language Message Specification) protokoli orqali smart meters va energy management qurilmalari bilan ishlash uchun kutubxona. Gurux.DLMS SDK asosida qurilgan.

## Xususiyatlari

- ?? **DLMS/COSEM protokol** - IEC 62056 standartiga muvofiq
- ?? **COSEM obyektlari** - Register, Profile, Clock, Data obyektlari
- ?? **Xavfsizlik** - LLS (Low Level Security) va HLS (High Level Security)
- ?? **Load profile** - Profil ma'lumotlarini o'qish
- ?? **Clock sinxronizatsiya** - Qurilma soatini boshqarish
- ?? **Bir nechta transport** - HDLC, TCP, UDP qo'llab-quvvatlanadi
- ? **Async/Await** - To'liq asinxron operatsiyalar

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.GuruxDLMS/Psxbox.GuruxDLMS.csproj
```

## Bog'liqliklar

- `Gurux.Common` (v8.4.2503.602) - Umumiy Gurux funksiyalari
- `Gurux.DLMS` (v9.0.2508.2201) - DLMS protokol implementatsiyasi
- `Gurux.Net` (v8.4.2503.1001) - TCP/UDP transport
- `Gurux.Serial` (v8.4.2503.603) - Serial port transport
- `Psxbox.Streams` - Stream integratsiyasi

## Qo'llab-quvvatlanuvchi Qurilmalar

- Smart Meters (Elektr hisoblagichlari)
  - Landis+Gyr E350, E450, E650
  - Itron ACE6000, SL7000
  - Elster AS1440, AS3000
  - Kamstrup Omnipower
- Gas Meters
- Water Meters
- Heat Meters
- Multi-utility meters

## Foydalanish

### DLMS Mijozni Yaratish

```csharp
using Gurux.DLMS;
using Gurux.Serial;
using Psxbox.GuruxDLMS;
using Microsoft.Extensions.Logging;

// DLMS mijoz sozlamalari
var client = new GXDLMSClient(
    useLogicalNameReferencing: true,
    clientAddress: 16,
    serverAddress: 1,
    authentication: Authentication.Low,
    password: "00000000",
    interfaceType: InterfaceType.HDLC
);

// Serial transport
var serial = new GXSerial();
serial.PortName = "COM3";
serial.BaudRate = 9600;
serial.DataBits = 8;
serial.Parity = System.IO.Ports.Parity.None;
serial.StopBits = System.IO.Ports.StopBits.One;

// DLMS Reader yaratish
var logger = loggerFactory.CreateLogger<DLMSReader>();
var reader = new DLMSReader(client, serial, logger, timeout: 5000);
```

### Psxbox.Streams bilan Integratsiya

```csharp
using Psxbox.Streams;
using Psxbox.GuruxDLMS;

// Serial stream yaratish
var stream = new SerialStream("COM3,9600,8,N,1");
await stream.ConnectAsync();

// GXStream adapter yaratish
var gxStream = new GXStream(stream);

// DLMS reader
var reader = new DLMSReader(client, gxStream, logger);
```

### Ulanish va Autentifikatsiya

```csharp
// Ulanish o'rnatish
await reader.InitializeConnection();

Console.WriteLine("DLMS qurilmaga muvaffaqiyatli ulandi!");
```

### COSEM Obyektlarini O'qish

```csharp
// Clock obyektini o'qish (0.0.1.0.0.255)
var clockLN = new GXDLMSClock("0.0.1.0.0.255");
var time = await reader.Read(clockLN, 2);  // Attribute 2 - Time
Console.WriteLine($"Qurilma vaqti: {time}");

// Register obyektini o'qish (1.0.1.8.0.255)
var registerLN = new GXDLMSRegister("1.0.1.8.0.255");
var value = await reader.Read(registerLN, 2);  // Attribute 2 - Value
var scaler = await reader.Read(registerLN, 3); // Attribute 3 - Scaler
Console.WriteLine($"Energiya: {value} x 10^{scaler}");
```

### Aktiv Energiya O'qish

```csharp
// Aktiv energiya (A+) - 1.0.1.8.0.255
var activeEnergyImport = new GXDLMSRegister("1.0.1.8.0.255");
var energy = await reader.Read(activeEnergyImport, 2);
var unit = await reader.Read(activeEnergyImport, 3);

Console.WriteLine($"Aktiv energiya import: {energy} kWh");

// Aktiv energiya eksport (A-) - 1.0.2.8.0.255
var activeEnergyExport = new GXDLMSRegister("1.0.2.8.0.255");
```

### Reaktiv Energiya O'qish

```csharp
// Reaktiv energiya (R+) - 1.0.3.8.0.255
var reactiveEnergyImport = new GXDLMSRegister("1.0.3.8.0.255");

// Reaktiv energiya eksport (R-) - 1.0.4.8.0.255
var reactiveEnergyExport = new GXDLMSRegister("1.0.4.8.0.255");
```

### Joriy Qiymatlarni O'qish

```csharp
// Kuchlanish L1 - 1.0.32.7.0.255
var voltageL1 = new GXDLMSRegister("1.0.32.7.0.255");
var v1 = await reader.Read(voltageL1, 2);
Console.WriteLine($"Kuchlanish L1: {v1}V");

// Tok L1 - 1.0.31.7.0.255
var currentL1 = new GXDLMSRegister("1.0.31.7.0.255");
var i1 = await reader.Read(currentL1, 2);
Console.WriteLine($"Tok L1: {i1}A");

// Aktiv quvvat - 1.0.1.7.0.255
var activePower = new GXDLMSRegister("1.0.1.7.0.255");
var p = await reader.Read(activePower, 2);
Console.WriteLine($"Aktiv quvvat: {p}W");

// Quvvat faktori - 1.0.13.7.0.255
var powerFactor = new GXDLMSRegister("1.0.13.7.0.255");
var pf = await reader.Read(powerFactor, 2);
Console.WriteLine($"Quvvat faktori: {pf}");
```

### Load Profile O'qish

```csharp
// Load profile obyekti (1.0.99.1.0.255)
var profileGeneric = new GXDLMSProfileGeneric("1.0.99.1.0.255");

// Profil ma'lumotlarini o'qish
var buffer = await reader.Read(profileGeneric, 2);  // Buffer

// Profil davri
var capturePeriod = await reader.Read(profileGeneric, 4);
Console.WriteLine($"Capture period: {capturePeriod} seconds");

// Qo'llab-quvvatlanadigan ustunlar
var captureObjects = await reader.Read(profileGeneric, 3);
```

### Yozish Operatsiyalari

```csharp
// Clock ni o'rnatish
var clock = new GXDLMSClock("0.0.1.0.0.255");
await reader.Write(clock, 2, DateTime.Now);

// Parametrni yozish
var register = new GXDLMSRegister("0.0.42.0.0.255");
await reader.Write(register, 2, 12345);
```

### Method Chaqirish

```csharp
// Clock ni sinxronizatsiya qilish (method 1)
var clock = new GXDLMSClock("0.0.1.0.0.255");
await reader.Method(clock, 1, DateTime.Now, DataType.DateTime);

// Qurilmani qayta yuklash
var associationLN = new GXDLMSAssociationLogicalName("0.0.40.0.0.255");
await reader.Method(associationLN, 1, null, DataType.None);
```

### Ulanishni Yopish

```csharp
// Sessiyani to'g'ri yopish
await reader.Close();
serial.Close();
```

## Asosiy OBIS Kodlar

| OBIS Code | Tavsif |
|-----------|--------|
| 0.0.1.0.0.255 | Clock (Soat) |
| 1.0.1.8.0.255 | Aktiv energiya import (A+) |
| 1.0.2.8.0.255 | Aktiv energiya eksport (A-) |
| 1.0.3.8.0.255 | Reaktiv energiya import (R+) |
| 1.0.4.8.0.255 | Reaktiv energiya eksport (R-) |
| 1.0.1.7.0.255 | Aktiv quvvat |
| 1.0.3.7.0.255 | Reaktiv quvvat |
| 1.0.9.7.0.255 | To'liq quvvat |
| 1.0.32.7.0.255 | Kuchlanish L1 |
| 1.0.52.7.0.255 | Kuchlanish L2 |
| 1.0.72.7.0.255 | Kuchlanish L3 |
| 1.0.31.7.0.255 | Tok L1 |
| 1.0.51.7.0.255 | Tok L2 |
| 1.0.71.7.0.255 | Tok L3 |
| 1.0.13.7.0.255 | Quvvat faktori |
| 1.0.14.7.0.255 | Chastota |
| 1.0.99.1.0.255 | Load Profile |

## MyGateway Loyihasida Foydalanish

MyGateway.Worker.DLMS tizimida DLMS protokoli quyidagi maqsadlar uchun ishlatiladi:

- ? **Smart Metering** - Aqlli elektr hisoblagichlari
- ?? **AMI (Advanced Metering Infrastructure)** - Ilg'or o'lchash infratuzilmasi
- ??? **Smart City** - Aqlli shahar loyihalari
- ?? **Energy Management** - Energiya boshqaruvi tizimlari
- ?? **Multi-utility** - Elektr, gaz, suv hisoblagichlari

### Device Template Example

```json
{
  "protocol": "dlms",
  "transport": "serial",
  "port": "COM3",
  "baudRate": 9600,
  "clientAddress": 16,
  "serverAddress": 1,
  "authentication": "Low",
  "password": "00000000",
  "interfaceType": "HDLC",
  "cron": "0 */5 * * * ?",
  "telemetry": [
    {
      "name": "active_energy_import",
      "obis": "1.0.1.8.0.255",
      "classId": 3,
      "attribute": 2
    },
    {
      "name": "voltage_l1",
      "obis": "1.0.32.7.0.255",
      "classId": 3,
      "attribute": 2
    },
    {
      "name": "current_l1",
      "obis": "1.0.31.7.0.255",
      "classId": 3,
      "attribute": 2
    }
  ]
}
```

## Authentication Turlari

```csharp
public enum Authentication
{
    None = 0,           // Autentifikatsiya yo'q
    Low = 1,            // LLS (password)
    High = 2,           // HLS (challenge-response)
    HighMD5 = 3,        // HLS MD5
    HighSHA1 = 4,       // HLS SHA1
    HighGMAC = 5,       // HLS GMAC
    HighSHA256 = 6,     // HLS SHA256
    HighECDSA = 7       // HLS ECDSA
}
```

## Xatolarni Boshqarish

```csharp
try
{
    await reader.InitializeConnection();
    var data = await reader.Read(register, 2);
}
catch (GXDLMSException ex)
{
    logger.LogError($"DLMS xatosi: {ex.ErrorCode} - {ex.Message}");
}
catch (TimeoutException ex)
{
    logger.LogError("Qurilma javob bermadi");
}
finally
{
    await reader.Close();
}
```

## Litsenziya

MIT License

## Qo'shimcha Ma'lumot

- DLMS/COSEM standart: IEC 62056
- Gurux dokumentatsiya: https://www.gurux.fi/
