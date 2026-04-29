using System.Globalization;
using Microsoft.Extensions.Logging;
using Psxbox.Streams;

namespace Psxbox.CE30XProtocol;

public class ReaderCE308CAS(IStream stream,
                            string id,
                            string password = "777777",
                            ILogger? logger = null) : ReaderCE308(stream, id, password, logger)
{
    public new const string READER_TYPE = "CE308CAS";

    protected override (string date, double tSum, double t1, double t2, double t3, double t4) ParseEndOfPeriod(string[] values, ArchiveType archiveType)
    {
        // CE308CAS uchun maxsus parsing
        // values[0]: "16.09.25,00:00:00,-300,0x0->1.05276"
        // values[1]: "0.0"
        // values[2]: "0.89293"
        // values[3]: "0.0"
        // values[4]: "0.15983"
        var dateAndSum = values[0].Split(',');
        string date = ParseArchiveTime(values[0], archiveType).ToString("dd.MM.yy", CultureInfo.InvariantCulture);  //dateAndSum[0];
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

    protected override List<DateOnly> ParseArchiveTimes(IEnumerable<string> archiveTimes, ArchiveType archiveType)
    {
        // CE308CAS uchun maxsus
        // archiveTimes elementlari "16.09.25,00:00:00,-300,0x0" ko'rinishida bo'lishi mumkin, shundan faqat sana qismni olish kerak
        return archiveTimes.Select(at => ParseArchiveTime(at, archiveType)).ToList();
    }

    protected override DateOnly ParseArchiveTime(string archiveTimeStr, ArchiveType archiveType)
    {
        // CE308CAS uchun maxsus
        // archiveTimeStr "16.09.25,00:00:00,-300,0x0" ko'rinishida bo'lishi mumkin, shundan faqat sana qismni olish kerak
        var datePart = archiveTimeStr.Split(',')[0];
        var dateOnly = DateOnly.TryParseExact(datePart, "dd.MM.yy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) ? date : default;

        return archiveType switch
        {
            ArchiveType.Day => dateOnly.AddDays(-1), // CE308CAS da daily archive sanasi o'sha kun emas, balki oldingi kun bo'ladi
            ArchiveType.Month => dateOnly.AddMonths(-1),
            ArchiveType.Year => dateOnly.AddYears(-1),
            _ => dateOnly,
        };
    }
}
