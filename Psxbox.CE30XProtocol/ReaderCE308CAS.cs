using System;
using Microsoft.Extensions.Logging;
using Psxbox.Streams;

namespace Psxbox.CE30XProtocol;

public class ReaderCE308CAS(IStream stream,
                            string id,
                            string password = "777777",
                            ILogger? logger = null) : ReaderCE308(stream, id, password, logger)
{
    public new const string READER_TYPE = "CE308CAS";

    protected override (string date, double tSum, double t1, double t2, double t3, double t4) ParseEndOfPeriod(string[] values)
    {
        // CE308CAS uchun maxsus parsing
        // values[0]: "16.09.25,00:00:00,-300,0x0->1.05276"
        // values[1]: "0.0"
        // values[2]: "0.89293"
        // values[3]: "0.0"
        // values[4]: "0.15983"
        var dateAndSum = values[0].Split(',');
        string date = dateAndSum[0];
        // Sum qiymati "0x0->1.05276" ko'rinishida bo'lishi mumkin, shuni ajratib olish kerak
        string sumStr = dateAndSum.Length > 3 && dateAndSum[3].Contains("->")
            ? dateAndSum[3].Split("->")[1]
            : dateAndSum[1];
        double tSum = double.Parse(sumStr, System.Globalization.CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture);
        return (date, tSum, t1, t2, t3, t4);
    }
}
