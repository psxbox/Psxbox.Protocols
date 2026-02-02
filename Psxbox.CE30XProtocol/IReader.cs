namespace Psxbox.CE30XProtocol;

public interface IReader : IDisposable
{
    /// <summary>
    /// Hisoblagich ID
    /// </summary>
    string ID { get; }

    /// <summary>
    /// Bog'lanish
    /// </summary>
    /// <returns><b>true</b> bo'lsa bog'landi, aks holda yo'q</returns>
    Task<bool> Connect();

    /// <summary>
    /// Sessiyani tugatish
    /// </summary>
    Task Disconnect();

    /// <summary>
    /// Hisoblagich soatini o'qish
    /// </summary>
    /// <returns></returns>
    Task<DateTimeOffset> GetWatch();

    /// <summary>
    /// Elektr linyasini joriy chastotasini o'qish
    /// </summary>
    /// <returns></returns>
    Task<double> GetFrequency();

    /// <summary>
    /// Fazalardagi tok kuchi
    /// </summary>
    Task<(double a, double b, double c)> GetCurrent();

    /// <summary>
    /// Fazalardagi kuchlanish
    /// </summary>
    Task<(double a, double b, double c)> GetVoltage();

    /// <summary>
    /// To'liq quvvat, kVA
    /// </summary>
    Task<(double a, double b, double c, double sum)> GetPowerS();

    /// <summary>
    /// Aktiv quvvat, kW
    /// </summary>
    Task<(double a, double b, double c, double sum)> GetPowerA();

    /// <summary>
    /// Reaktiv quvvat, kVar
    /// </summary>
    Task<(double a, double b, double c, double sum)> GetPowerR();

    /// <summary>
    /// Tok kuchi va kuchlanish burchagi, ℃
    /// </summary>
    Task<(double a, double b, double c)> GetCorIU();

    /// <summary>
    /// Fazalar burchagi, ℃
    /// </summary>
    Task<(double ab, double bc, double ca)> GetCorUU();

    /// <summary>
    /// Period (kun, oy, yil va h.k.) oxiri ma'lumotlarini olish
    /// </summary>
    /// <param name="ago">Joriy davrdan necha davr avval</param>
    /// <param name="func">Funksiya. Bu yerda hamma funksiyani ham ishlatib bo'lmaydi.
    ///     Faqat kun, oy, yil va shunga o'xshash arxivlarni olish funksiyalari ishlatiladi.</param>
    /// <param name="args">Qo'shimcha parametrlar (bazi readerlarda ishlatilishi mumkin)</param>
    /// <returns>Tuple ko'rinishida (Date, Summa, T1 tarif, T2 tarif, T3 tarif, T4 tarif)</returns>
    Task<(string date, double tSum, double t1, double t2, double t3, double t4)> GetEndOfPeriod(
        ushort ago, string func, params string[] args);

    /// <summary>
    /// Arhiv yozuvlari sanalarini o'qish
    /// </summary>
    /// <param name="func">Funksiya. Bu yerda LST01, LST02, LST03, LST04 kabi funksiyalarni ishlatish
    /// mumkin.</param>
    /// <returns>Sanalar ro'yxati</returns>
    Task<IEnumerable<string>> GetListOfArchiveTimes(string func);

    /// <summary>
    /// Профиль нагрузки (получасовка) funksiyalarining ro'yxati
    /// </summary>
    /// <returns> 
    /// string[] ko'rinishida funksiyalar ro'yxati.
    /// </returns>
    string[] GetLoadProfileFunctions();

    /// <summary>
    /// профиль нагрузки (получасовка)
    /// </summary>
    /// <param name="daysAgo">Joriy kundan necha kun avval, agar 0 bo'lsa shu kun</param>
    /// <param name="fromRecord">Shu kundagi qaysi yozuvdan o'qish kerakligi</param>
    /// <param name="func">Funksiya. VPR01, VPR02, VPR03, VPR04 funksiyalarni ishlatish kerak.</param>
    /// <returns></returns>
    Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(ushort daysAgo,
        short fromRecord, string func);

    /// <summary>
    /// Retrieves load profile data for a specified date and function.
    /// </summary>
    /// <remarks>This method retrieves load profile data for a specific date and function, starting
    /// from the specified record index. The data is returned as a collection of tuples, where each tuple contains a
    /// load value and its corresponding record index.</remarks>
    /// <param name="lastReadedDate">The date for which to retrieve the load profiles.</param>
    /// <param name="deviceDateTime">The starting record index for the data retrieval. Must be a non-negative value.</param>
    /// <param name="func">The function identifier used to filter the load profiles. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with: <list
    /// type="bullet"> <item> <description>The date associated with the retrieved load profiles.</description>
    /// </item> <item> <description>An enumerable collection of tuples, where each tuple contains: <list
    /// type="bullet"> <item><description>A float representing the load value.</description></item>
    /// <item><description>A short representing the record index.</description></item> </list> </description>
    /// </item> </list></returns>
    Task<(string date, IEnumerable<(double, short)> data)> GetLoadProfiles(DateTimeOffset lastReadedDate,
        DateTimeOffset deviceDateTime, string func);

    /// <summary>
    /// LNE04 - Poyavleniye i propadaniye silovogo pitaniya schetchika, 
    /// LNE05 - Polnoe propadaniya pitaniya,
    /// LNE22 - Sostoyaniye litevogo elementa pitaniya
    /// </summary>
    /// <param name="func">Funksiya. LNE04, LNE05, LNE22 funksiyalarni ishlatish kerak.</param>
    /// <returns></returns>
    Task<IEnumerable<(ushort recNo, DateTimeOffset dateTime, byte status)>> GetPowerStatuses(string func);

    /// <summary>
    /// Yig'ilgan aktiv energiya +
    /// </summary>
    /// <returns>Summa va tariflar bo'yish qiymatlar</returns>
    Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyIn(bool forCurrentPeriod = false, string period = "day");

    /// <summary>
    /// Yig'ilgan aktiv energiya -
    /// </summary>
    /// <returns>Summa va tariflar bo'yish qiymatlar</returns>
    Task<(double sum, double t1, double t2, double t3, double t4)> GetActiveEnergyOut(bool forCurrentPeriod = false, string period = "day");

    /// <summary>
    /// Yig'ilgan reaktiv energiya +
    /// </summary>
    /// <returns>Summa va tariflar bo'yish qiymatlar</returns>
    Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyIn(bool forCurrentPeriod = false, string period = "day");

    /// <summary>
    /// Yig'ilgan reaktiv energiya -
    /// </summary>
    /// <returns>Summa va tariflar bo'yish qiymatlar</returns>
    Task<(double sum, double t1, double t2, double t3, double t4)> GetReactiveEnergyOut(bool forCurrentPeriod = false, string period = "day");

    string[] GetEndOfDayFunctions();
    string[] GetEndOfMonthFunctions();
    string[] GetEndOfYearFunctions();
    string[] GetCurrentDayFunctions();
    string[] GetCurrentMonthFunctions();
    string[] GetCurrentYearFunctions();
}
