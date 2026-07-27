# O'zgarishlar Tarixi

## [2.2.0] - 2026-07-27

### ✨ Qo'shildi

- `ReaderCE208` to'liq qo'llab-quvvatlanishi: o'lchov va joriy energiya, arxiv sanalari (DATED/DATEM/DATEP), yuklama profili (GRAPE/VPR25), davr oxiri (ENDPE/ENMPE), quvvat holatlari (LOG01-03), rele boshqaruvi (RCTL1)
- `CE208Function` enum qo'shildi
- `IReader` interfeysiga rele boshqaruv metodlari qo'shildi (`SwitchRelayOn`, `SwitchRelayOff`)
- `BaseReader` ga virtual `SendWrite` metodi qo'shildi
- `CommonIEC61107.SendWrite` — W1 yozish oqimi (ACK/ERR qayta ishlash)
- `BaseReader.GetRecordDateTime` — yozuv vaqtini hisoblash metodi
- `IecQueryException` sinfi — IEC so'rov xatoliklari uchun
- CE30X arxivlarini kesh bilan qo'llab-quvvatlash

### 🐛 Tuzatildi

- `SendAndGet` metodlari DEFAULT_END o'rniga ETX ishlatadigan qilindi
- `BaseReader` — ulanishdan oldin uzish qo'shildi (konfliktlarni oldini olish)
- `CommonIEC61107` — yozishdan oldin kechikish oshirildi
- `ReaderCE308CAS` — yuklama profili indeksini hisoblash va vaqtni qayta ishlash to'g'rilandi
- `ReaderCE208` — `LoadProfilePeriodInMinutes`, `LoadProfileCountPerRequest` va `GetLoadProfileFunctions` metodlari to'g'rilandi
- `ReaderCE208` — real qurilma testlarida topilgan xatolar bartaraf etildi
- `ReaderCE208` — `ERRxx` javoblari bare ETX bilan tugash muammosi hal qilindi
- `ReaderCE208` — `GetPowerS/GetPowerR` ERRxx javobini tozalab ko'rsatish
- `RocMaster.GetParameters` — "hex" qo'riqchisi "ascii" bilan birga qo'shildi

### 🚀 Optimallashtirildi

- `RocMaster.GetParameters` va `RocProtocol.Requests` optimallashtirildi

### 🔄 O'zgartirildi

- `Microsoft.Extensions.Logging.Abstractions` paketi `10.0.8` va `10.0.9` versiyalariga yangilandi (CE30XProtocol, RocProtocol)
- `IReader.GetLoadProfiles` metodi imzosi yaxshilandi

---

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
