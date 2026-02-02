# Psxbox.Modbus

**Psxbox.Modbus** - Modbus RTU, ASCII va TCP protokollarini qo'llab-quvvatlovchi to'liq funksional Modbus Master kutubxonasi.

## Xususiyatlari

- ?? **Modbus RTU** - Serial port (RS-232/RS-485) orqali RTU protokoli
- ?? **Modbus ASCII** - Serial port orqali ASCII protokoli  
- ?? **Modbus TCP** - Ethernet orqali TCP/IP protokoli
- ? **CRC/LRC tekshiruvi** - avtomatik checksum hisoblash va validatsiya
- ?? **Barcha funksiyalar** - FC01-FC06, FC15, FC16 qo'llab-quvvatlanadi
- ? **Async/Await** - to'liq asinxron operatsiyalar
- ?? **Transport agnostik** - `IStream` interfeysi orqali ishlaydi

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.Modbus/Psxbox.Modbus.csproj
```

## Bog'liqliklar

- `Psxbox.Streams` - transport qatlami (TCP/Serial)

## Foydalanish

### Modbus TCP

```csharp
using Psxbox.Modbus;
using Psxbox.Streams;

// TCP ulanishni yaratish
var tcpStream = new TcpStream("192.168.1.100:502");
var modbus = new ModbusTCPWrapper();

await tcpStream.ConnectAsync();

// Holding Registers o'qish (FC03)
var (request, responseSize, transactionId) = 
    modbus.BuildReadHoldingRegistersRequest(
        slaveId: 1,
        startAddress: 0,
        count: 10
    );

await tcpStream.WriteAsync(request);
byte[] response = await modbus.ReadResponse(tcpStream, 1, 0x03, responseSize, transactionId);

// Registrlarni parse qilish
ushort[] registers = ModbusHelpers.ParseRegisters(response);
```

### Modbus RTU (Serial)

```csharp
// Serial port ulanishni yaratish
var serialStream = new SerialStream("COM3,9600,8,N,1");
var modbus = new ModbusRTUWrapper();

await serialStream.ConnectAsync();

// Input Registers o'qish (FC04)
var (request, responseSize, _) = 
    modbus.BuildReadInputRegistersRequest(
        slaveId: 1,
        startAddress: 100,
        count: 5
    );

await serialStream.WriteAsync(request);
byte[] response = await modbus.ReadResponse(serialStream, 1, 0x04, responseSize, 0);

ushort[] inputRegisters = ModbusHelpers.ParseRegisters(response);
```

### Modbus ASCII

```csharp
var serialStream = new SerialStream("COM1,9600,7,E,1");
var modbus = new ModbusASCIIWrapper();

await serialStream.ConnectAsync();

// Coil yozish (FC05)
var (request, responseSize, _) = 
    modbus.BuildWriteSingleCoilRequest(
        slaveId: 1,
        address: 50,
        value: true
    );

await serialStream.WriteAsync(request);
byte[] response = await modbus.ReadResponse(serialStream, 1, 0x05, responseSize, 0);
```

## Qo'llab-quvvatlanuvchi Funksiyalar

### O'qish Funksiyalari

| FC | Funksiya | Tavsif |
|----|----------|--------|
| 01 | Read Coils | Diskret chiqishlarni o'qish (DO) |
| 02 | Read Discrete Inputs | Diskret kirishlarni o'qish (DI) |
| 03 | Read Holding Registers | Holding registrlarni o'qish (RW) |
| 04 | Read Input Registers | Input registrlarni o'qish (RO) |

### Yozish Funksiyalari

| FC | Funksiya | Tavsif |
|----|----------|--------|
| 05 | Write Single Coil | Bitta coil yozish |
| 06 | Write Single Register | Bitta register yozish |
| 15 | Write Multiple Coils | Bir nechta coil yozish |
| 16 | Write Multiple Registers | Bir nechta register yozish |

## ModbusMaster - Yuqori Darajali API

```csharp
using Psxbox.Modbus;

var master = new ModbusMaster(stream, modbus);

// O'qish operatsiyalari
ushort[] holdingRegs = await master.ReadHoldingRegistersAsync(1, 0, 10);
ushort[] inputRegs = await master.ReadInputRegistersAsync(1, 100, 5);
bool[] coils = await master.ReadCoilsAsync(1, 0, 16);
bool[] inputs = await master.ReadDiscreteInputsAsync(1, 0, 8);

// Yozish operatsiyalari
await master.WriteSingleRegisterAsync(1, 40, 1234);
await master.WriteSingleCoilAsync(1, 50, true);
await master.WriteMultipleRegistersAsync(1, 0, new ushort[] { 100, 200, 300 });
await master.WriteMultipleCoilsAsync(1, 0, new bool[] { true, false, true, true });
```

## Ma'lumotlarni Konvertatsiya Qilish

```csharp
using Psxbox.Modbus;

// Registrlardan float o'qish (2 register)
float temperature = ModbusHelpers.GetFloat(registers, offset: 0, isBigEndian: true);

// Registrlardan int32 o'qish (2 register)
int value = ModbusHelpers.GetInt32(registers, offset: 2, isBigEndian: false);

// Float ni registrlarga yozish
ushort[] floatRegs = ModbusHelpers.SetFloat(25.5f, isBigEndian: true);

// String ni registrlarga konvertatsiya
ushort[] stringRegs = ModbusHelpers.StringToRegisters("HELLO");
```

## Byte Order (Endianness)

Modbus uchun to'rtta byte order mavjud:

```csharp
// Big Endian (ABCD) - standart Modbus
float value1 = ModbusHelpers.GetFloat(regs, 0, ByteOrder.BigEndian);

// Little Endian (DCBA)
float value2 = ModbusHelpers.GetFloat(regs, 0, ByteOrder.LittleEndian);

// Big Endian Byte Swap (BADC)
float value3 = ModbusHelpers.GetFloat(regs, 0, ByteOrder.BigEndianByteSwap);

// Little Endian Byte Swap (CDAB)
float value4 = ModbusHelpers.GetFloat(regs, 0, ByteOrder.LittleEndianByteSwap);
```

## MyGateway Loyihasida Foydalanish

MyGateway tizimida ushbu kutubxona quyidagi sanoat qurilmalari bilan aloqa qilish uchun ishlatiladi:

- ?? **PLC (Programmable Logic Controllers)** - Siemens, Allen-Bradley, Schneider
- ?? **RTU (Remote Terminal Units)** - Fisher ROC, ABB RTU
- ? **Smart Meters** - elektr/gaz/suv hisoblagichlari
- ??? **Sensor qurilmalari** - harorat, bosim, oqim o'lchagichlari

### Device Settings Example

```json
{
  "protocol": "modbus",
  "transport": "tcp",
  "address": "192.168.1.100:502",
  "slaveId": 1,
  "telemetry": [
    {
      "fc": 3,
      "start": 0,
      "count": 10,
      "tags": [
        {"name": "temperature", "dataType": "float", "byteOrder": "ABCD"}
      ]
    }
  ]
}
```

## Xatolarni Boshqarish

```csharp
try
{
    var data = await master.ReadHoldingRegistersAsync(1, 0, 10);
}
catch (ModbusException ex)
{
    Console.WriteLine($"Modbus xatosi: {ex.FunctionCode} - {ex.ExceptionCode}");
}
catch (TimeoutException ex)
{
    Console.WriteLine("Qurilma javob bermadi");
}
catch (Exception ex)
{
    Console.WriteLine($"Umumiy xato: {ex.Message}");
}
```

## Exception Codes

| Code | Nomi | Tavsif |
|------|------|--------|
| 01 | Illegal Function | Noto'g'ri funksiya kodi |
| 02 | Illegal Data Address | Noto'g'ri manzil |
| 03 | Illegal Data Value | Noto'g'ri qiymat |
| 04 | Slave Device Failure | Qurilma xatosi |

## Litsenziya

MIT License
