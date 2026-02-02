# Psxbox.Protocols

**Psxbox.Protocols** - Sanoat protokollari kutubxonalari to'plami.

## Loyihalar

### 1. Psxbox.Modbus
Modbus RTU, ASCII va TCP protokollari implementatsiyasi.

### 2. Psxbox.Mercury
Mercury elektr hisoblagichlari protokoli.

### 3. Psxbox.RocProtocol
Fisher ROC (Remote Operations Controller) protokoli - neft va gaz sanoati.

### 4. Psxbox.CE30XProtocol
Energomera CE30X seriyali hisoblagichlari (IEC 61107) protokoli.

### 5. Psxbox.GuruxDLMS
DLMS/COSEM (IEC 62056) smart meters protokoli.

### 6. Psxbox.CustomTE73Protocol
TE73 elektr hisoblagichlari maxsus protokoli.

## O'rnatish

```bash
# Barcha protokollarni qo'shish
dotnet add reference path/to/Psxbox.Protocols/Psxbox.Modbus/Psxbox.Modbus.csproj
dotnet add reference path/to/Psxbox.Protocols/Psxbox.Mercury/Psxbox.Mercury.csproj
# ...
```

## Bog'liqliklar

Barcha protokollar `Psxbox.Streams` transport kutubxonasidan foydalanadi.

## MyGateway Loyihasida Foydalanish

Ushbu protokollar MyGateway IoT Gateway tizimida turli sanoat qurilmalari bilan aloqa qilish uchun ishlatiladi:

- ? **Elektr hisoblagichlari** - Mercury, CE30X, TE73, DLMS
- ?? **PLC va RTU** - Modbus
- ??? **Neft va gaz** - ROC Protocol

## Litsenziya

MIT License
