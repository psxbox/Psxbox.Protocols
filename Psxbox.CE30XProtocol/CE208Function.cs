namespace Psxbox.CE30XProtocol;

public enum CE208Function
{
    WATCH, // soat va sana
    FREQU, // chastota, Hz
    CURRE, // tok kuchi, A
    VOLTA, // kuchlanish, V
    POWEP, // aktiv quvvat, kW
    POWEQ, // reaktiv quvvat, kVar
    POWES, // to'liq quvvat, kVA
    ET0PE, // yig'ilgan aktiv energiya (import)
    ET0QI, // yig'ilgan reaktiv energiya (import)
    ET0QE, // yig'ilgan reaktiv energiya (eksport)
    ENDPE, // kun oxiridagi aktiv energiya, sana bo'yicha
    ENMPE, // oy oxiridagi aktiv energiya, oy bo'yicha
    DATED, // kunlik arxiv sanalari ro'yxati (128 tagacha)
    DATEM, // oylik arxiv sanalari ro'yxati (36 tagacha)
    DATEP, // yuklama profili yozuvi bor sanalar
    GRAPE, // aktiv quvvat yuklama profili, sana bo'yicha
    VPR25, // kuchlanish RMS profili, sana bo'yicha
    TAVER, // profil o'rtalash intervali, daqiqa
    STAT_, // holat so'zi (bit 15 - rele holati)
    RCTL1, // rele boshqaruvi (W1): 1 - yoqish, 0 - o'chirish
    REL_1, // rele konfiguratsiyasi (bit 3 - interfeys orqali boshqarish)
    LOG01, // kuchlanish holati jurnali
    LOG02, // dasturlash jurnali
    LOG03, // pitaniye jurnali
    EADPE, // kunlik energiya
    EAMPE, // oylik energiya

}
