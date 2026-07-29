namespace Psxbox.CE30XProtocol
{
    public enum CE303Function
    {
        DATE_,
        TIME_,
        FREQU, // Frequency
        CURRE, // Current
        VOLTA, // Voltage
        CORUU, // 2 faza orasidagi burchak
        POWPP, // Active power, kWt
        POWPQ, // Reactive power, KVar
        ENDPE, // End of day active +
        ENDQE, // End of day reactive +
        ENDQI, // End of day reactive -
        ENMPE, // End of month active +
        ET0PE, // Accumulated active in power
        ET0QE, // Accumulated reactive in power
        ET0QI, // Accumulated reactive out power
        CORIU, // I va U orasidagi burchak
        GRAPE, // Get load profile, active power, in
        GRAQE, // Get load profile, reactive power, in
        GRAQI, // Get load profile, reactive power, out
        POWEP, // Active power sum, kW
        POWEQ, // Reactive power in/out, kVar
        COS_f, // Power factor: sum, A, B, C
        TAN_f, // Tangent phi: sum, A, B, C
    }
}
