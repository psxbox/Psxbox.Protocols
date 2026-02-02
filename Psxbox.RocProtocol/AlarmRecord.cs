namespace Psxbox.RocProtocol
{
    public record struct AlarmRecord(
        byte AlarmType,
        byte AlarmCode,
        byte Seconds,
        byte Minutes,
        byte Hours,
        byte Day,
        byte Month,
        byte Year,
        string Tag,
        float Value);
}