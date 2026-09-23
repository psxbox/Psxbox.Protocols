namespace Psxbox.CE30XProtocol
{
    public enum CE308Function
    {
        LST01, // kun oxiri arxivi sanalari
        LST02, // oy oxiri arxivi sanalari
        LST03, // yil oxiri arxivi sanalari
        LST04, // kunlik profil arxivi sanalari (LST04[0] = joriy sutka)
        EMD01, // kun oxiri A+
        EMD02, // kun oxiri A-
        EMD03, // kun oxiri R+
        EMD04, // kun oxiri R-
        EMM01, // oy oxiri A+
        EMM02, // oy oxiri A-
        EMM03, // oy oxiri R+
        EMM04, // oy oxiri A-
        EMY01, // yil oxiri A+
        EMY02, // yil oxiri A-
        EMY03, // yil oxiri R+
        EMY04, // yil oxiri R-
        WATCH, // soatni so'rash
        FREQU, // frequency
        CURRE, // tok kuchi, A
        VOLTA, // kuchlanisg, V
        POWES, // KVA
        POWEP, // KWt
        POWEQ, // KVar
        CORIU, // I va U orasidagi burchak
        CORUU, // 2 faza orasidagi burchak
        VPR01, // 1 profil nagruzka
        VPR02, // 2 profil nagruzka
        VPR03, // 3 profil nagruzka
        VPR04, // 4 profil nagruzka
        LNE04, // LOG_PowerSupply — Silovoe pitanie: 0=vkl / 1=vykl
        LNE05, // LOG_FaultSupply — Polnoe propadanie pitaniya
        LNE22, // LOG_StatBattery — Sostoyaniye litevogo elementa: 0=OK, 1=plohoe, 2=otsutstvuyet
        PROFI, // profil o'rtalash intervali (hex) + 6 ta profil identifikatori
    }
}
