using Microsoft.Extensions.Logging;
using Psxbox.Streams;

namespace Psxbox.CE30XProtocol
{
    public class ReaderCE6850(IStream stream,
                              string id,
                              string password = "",
                              ILogger? logger = null) : ReaderCE6850M(stream, id, password, logger)
    {
        public new const string READER_TYPE = "CE6850";

        public override Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(
            ushort ago, string func, params string[] args)
        {
            throw new NotSupportedException("CE6850 hisoblagichda to'plangan energiya arxivlari mavjud emas!");
        }

        protected override int ReadCount => 48;
        protected override string FormatLoadProfileParams(DateTimeOffset date, int fromRecord, int count)
        {
            return $"{date:d.M}.{fromRecord}.{count}";
        }

        public override async Task<DateTimeOffset> GetWatch()
        {
            logger?.LogDebug("Getting watch");
            var responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.DATE_.ToString(), CommonIEC61107.DEFAULT_END);
            var values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
            var date = DateOnly.ParseExact(values[0][3..], "dd.MM.yy");
            responceStr = await SendAndGet(CE30XCommand.R1, CE6850MFunction.TIME_.ToString(), CommonIEC61107.DEFAULT_END);
            values = CommonIEC61107.ParseResponseValues(responceStr).ToArray();
            var time = TimeOnly.ParseExact(values[0], "HH:mm:ss");

            var result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(5));
            return result;
        }
    }
}
