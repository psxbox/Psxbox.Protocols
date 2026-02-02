namespace Psxbox.CE30XProtocol;

public enum CE6850MFunction
{
    DATE_, // Date
    TIME_, // Time
    FREQU, // Frequency
    CURRE, // Current
    VOLTA, // Voltage
    POWES, // Apparent power, KVA
    POWEP, // Active power, KWt
    POWEQ, // Reactive power, KVar
    CORIU, // I va U orasidagi burchak
    CORUU, // 2 faza orasidagi burchak
    ET0PE, // Accumulated active energy, in
    ET0PI, // Accumulated active energy, out
    ET0QE, // Accumulated reactive energy, in
    ET0QI, // Accumulated reactive energy, out
    DATED, // Get list of dates from day archive
    DATEM, // Get list of dates from month archive
    ED0PE, // Get accumulated active energy from day archive, in
    ED0PI, // Get accumulated active energy from day archive, out
    ED0QE, // Get accumulated reactive energy from day archive, in
    ED0QI, // Get accumulated reactive energy from day archive, out
    EM0PE, // Get accumulated active energy from month archive, in
    EM0PI, // Get accumulated active energy from month archive, out
    EM0QE, // Get accumulated reactive energy from month archive, in
    EM0QI, // Get accumulated reactive energy from month archive, out
    GRAPE, // Get load profile, active power, in
    GRAPI, // Get load profile, active power, out
    GRAQE, // Get load profile, reactive power, in
    GRAQI, // Get load profile, reactive power, out
}

