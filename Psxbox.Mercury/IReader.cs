
namespace Psxbox.Mercury;

public interface IReader
{
    Task<bool> Open(byte address, byte level, string password, bool passwordIsHex = false);
    Task Close(byte address);
    Task<(float ab, float ac, float bc)> GetAngleOfUU(byte address);
    Task<(float a, float b, float c)> GetVoltages(byte address);
    Task<(float a, float b, float c)> GetCurrents(byte address);
    Task<float> GetFrequency(byte address);
    Task<(float sum, float a, float b, float c)> GetPowerP(byte address);
    Task<(float sum, float a, float b, float c)> GetPowerQ(byte address);
    Task<(float sum, float a, float b, float c)> GetPowerS(byte address);
    Task<(float avg, float a, float b, float c)> GetPowerFactor(byte address);
    IAsyncEnumerable<(DateOnly date, byte tarif, float v1, float v2, float v3, float v4)> GetArchive(
        byte address, ArchiveType archiveType, DateOnly from, DateOnly to);
    Task ReadWatch(byte address);
    IAsyncEnumerable<(byte tarif, float a1, float a2, float r1, float r2)> GetLastEnergy(byte address);
}
