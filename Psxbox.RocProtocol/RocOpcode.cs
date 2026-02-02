namespace Psxbox.RocProtocol
{
    public enum RocOpcode
    {
        Opcode6 = 6,     // Sends FB Series configuration information
        Opcode7 = 7,     // Sends current time and date
        Opcode8 = 8,     // Sets new time and date
        OPcode17 = 17,   // Sets operator identification
        Opcode103 = 103, // Sends system information such as on/off times, manual/alarm status, firmware version, and current time and date
        Opcode120 = 120, // Sends pointers for alarm, event, and history logs
        Opcode121 = 121, // Sends specified number of alarms starting at specified alarm pointer
        Opcode122 = 122, // Sends specified number of events starting at specified event pointer
        Opcode130 = 130, // Sends archived hourly and daily data for specified history point starting at specified history pointer
        Opcode136 = 136, // Requests multiple history points for multiple time periods
        Opcode165 = 165, // Sends current history configuration data
        Opcode166 = 166, // Sets specified contiguous block of parameters
        Opcode167 = 167, // Sends specified contiguous block of parameters
        Opcode180 = 180, // Sends specified parameters
        Opcode181 = 181, // Sets specified parameters
        Opcode255 = 255, // Transmits error messages by FB Series in response to a request with invalid parameters or format
    }
}