namespace Psxbox.CE30XProtocol;

/// <summary>
/// CE303/CE301 "PHASE" (faza holati) jurnali status bayti — CPHAS dekoderi.
///
/// Bayt bitlari (rasmiy RE ИНЕС.411152.081, "CPHAS"):
///   0, 1, 2 — A, B, C fazalar holati (0 — o'chiq, 1 — yoniq);
///   3, 4, 5 — A, B, C fazada kuchlanishsiz tok borligi;
///   6       — hisoblagich yoniq (1) / o'chiq (0);
///   7       — barcha 3 faza borligida kuchlanish vektor burchaklari manfiy.
/// </summary>
public readonly record struct PhaseStatus(byte Value)
{
    /// <summary>Bit 6 — hisoblagich yoniq.</summary>
    public const byte MeterOnMask = 0x40;

    /// <summary>Bit 7 — kuchlanish vektor burchaklari manfiy.</summary>
    public const byte NegativeAnglesMask = 0x80;

    /// <summary>Bit 6 — hisoblagich yoniq / o'chiq.</summary>
    public bool MeterOn => (Value & MeterOnMask) != 0;

    /// <summary>Bit 0 — A faza yoniq.</summary>
    public bool PhaseA => (Value & 0x01) != 0;

    /// <summary>Bit 1 — B faza yoniq.</summary>
    public bool PhaseB => (Value & 0x02) != 0;

    /// <summary>Bit 2 — C faza yoniq.</summary>
    public bool PhaseC => (Value & 0x04) != 0;

    /// <summary>Bit 3 — A fazada kuchlanishsiz tok bor.</summary>
    public bool CurrentWithoutVoltageA => (Value & 0x08) != 0;

    /// <summary>Bit 4 — B fazada kuchlanishsiz tok bor.</summary>
    public bool CurrentWithoutVoltageB => (Value & 0x10) != 0;

    /// <summary>Bit 5 — C fazada kuchlanishsiz tok bor.</summary>
    public bool CurrentWithoutVoltageC => (Value & 0x20) != 0;

    /// <summary>Bit 7 — kuchlanish vektor burchaklari manfiy (barcha 3 faza borligida).</summary>
    public bool NegativeVoltageAngles => (Value & NegativeAnglesMask) != 0;

    /// <summary>Status baytini dekodlaydi (har qanday bayt to'g'ri dekodlanadi).</summary>
    public static PhaseStatus Decode(byte value) => new(value);

    /// <summary>Qisqa, bir qatorli tavsif (loglar uchun).</summary>
    public override string ToString()
    {
        var text = MeterOn ? "hisoblagich: yoniq; " : "hisoblagich: o'chiq; ";
        text += "fazalar: " + (PhaseA ? "A" : "-") + (PhaseB ? "B" : "-") + (PhaseC ? "C" : "-");

        if (CurrentWithoutVoltageA || CurrentWithoutVoltageB || CurrentWithoutVoltageC)
        {
            text += "; kuchlanishsiz tok: " +
                (CurrentWithoutVoltageA ? "A" : "") +
                (CurrentWithoutVoltageB ? "B" : "") +
                (CurrentWithoutVoltageC ? "C" : "");
        }
        if (NegativeVoltageAngles)
        {
            text += "; manfiy burchaklar";
        }
        return text;
    }

    /// <summary>
    /// AdminTools uslubidagi ko'p qatorli ruscha tavsif — AdminTools eksporti
    /// bilan solishtirish uchun (qatorlar "\n" bilan ajratiladi; harflar
    /// AdminTools dagidek: holat qatorlarida kirill А/В/С, tok qatorlarida
    /// А kirill + B/C lotin).
    /// </summary>
    public string ToAdminToolsText()
    {
        var lines = new List<string>
        {
            MeterOn ? "Счетчик включен" : "Счетчик выключен",
            $"Фаза А - {(PhaseA ? "вкл." : "выкл.")}",
            $"Фаза В - {(PhaseB ? "вкл." : "выкл.")}",
            $"Фаза С - {(PhaseC ? "вкл." : "выкл.")}",
        };

        if (CurrentWithoutVoltageA) lines.Add("Фаза А - ток при отсутствии напряжения");
        if (CurrentWithoutVoltageB) lines.Add("Фаза B - ток при отсутствии напряжения");
        if (CurrentWithoutVoltageC) lines.Add("Фаза C - ток при отсутствии напряжения");
        if (NegativeVoltageAngles) lines.Add("Отрицательные углы векторов напряжения фаз");

        return string.Join("\n", lines);
    }
}
