using Psxbox.Streams;

namespace Psxbox.RocProtocol;

public interface IRocProtocol : IDisposable
{
    IStream? Stream { get; set; }

    Task<byte[]> Requests(ROCAddress rocAddress,
                          ROCAddress hostAddress,
                          RocOpcode opcode,
                          byte[] requestData);

    /// <summary>
    /// Opcode 180 reads several parameters in a single request.
    /// </summary>
    /// <param name="rocDeviceSettings"></param>
    /// <param name="paramsCount"></param>
    /// <param name="parameters"></param>
    /// <returns>Data comprising the parameter</returns>
    Task<byte[]> RequestOpcode180(RocDeviceSettings rocDeviceSettings,
                            IEnumerable<(byte point, byte logic, byte param)> parameters);

    /// <summary>
    /// Opcode 120 also sends the current hour (periodic) and day pointers for the history groups 
    /// and maximum number of logs for each group.
    /// </summary>
    /// <param name="rocDeviceSettings"></param>
    /// <returns></returns>
    Task<HistoryPointers> RequestOpcode120(RocDeviceSettings rocDeviceSettings);

    /// <summary>
    /// Opcode 130 requests a specified number of hourly or daily data values for a specified 
    /// history point from history group 1 (User periodic 1) or group 4 (Station 1) starting at a specified history pointer.
    /// <list>The current history index for each group can be retrieved by Opcode 120.</list>
    /// <list>The starting history index specifies the beginning record for hourly values or daily values:</list>
    /// <list>• Daily Values: 840 + x, where x can be 0 – 34 to indicate the starting history index.</list>
    /// <list>• Hourly Values: 0 – 839 </list>
    /// </summary>
    /// <param name="rocDeviceSettings"></param>
    /// <param name="typeOfHistory">Type of History: 0 = Hourly or Daily (Standard) 1 = Extended</param>
    /// <param name="historyPointNumber">History Point Number (0-59, for Timestamp specify 254)</param>
    /// <param name="count">Number of history values requested (maximum 60)</param>
    /// <param name="index">Starting history index (0-839 for hourly, 840-874 for daily)</param>
    /// <returns>History values in bytes</returns>
    public Task<byte[]> RequestOpcode130(RocDeviceSettings rocDeviceSettings,
                                   byte typeOfHistory,
                                   byte historyPointNumber,
                                   byte count,
                                   short index);

    /// <summary>
    /// Opcode 167 reads the configuration of a single point or it can be used to read a
    /// contiguous block of parameters for a single point. Opcode 167 is more efficient than
    /// Opcode 180 when reading the entire, or even partial, point configuration.
    /// </summary>
    /// <param name="rocDeviceSettings">ROC and HOST sddresses</param>
    /// <param name="pointType">Point Type</param>
    /// <param name="logicalNumber">Logic Number</param>
    /// <param name="parametersCount">Number of Parameters</param>
    /// <param name="startingIndex">Starting Parameter Number</param>
    /// <returns>Requested values in byte array</returns>
    Task<byte[]> RequestOpcode167(RocDeviceSettings rocDeviceSettings,
                            RocPointType pointType,
                            byte logicalNumber,
                            byte parametersCount,
                            byte startingIndex);

    /// <summary>
    /// Opcode 121 requests alarm data from the Alarm Log in the FB Series. The Alarm Log
    /// consists of a maximum of 240 alarms.
    /// </summary>
    /// <param name="rocDeviceSettings">ROC and HOST sddresses</param>
    /// <param name="numberOfAlarms">Number of alarms requested (maximum 10)</param>
    /// <param name="startingPointer"></param>
    /// <returns></returns>
    public Task<IEnumerable<AlarmRecord>> RequestOpcode121(RocDeviceSettings rocDeviceSettings,
                                                     byte numberOfAlarms,
                                                     short startingPointer);
}