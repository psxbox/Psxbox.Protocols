using System.Globalization;
using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using Psxbox.Utils.Helpers;

namespace Psxbox.CE30XProtocol;

public class ReaderCE308CAS(IStream stream,
                            string id,
                            string password = "777777",
                            ILogger? logger = null) : ReaderCE308(stream, id, password, logger)
{
    public new const string READER_TYPE = "CE308CAS";
    public override int LoadProfilePeriodInMinutes => 15;
    public override int LoadProfileCountPerRequest => 24;

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
        double tSum = double.Parse(sumStr, CultureInfo.InvariantCulture);
        double t1 = double.Parse(values[1], CultureInfo.InvariantCulture);
        double t2 = double.Parse(values[2], CultureInfo.InvariantCulture);
        double t3 = double.Parse(values[3], CultureInfo.InvariantCulture);
        double t4 = double.Parse(values[4], CultureInfo.InvariantCulture);
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
        var dateOnly = DateOnly.TryParseExact(datePart, "dd.MM.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : default;

        return archiveType switch
        {
            ArchiveType.Day => dateOnly.AddDays(-1), // CE308CAS da daily archive sanasi o'sha kun emas, balki oldingi kun bo'ladi
            ArchiveType.Month => dateOnly.AddMonths(-1),
            ArchiveType.Year => dateOnly.AddYears(-1),
            _ => dateOnly,
        };
    }

    public override async Task<IEnumerable<(DateTimeOffset dateTime, double value, short status)>> GetLoadProfiles(DateTimeOffset lastReadedDate, DateTimeOffset deviceDateTime, string func)
    {
        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            logger.LogDebug("Getting load profiles {func}, Date: {date}, Device date: {index}", func, lastReadedDate, deviceDateTime);
        }

        MatchMinute15Div(ref deviceDateTime);
        MatchMinute15Div(ref lastReadedDate);

        var lastRecordIndex = (int)((deviceDateTime - lastReadedDate).TotalMinutes / LoadProfilePeriodInMinutes);
        var chunkIndex = lastRecordIndex / LoadProfileCountPerRequest + 1;

        if (lastReadedDate > deviceDateTime)
        {
            throw new Exception("Oxirgi o'qilgan vaqt qurilma vaqtidan katta");
        }
        
        var responceStr = await SendAndGet(CE30XCommand.R1, func, [CommonIEC61107.ETX], chunkIndex.ToString(), LoadProfileCountPerRequest.ToString());
        string[] values = [.. CommonIEC61107.ParseResponseValues(responceStr)];

        List<(DateTimeOffset dateTime, double value, short status)> data = [];

        foreach (var item in values)
        {
            var splitted = item.Split(',');
            var dateFromDevice = DateOnly.ParseExact(splitted[0], "dd.MM.yy", CultureInfo.InvariantCulture);
            var timeFromDevice = TimeOnly.ParseExact(splitted[1], "hh:mm", CultureInfo.InvariantCulture);
            var dateTimeFromDevice = new DateTimeOffset(dateFromDevice.ToDateTime(timeFromDevice), TimeSpan.FromHours(5));
            
            var value = double.Parse(splitted[3], CultureInfo.InvariantCulture);
            var status = Convert.ToInt16(splitted[4].Trim(), 16);

            data.Add((dateTimeFromDevice, value, status));
        }

        return data.Where(d => d.dateTime >= lastReadedDate && d.dateTime <= deviceDateTime).OrderBy(d => d.dateTime);
    }

    private void MatchMinute15Div(ref DateTimeOffset dateTimeOffset)
    {
        var minute = dateTimeOffset.Minute;
        var temp = dateTimeOffset.AddMinutes(-minute % LoadProfilePeriodInMinutes);
        dateTimeOffset = new DateTimeOffset(temp.Year, temp.Month, temp.Day, temp.Hour, temp.Minute, 0, temp.Offset);
    }

    // CE308CAS uchun GetRecordDateTime metodi kerak emas, chunki bu reader load profile uchun har bir recordni date va time bilan qaytaradi.
    // protected override DateTimeOffset GetRecordDateTime(DateTimeOffset dateTimeOffset, int fromRecord, int recordIndex)
    // {
    //     throw new NotImplementedException();
    // }
}
