using Microsoft.Extensions.Logging;
using Psxbox.Streams;

namespace Psxbox.CE30XProtocol;

public class ReaderCE301(IStream stream,
                         string id,
                         string password = "777777",
                         ILogger? logger = null) : ReaderCE303(stream, id, password, logger)
{
    public new const string READER_TYPE = "CE301";
    public override Task<(double a, double b, double c)> GetCorIU()
    {
        throw new NotImplementedException("GetCorIU is not supported in CE301 protocol.");
    }

    public override Task<(double a, double b, double c, double sum)> GetPowerR()
    {
        throw new NotImplementedException("GetPowerR is not supported in CE301 protocol.");
    }

    public override Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(
        bool forCurrentPeriod = false, string period = "day")
    {
        throw new NotImplementedException("GetReactiveEnergyIn is not supported in CE301 protocol.");
    }

    public override Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(
        bool forCurrentPeriod = false, string period = "day")
    {
        throw new NotImplementedException("GetReactiveEnergyOut is not supported in CE301 protocol.");
    }

    public override string[] GetEndOfDayFunctions() =>
        [
            CE303Function.ENDPE.ToString(),
        ];

    public override string[] GetLoadProfileFunctions() => 
        [
            CE303Function.GRAPE.ToString(),
        ];

    // CE301 RE larida ham "Журнал состояния фаз" (PHASE/PPHAS) mavjud, ammo sig'imi
    // versiyaga qarab farq qiladi (CE301 v11 RE — 50 yozuv, yangiroq CE301 RE — 200 yozuv).
    // Sig'im o'lchovdan tasdiqlanmaguncha CE301 uchun o'chirilgan holat saqlanadi
    // (noto'g'ri slot manzillab, jimgina xato ma'lumot olishdan himoya).
    public override Task<IEnumerable<(long recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func)
    {
        throw new NotImplementedException("GetPowerStatuses is not supported in CE301 protocol.");
    }

    public override string[] GetPowerStatusFunctions() => [];

}
