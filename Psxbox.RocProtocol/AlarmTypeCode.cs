namespace Psxbox.RocProtocol;

internal class AlarmTypeCode
{
    public static (byte, byte) GetAlarmTypeNibbles(byte at) => ((byte)(at >> 4), (byte)(at & 0b00001111));

    public static (string alarmType, string alarmClearSet) GetAlarmTypeString(byte at)
    {
        (byte high, byte low) = GetAlarmTypeNibbles(at);

        string alarmTypeString = high switch
        {
            1 => "DP Sensor",
            2 => "SP Sensor",
            3 => "PT Sensor",
            5 => "I/O point (AIs, DIs, PIs, and AOs)",
            6 => "Meter run",
            7 => "User Text",
            8 => "User Value",
            9 => "Integral Sensor",
            _ => $"Unknown code: {high}"
        };

        string alarmClearSetString = low switch
        {
            0 => "Alarm Clear",
            1 => "Alarm Set",
            _ => $"Unknown code: {low}",
        };

        return (alarmTypeString, alarmClearSetString);
    }

    public static string GetAlarmCodeString(byte alarmType, byte alarmCode)
    {
        (byte high, _) = GetAlarmTypeNibbles(alarmType);

        return high switch
        {
            1 or 2 or 3 or 5 => alarmCode switch
            {
                0 => "Low Alarm",
                1 => "Lo Lo Alarm",
                2 => "High Alarm",
                3 => "Hi Hi Alarm",
                4 => "Rate Alarm",
                5 => "Status Change",
                6 => "Point Fail",
                7 => "Override Mode",
                _ => $"Unknown alarm code: {alarmCode}"
            },
            6 => alarmCode switch
            {
                0 => "Low Alarm",
                2 => "High Alarm",
                6 => "No Flow Alarm",
                7 => "Manual Mode",
                _ => $"Unknown alarm code: {alarmCode}"
            },
            9 => alarmCode switch
            {
                4 => "Input Freeze Mode (Calibration in progress)",
                6 => "Sensor Communications Fail Alarm",
                7 => "Scanning disabled",
                _ => $"Unknown alarm code: {alarmCode}"
            },
            _ => $"Unknown alarm type: {high}"
        };
    }
}
