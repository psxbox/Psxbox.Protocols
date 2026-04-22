# O'zgarishlar Tarixi

## [2.1.0] - 2026-04-22

### ✨ Qo'shildi

- `StreamReceiveExtensions` kengaytmasi qo'shildi — `ReceiveAsync` metodi orqali oqimdan ma'lumotlarni asenkron olish uchun

### 🔄 O'zgartirildi

- `CloseAsync` metodidagi uzilish logikasi soddalashtirildi
- `Microsoft.Extensions.Logging.Abstractions` paketi `10.0.7` versiyasiga yangilandi (CE30XProtocol, RocProtocol)
- Gurux paketlari yangilandi: `Gurux.DLMS`, `Gurux.Net` (GuruxDLMS)

### 🐛 Tuzatildi

- `WaitTime` ni 1000 ga ko'paytirish xatoligi bartaraf etildi; xato xabarlari yaxshilandi

### 🔄 O'zgartirildi (kod sifati)

- `ReaderCE301.cs`, `ReaderCE308.cs`, `ReaderCE308CAS.cs`, `GXDLMSReader.cs`, `ModbusRTUWrapper.cs` fayllaridan ishlatilmagan `using` direktivalari olib tashlandi

---
