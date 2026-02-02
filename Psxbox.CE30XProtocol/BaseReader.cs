using Microsoft.Extensions.Logging;
using Psxbox.Streams;
using System.Globalization;

namespace Psxbox.CE30XProtocol;

public abstract class BaseReader(IStream stream, string id, string password = "777777", ILogger? logger = null) : IDisposable
{
    protected readonly IStream stream = stream;
    protected readonly string id = id;
    protected readonly string password = password;
    protected readonly ILogger? logger = logger;
    private bool disposedValue;

    public string ID => id;

    public async Task<bool> Connect()
    {
        try
        {
            return await CommonIEC61107.ConnectAndAuthorize(stream, id, password);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "{ex}", ex.Message);
            return false;
        }
    }

    public async Task Disconnect()
    {
        await CommonIEC61107.Disconnect(stream);
    }

    protected async Task<string> SendAndGet(CE30XCommand cmd, string func, byte[] waitingLastBytes, params string[] paramArg)
    {
        string resultStr = string.Empty;
        var maxRetries = 2;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                resultStr = await CommonIEC61107.SendAndGet(stream, cmd, func, waitingLastBytes, paramArg);
                break;
            }
            catch (Exception)
            {
                if (attempt == maxRetries - 1)
                {
                    throw;
                }
                else
                {
                    await Disconnect();
                    await Task.Delay(1000);
                    await Connect();
                }
            }
        }

        return resultStr;
    }

    protected static double[] ParseDoubleValues(string response)
    {
        return CommonIEC61107.ParseResponseValues(response)
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture))
            .ToArray();
    }

    protected async Task<(double sum, double t1, double t2, double t3, double t4)> GetEnergyValues(string func)
    {
        var responceStr = await SendAndGet(CE30XCommand.R1, func, CommonIEC61107.DEFAULT_END);
        var values = ParseDoubleValues(responceStr);
        return (values[0], values[1], values[2], values[3], values[4]);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Task.Run(Disconnect);
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
