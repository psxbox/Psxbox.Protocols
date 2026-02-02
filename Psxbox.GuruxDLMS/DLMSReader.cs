using Gurux.Common;
using Gurux.DLMS;
using Gurux.DLMS.Enums;
using Microsoft.Extensions.Logging;

namespace Psxbox.GuruxDLMS;

public class DLMSReader
{
    private readonly ILogger<DLMSReader> logger;
    private readonly IGXMedia media;
    private readonly GXDLMSClient client;
    private readonly int timeout;

    public DLMSReader(GXDLMSClient client, IGXMedia media, ILogger<DLMSReader> logger = null, int timeout = 5000)
    {
        this.client = client;
        this.media = media;
        this.logger = logger;
        this.timeout = timeout;
    }

    public async System.Threading.Tasks.Task InitializeConnection()
    {
        GXReplyData reply = new GXReplyData();
        byte[] data = client.SNRMRequest();
        if (data != null)
        {
            await ReadDLMSPacket(data, reply);
            //Has server accepted client.
            client.ParseUAResponse(reply.Data);
        }

        //Generate AARQ request.
        //Split requests to multiple packets if needed. 
        //If password is used all data might not fit to one packet.
        foreach (byte[] it in client.AARQRequest())
        {
            {
                reply.Clear();
                await ReadDLMSPacket(it, reply);
            }
            //Parse reply.
            client.ParseAAREResponse(reply.Data);
        }
    }

    public async System.Threading.Tasks.Task Close()
    {
        GXReplyData reply = new GXReplyData();
        await ReadDLMSPacket(client.DisconnectRequest(), reply);
    }

    /// <summary>
    /// Send data block(s) to the meter.
    /// </summary>
    /// <param name="data">Send data block(s).</param>
    /// <param name="reply">Received reply from the meter.</param>
    /// <returns>Return false if frame is rejected.</returns>
    public async Task<bool> ReadDataBlock(byte[][] data, GXReplyData reply)
    {
        if (data == null)
        {
            return true;
        }
        foreach (byte[] it in data)
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    reply.Clear();
                    await ReadDataBlock(it, reply);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == 1)
                    {
                        throw;
                    }
                    else
                    {
                        logger.LogError("Read data block failed: {message}", ex.Message);
                    }
                }
            }
        }
        return reply.Error == 0;
    }

    /// <summary>
    /// Read data block from the device.
    /// </summary>
    /// <param name="data">data to send</param>
    /// <param name="text">Progress text.</param>
    /// <param name="multiplier"></param>
    /// <returns>Received data.</returns>
    public async System.Threading.Tasks.Task ReadDataBlock(byte[] data, GXReplyData reply)
    {
        await ReadDLMSPacket(data, reply);
        while (reply.IsMoreData && client.ConnectionState != ConnectionState.None)
        {
            if (reply.IsStreaming())
            {
                data = null;
            }
            else
            {
                data = client.ReceiverReady(reply);
            }
            await ReadDLMSPacket(data, reply);
        }
    }

    /// <summary>
    /// Read DLMS Data from the device.
    /// </summary>
    /// <param name="data">Data to send.</param>
    /// <returns>Received data.</returns>
    public async System.Threading.Tasks.Task ReadDLMSPacket(byte[] data, GXReplyData reply)
    {
        if (data == null)
        {
            return;
        }
        object eop = (byte)0x7E;
        //In network connection terminator is not used.
        if (client.InterfaceType == InterfaceType.WRAPPER)
        {
            eop = null;
        }
        int pos = 0;
        bool succeeded = false;
        ReceiveParameters<byte[]> p = new ReceiveParameters<byte[]>()
        {
            AllData = true,
            Eop = eop,
            Count = 5,
            WaitTime = timeout,
        };

        media.ResetSynchronousBuffer();
        while (!succeeded && pos != 3)
        {
            logger?.LogTrace("<- " + DateTime.Now.ToLongTimeString() + "\t" + GXCommon.ToHex(data, true));
            media.Send(data, null);

            await System.Threading.Tasks.Task.Delay(500);

            succeeded = media.Receive(p);
            if (!succeeded)
            {
                //If Eop is not set read one byte at time.
                if (p.Eop == null)
                {
                    p.Count = 1;
                }
                //Try to read again...
                if (++pos != 3)
                {
                    logger?.LogDebug("Data send failed. Try to resend " + pos.ToString() + "/3");
                    continue;
                }
                throw new Exception("Failed to receive reply from the device in given time.");
            }
        }
        //Loop until whole COSEM packet is received.    

        GXByteBuffer buffer = new(p.Reply);


        while (!client.GetData(buffer, reply))
        {
            //If Eop is not set read one byte at time.
            if (p.Eop == null)
            {
                p.Count = 1;
            }
            if (!media.Receive(p))
            {
                //Try to read again...
                if (pos != 3)
                {
                    System.Diagnostics.Debug.WriteLine("Data send failed. Try to resend " + pos.ToString() + "/3");
                    continue;
                }
                throw new Exception("Failed to receive reply from the device in given time.");
            }
        }

        logger?.LogTrace("-> " + DateTime.Now.ToLongTimeString() + "\t" + GXCommon.ToHex(p.Reply, true));
        if (reply.Error != 0)
        {
            throw new GXDLMSException(reply.Error);
        }

        await System.Threading.Tasks.Task.CompletedTask;
    }
}
