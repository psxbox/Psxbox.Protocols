namespace Psxbox.CE30XProtocol;

/// <summary>
/// CE308 yuklama profili yozuvi status belgisi (uStatRec_TypeDef, HEX).
/// Rasmiy МЭК protokol tavsifi bo'yicha bitlar (STAT_REC_ARC):
/// </summary>
[Flags]
public enum ProfileStatusFlags : byte
{
    None = 0,

    /// <summary>bit0 (NVR) — bo'sh yozuv emas.</summary>
    NotVoid = 0x01,

    /// <summary>bit1 (DST) — yozgi vaqt belgisi.</summary>
    DaylightSaving = 0x02,

    /// <summary>bit2 (PDN) — quvvat uzilishi.</summary>
    PowerDown = 0x04,

    /// <summary>bit3 (CAD) — vaqt o'zgartirilgan (soatga yozish).</summary>
    ClockAdjusted = 0x08,

    /// <summary>bit4 (ODA) — bir nechta to'planish (2 va undan ortiq o'tish).</summary>
    MultipleAccumulations = 0x10,

    /// <summary>bit5 (CDA) — arxiv tozalangan.</summary>
    ArchiveCleared = 0x20,

    /// <summary>bit6 (CIV) — soat nosozligi.</summary>
    ClockFailure = 0x40,

    /// <summary>bit7 (ERR) — yozuv ma'lumotlari buzilgan.</summary>
    CorruptRecord = 0x80,
}
