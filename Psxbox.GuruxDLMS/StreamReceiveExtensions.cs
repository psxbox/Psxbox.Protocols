using Gurux.Common;
using Psxbox.Streams;

namespace Psxbox.GuruxDLMS;

public static class StreamReceiveExtensions
{
    public static async Task<bool> ReceiveAsync<T>(this IStream stream, ReceiveParameters<T> args, CancellationToken ct = default)
    {
        if (args.Eop == null && args.Count == 0 && !args.AllData)
            throw new ArgumentException("Either Count or Eop must be set.");

        byte[] terminator = null;
        if (args.Eop != null)
        {
            terminator = args.Eop is Array arr
                ? GXCommon.GetAsByteArray(arr.GetValue(0))
                : GXCommon.GetAsByteArray(args.Eop);
        }

        int waitTime = args.WaitTime;
        if (waitTime <= 0) waitTime = stream.OperationTimeout;

        int originalTimeout = stream.OperationTimeout;
        stream.OperationTimeout = waitTime;

        try
        {
            List<byte> dataList = new();

            if (terminator != null && terminator.Length > 0)
            {
                byte[] received = await stream.ReadUntilAsync(terminator, ct).ConfigureAwait(false);
                dataList.AddRange(received);

                int maxRead = 1000;
                int readCount = 0;
                while (stream.Available && readCount < maxRead)
                {
                    received = await stream.ReadUntilAsync(terminator, ct).ConfigureAwait(false);
                    dataList.AddRange(received);
                    readCount++;
                    if (received.Length == 0) break;
                }
            }
            else if (args.Count > 0)
            {
                byte[] buf = new byte[args.Count];
                int read = await stream.ReadAsync(buf, ct).ConfigureAwait(false);
                for (int i = 0; i < read; i++)
                    dataList.Add(buf[i]);
            }

            if (dataList.Count == 0)
                return false;

            byte[] result = dataList.ToArray();
            int readBytes;
            object data = GXCommon.ByteArrayToObject(result, typeof(T), out readBytes);
            args.Count = 0;

            if (args.Reply == null)
                args.Reply = (T)data;
            else if (args.Reply is Array oldArr)
            {
                if (data is not Array newArr)
                    throw new ArgumentException("Data is not an array.");
                int len = oldArr.Length + newArr.Length;
                Array arr2 = (Array)Activator.CreateInstance(typeof(T), len);
                Array.Copy(oldArr, arr2, oldArr.Length);
                Array.Copy(newArr, 0, arr2, oldArr.Length, newArr.Length);
                args.Reply = (T)(object)arr2;
            }
            else if (args.Reply is string str)
            {
                args.Reply = (T)(object)(str + (string)data);
            }

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            stream.OperationTimeout = originalTimeout;
        }
    }
}
