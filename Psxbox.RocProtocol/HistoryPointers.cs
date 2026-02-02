namespace Psxbox.RocProtocol
{
    public class HistoryPointers
    {
        public short AlarmLogPointer { get; internal set; }
        public short EventLogPointer { get; internal set; }
        public short StationHourlyHistoryIndex { get; internal set; }
        public short UserPeriodicHourlyHistoryIndex { get; internal set; }
        public short UserPeriodicHourlyHistoryLogsCount { get; internal set; }
        public byte StationDailyHistoryIndex { get; internal set; }
        public byte DailyHistoryLogsCount { get; internal set; }
        public byte HourlyHistoryLogsDays { get; internal set; }
        public byte UserPeriodicHistoryLogsDays { get; internal set; }
    }
}