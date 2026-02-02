namespace Psxbox.RocProtocol;

public record struct HistoryParams((byte, byte, byte) PointTagIndentification, (byte, byte, byte) PointPath, byte ArchiveType,
    byte AvgOrRate)
{

}