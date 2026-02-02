namespace Psxbox.RocProtocol;

public record struct AlarmResult(DateTimeOffset DateTime, string Tag, string SetClear, object Value, string Description);
