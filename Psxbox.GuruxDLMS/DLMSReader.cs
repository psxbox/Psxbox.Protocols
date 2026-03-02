using Gurux.Common;
using Gurux.DLMS.Enums;
using Gurux.DLMS.Objects;
using Gurux.DLMS.Secure;
using Psxbox.GuruxDLMS;
using Psxbox.Streams;
using System.Diagnostics;
using System.IO.Ports;
using Task = System.Threading.Tasks.Task;

namespace Gurux.DLMS.Reader
{
    public class DLMSReader
    {
        public int WaitTime = 5000;
        public int RetryCount = 3;

        private readonly IStream _stream;
        private readonly TraceLevel Trace;
        private readonly GXDLMSSecureClient Client;
        private readonly string InvocationCounter;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public Action<object> OnNotification;

        public DLMSReader(GXDLMSSecureClient client, IStream stream, TraceLevel trace, string invocationCounter)
        {
            Trace = trace;
            _stream = stream;
            Client = client;
            InvocationCounter = invocationCounter;
        }

        private async Task SendAsync(byte[] data)
        {
            await _stream.WriteAsync(data).ConfigureAwait(false);
            await Task.Delay(200).ConfigureAwait(false);
        }

        public async Task SNRMRequestAsync(CancellationToken ct = default)
        {
            GXReplyData reply = new GXReplyData();
            byte[] data = Client.SNRMRequest();
            if (data != null)
            {
                if (Trace > TraceLevel.Info)
                    Console.WriteLine("Send SNRM request." + GXCommon.ToHex(data, true));
                await ReadDataBlockAsync(data, reply, ct).ConfigureAwait(false);
                if (Trace == TraceLevel.Verbose)
                    Console.WriteLine("Parsing UA reply." + reply.ToString());
                Client.ParseUAResponse(reply.Data);
                if (Trace > TraceLevel.Info)
                    Console.WriteLine("Parsing UA reply succeeded.");
            }
        }

        public async Task AarqRequestAsync(CancellationToken ct = default)
        {
            GXReplyData reply = new GXReplyData();
            var aarq = Client.AARQRequest();
            if (aarq.Length != 0)
            {
                foreach (byte[] it in aarq)
                {
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine("Send AARQ request", GXCommon.ToHex(it, true));
                    reply.Clear();
                    await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
                }
                if (Trace > TraceLevel.Info)
                    Console.WriteLine("Parsing AARE reply" + reply.ToString());
                Client.ParseAAREResponse(reply.Data);
                reply.Clear();
                if (Client.Authentication > Authentication.Low)
                {
                    foreach (byte[] it in Client.GetApplicationAssociationRequest())
                    {
                        reply.Clear();
                        await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
                    }
                    Client.ParseApplicationAssociationResponse(reply.Data);
                }
                if (Trace > TraceLevel.Info)
                    Console.WriteLine("Parsing AARE reply succeeded.");
            }
        }

        private async Task UpdateFrameCounterAsync(CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(InvocationCounter) && Client.Ciphering != null && Client.Ciphering.Security != Security.None)
            {
                await InitializeOpticalHeadAsync(ct).ConfigureAwait(false);
                byte[] data;
                GXReplyData reply = new GXReplyData();
                int add = Client.ClientAddress;
                int serverAddress = Client.ServerAddress;
                Authentication auth = Client.Authentication;
                Security security = Client.Ciphering.Security;
                Signing signing = Client.Ciphering.Signing;
                byte[] challenge = Client.CtoSChallenge;
                byte[] serverSystemTitle = Client.ServerSystemTitle;
                try
                {
                    Client.ServerSystemTitle = null;
                    Client.ClientAddress = 16;
                    Client.Authentication = Authentication.None;
                    Client.Ciphering.Security = Security.None;
                    Client.Ciphering.Signing = Signing.None;
                    data = Client.SNRMRequest();
                    if (data != null)
                    {
                        if (Trace > TraceLevel.Info)
                            Console.WriteLine("Send SNRM request." + GXCommon.ToHex(data, true));
                        await ReadDataBlockAsync(data, reply, ct).ConfigureAwait(false);
                        if (Trace == TraceLevel.Verbose)
                            Console.WriteLine("Parsing UA reply." + reply.ToString());
                        Client.ParseUAResponse(reply.Data);
                        if (Trace > TraceLevel.Info)
                            Console.WriteLine("Parsing UA reply succeeded.");
                    }
                    foreach (byte[] it in Client.AARQRequest())
                    {
                        if (Trace > TraceLevel.Info)
                            Console.WriteLine("Send AARQ request", GXCommon.ToHex(it, true));
                        reply.Clear();
                        await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
                    }
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine("Parsing AARE reply" + reply.ToString());
                    try
                    {
                        Client.ParseAAREResponse(reply.Data);
                        reply.Clear();
                        GXDLMSData d = new GXDLMSData(InvocationCounter);
                        await ReadAsync(d, 2, ct).ConfigureAwait(false);
                        Client.Ciphering.InvocationCounter = 1 + Convert.ToUInt32(d.Value);
                        Console.WriteLine("Invocation counter: " + Convert.ToString(Client.Ciphering.InvocationCounter));
                        reply.Clear();
                        await DisconnectAsync().ConfigureAwait(false);
                        if (Client.InterfaceType == InterfaceType.HdlcWithModeE)
                        {
                            await _stream.CloseAsync().ConfigureAwait(false);
                        }
                    }
                    catch (Exception)
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                        throw;
                    }
                }
                finally
                {
                    Client.ServerSystemTitle = serverSystemTitle;
                    Client.ClientAddress = add;
                    Client.ServerAddress = serverAddress;
                    Client.Authentication = auth;
                    Client.Ciphering.Security = security;
                    Client.CtoSChallenge = challenge;
                    Client.Ciphering.Signing = signing;
                    if (Client.PreEstablishedConnection)
                    {
                        Client.NegotiatedConformance |= Conformance.GeneralProtection;
                    }
                }
            }
        }

        private async Task DiscIECAsync(CancellationToken ct = default)
        {
            ReceiveParameters<string> p = new ReceiveParameters<string>()
            {
                AllData = false,
                Eop = (byte)0x0A,
                WaitTime = WaitTime * 1000
            };
            string data = (char)0x01 + "B0" + (char)0x03 + "\r\n";
            await SendAsync(System.Text.Encoding.ASCII.GetBytes(data)).ConfigureAwait(false);
            p.Eop = "\n";
            p.AllData = true;
            p.Count = 1;
            await _stream.ReceiveAsync(p, ct).ConfigureAwait(false);
        }

        public async Task InitializeOpticalHeadAsync(CancellationToken ct = default)
        {
            if (Client.InterfaceType != InterfaceType.HdlcWithModeE)
                return;

            SerialStream serial = _stream as SerialStream;
            byte Terminator = (byte)0x0A;
            await _stream.ConnectAsync().ConfigureAwait(false);
            await Task.Delay(1000, ct).ConfigureAwait(false);

            string data = "/?!\r\n";
            if (Trace > TraceLevel.Info)
                Console.WriteLine("IEC Sending:" + data);

            ReceiveParameters<string> p = new ReceiveParameters<string>()
            {
                AllData = false,
                Eop = Terminator,
                WaitTime = WaitTime * 1000
            };

            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await SendAsync(System.Text.Encoding.ASCII.GetBytes(data)).ConfigureAwait(false);
                if (!await _stream.ReceiveAsync(p, ct).ConfigureAwait(false))
                {
                    try
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                    }
                    catch (Exception) { }
                    await DiscIECAsync(ct).ConfigureAwait(false);
                    string str = "Failed to receive reply from the device in given time.";
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine(str);
                    await SendAsync(System.Text.Encoding.ASCII.GetBytes(data)).ConfigureAwait(false);
                    if (!await _stream.ReceiveAsync(p, ct).ConfigureAwait(false))
                        throw new Exception(str);
                }
                if (p.Reply == data)
                {
                    p.Reply = null;
                    if (!await _stream.ReceiveAsync(p, ct).ConfigureAwait(false))
                    {
                        GXReplyData reply = new GXReplyData();
                        await DisconnectAsync().ConfigureAwait(false);
                        if (serial != null)
                        {
                            await DiscIECAsync(ct).ConfigureAwait(false);
                            serial.DtrEnable = serial.RtsEnable = false;
                            serial.BaudRate = 9600;
                            serial.DtrEnable = serial.RtsEnable = true;
                            await DiscIECAsync(ct).ConfigureAwait(false);
                        }
                        data = "Failed to receive reply from the device in given time.";
                        if (Trace > TraceLevel.Info)
                            Console.WriteLine(data);
                        throw new Exception(data);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }

            if (Trace > TraceLevel.Info)
                Console.WriteLine("IEC received: " + p.Reply);

            int pos = 0;
            while (pos < p.Reply.Length && p.Reply[pos] != '/')
                ++pos;
            if (p.Reply[pos] != '/')
            {
                p.WaitTime = 100;
                await _stream.ReceiveAsync(p, ct).ConfigureAwait(false);
                await DiscIECAsync(ct).ConfigureAwait(false);
                throw new Exception("Invalid responce.");
            }
            string manufactureID = p.Reply.Substring(1 + pos, 3);
            char baudrate = p.Reply[4 + pos];
            int BaudRate = baudrate switch
            {
                '0' => 300,
                '1' => 600,
                '2' => 1200,
                '3' => 2400,
                '4' => 4800,
                '5' => 9600,
                '6' => 19200,
                _ => throw new Exception("Unknown baud rate.")
            };
            if (Trace > TraceLevel.Info)
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "\tBaudRate is : " + BaudRate.ToString());

            byte controlCharacter = (byte)'2';
            byte ModeControlCharacter = (byte)'2';
            byte[] arr = new byte[] { 0x06, controlCharacter, (byte)baudrate, ModeControlCharacter, 13, 10 };
            if (Trace > TraceLevel.Info)
                Console.WriteLine(DateTime.Now.ToLongTimeString() + "\tMoving to mode E.", arr);

            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                p.Reply = null;
                await SendAsync(arr).ConfigureAwait(false);
                await Task.Delay(200, ct).ConfigureAwait(false);
                p.WaitTime = 2000;
                await _stream.ReceiveAsync(p, ct).ConfigureAwait(false);
                if (p.Reply != null)
                {
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine("Received: " + p.Reply);
                }
                if (serial != null)
                {
                    _stream.Close();
                    serial.BaudRate = BaudRate;
                    serial.DataBits = 8;
                    serial.Parity = Parity.None;
                    serial.StopBits = StopBits.One;
                    await _stream.ConnectAsync().ConfigureAwait(false);
                }
                await Task.Delay(800, ct).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task InitializeConnectionAsync(CancellationToken ct = default)
        {
            Console.WriteLine("Standard: " + Client.Standard);
            if (Client.Ciphering.Security != Security.None)
            {
                Console.WriteLine("Security: " + Client.Ciphering.Security);
                Console.WriteLine("System title: " + GXCommon.ToHex(Client.Ciphering.SystemTitle, true));
                Console.WriteLine("Authentication key: " + GXCommon.ToHex(Client.Ciphering.AuthenticationKey, true));
                Console.WriteLine("Block cipher key " + GXCommon.ToHex(Client.Ciphering.BlockCipherKey, true));
                if (Client.Ciphering.DedicatedKey != null)
                    Console.WriteLine("Dedicated key: " + GXCommon.ToHex(Client.Ciphering.DedicatedKey, true));
            }
            await UpdateFrameCounterAsync(ct).ConfigureAwait(false);
            await InitializeOpticalHeadAsync(ct).ConfigureAwait(false);
            GXReplyData reply = new GXReplyData();
            await SNRMRequestAsync(ct).ConfigureAwait(false);
            if (!Client.PreEstablishedConnection)
            {
                foreach (byte[] it in Client.AARQRequest())
                {
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine("Send AARQ request", GXCommon.ToHex(it, true));
                    reply.Clear();
                    await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
                }
                if (Trace > TraceLevel.Info)
                    Console.WriteLine("Parsing AARE reply" + reply.ToString());
                Client.ParseAAREResponse(reply.Data);
                Console.WriteLine("Conformance: " + Client.NegotiatedConformance);
                reply.Clear();
                if (Client.Authentication > Authentication.Low)
                {
                    foreach (byte[] it in Client.GetApplicationAssociationRequest())
                    {
                        reply.Clear();
                        await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
                    }
                    Client.ParseApplicationAssociationResponse(reply.Data);
                }
                if (Trace > TraceLevel.Info)
                    Console.WriteLine("Parsing AARE reply succeeded.");
            }
        }

        public async Task ReadDLMSPacketAsync(byte[] data, GXReplyData reply, CancellationToken ct = default)
        {
            if (data == null && !reply.IsStreaming())
                return;

            GXReplyData notify = new GXReplyData();
            reply.Error = 0;
            object eop = (byte)0x7E;
            if (Client.InterfaceType != InterfaceType.HDLC &&
                Client.InterfaceType != InterfaceType.HdlcWithModeE)
            {
                eop = null;
            }
            int pos = 0;
            bool succeeded = false;
            GXByteBuffer rd = new GXByteBuffer();
            ReceiveParameters<byte[]> p = new ReceiveParameters<byte[]>()
            {
                Eop = eop,
                Count = Client.GetFrameSize(rd),
                AllData = true,
                WaitTime = WaitTime,
            };

            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                while (!succeeded && pos != 3)
                {
                    if (!reply.IsStreaming())
                    {
                        WriteTrace("TX:\t" + DateTime.Now.ToLongTimeString() + "\t" + GXCommon.ToHex(data, true));
                        p.Reply = null;
                        await SendAsync(data).ConfigureAwait(false);
                    }
                    succeeded = await _stream.ReceiveAsync(p, ct).ConfigureAwait(false);
                    if (!succeeded)
                    {
                        if (++pos >= RetryCount)
                            throw new Exception("Failed to receive reply from the device in given time.");
                        if (p.Eop == null)
                            p.Count = 1;
                        System.Diagnostics.Debug.WriteLine("Data send failed. Try to resend " + pos.ToString() + "/3");
                    }
                }
                rd = new GXByteBuffer(p.Reply);
                try
                {
                    pos = 0;
                    while (!Client.GetData(rd, reply, notify))
                    {
                        p.Reply = null;
                        if (notify.IsComplete && notify.Data.Data != null)
                        {
                            if (!notify.IsMoreData)
                            {
                                if (notify.PrimeDc != null)
                                {
                                    OnNotification?.Invoke(notify.PrimeDc);
                                    Console.WriteLine(notify.PrimeDc);
                                }
                                else
                                {
                                    string xml;
                                    GXDLMSTranslator t = new GXDLMSTranslator(TranslatorOutputType.SimpleXml);
                                    t.DataToXml(notify.Data, out xml);
                                    OnNotification?.Invoke(xml);
                                    Console.WriteLine(xml);
                                }
                                notify.Clear();
                                continue;
                            }
                        }
                        if (p.Eop == null)
                            p.Count = Client.GetFrameSize(rd);
                        while (!await _stream.ReceiveAsync(p, ct).ConfigureAwait(false))
                        {
                            if (++pos >= RetryCount)
                                throw new Exception("Failed to receive reply from the device in given time.");
                            p.Reply = null;
                            await SendAsync(data).ConfigureAwait(false);
                            System.Diagnostics.Debug.WriteLine("Data send failed. Try to resend " + pos.ToString() + "/3");
                        }
                        rd.Set(p.Reply);
                    }
                }
                catch (Exception)
                {
                    WriteTrace("RX:\t" + DateTime.Now.ToLongTimeString() + "\t" + rd);
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
            WriteTrace("RX:\t" + DateTime.Now.ToLongTimeString() + "\t" + rd);
            if (reply.Error != 0)
            {
                if (reply.Error == (short)ErrorCode.Rejected)
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                    await ReadDLMSPacketAsync(data, reply, ct).ConfigureAwait(false);
                }
                else
                {
                    throw new GXDLMSException(reply.Error);
                }
            }
        }

        public async Task<bool> ReadDataBlockAsync(byte[][] data, GXReplyData reply, CancellationToken ct = default)
        {
            if (data == null)
                return true;
            foreach (byte[] it in data)
            {
                reply.Clear();
                await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
            }
            return reply.Error == 0;
        }

        public async Task ReadDataBlockAsync(byte[] data, GXReplyData reply, CancellationToken ct = default)
        {
            await ReadDLMSPacketAsync(data, reply, ct).ConfigureAwait(false);
            while (reply.IsMoreData &&
                (Client.ConnectionState != ConnectionState.None ||
                Client.PreEstablishedConnection))
            {
                if (reply.IsStreaming())
                    data = null;
                else
                    data = Client.ReceiverReady(reply);
                await ReadDLMSPacketAsync(data, reply, ct).ConfigureAwait(false);
            }
        }

        public async Task<object> ReadAsync(GXDLMSObject it, int attributeIndex, CancellationToken ct = default)
        {
            if (Client.CanRead(it, attributeIndex))
            {
                GXReplyData reply = new GXReplyData();
                if (!await ReadDataBlockAsync(Client.Read(it, attributeIndex), reply, ct).ConfigureAwait(false))
                {
                    if (reply.Error != (short)ErrorCode.Rejected)
                        throw new GXDLMSException(reply.Error);
                    reply.Clear();
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                    if (!await ReadDataBlockAsync(Client.Read(it, attributeIndex), reply, ct).ConfigureAwait(false))
                        throw new GXDLMSException(reply.Error);
                }
                if (it.GetDataType(attributeIndex) == DataType.None)
                    it.SetDataType(attributeIndex, reply.DataType);
                return Client.UpdateValue(it, attributeIndex, reply.Value);
            }
            else
            {
                Console.WriteLine("Can't read " + it.ToString() + ". Not enought acccess rights.");
            }
            return null;
        }

        public async Task ReadListAsync(List<KeyValuePair<GXDLMSObject, int>> list, CancellationToken ct = default)
        {
            byte[][] data = Client.ReadList(list);
            GXReplyData reply = new GXReplyData();
            List<object> values = new List<object>();
            foreach (byte[] it in data)
            {
                await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
                if (!reply.IsMoreData)
                {
                    if (reply.Value is IEnumerable<object>)
                        values.AddRange((IEnumerable<object>)reply.Value);
                }
                reply.Clear();
            }
            if (values.Count != list.Count)
                throw new Exception("Invalid reply. Read items count do not match.");
            Client.UpdateValues(list, values);
        }

        public async Task WriteListAsync(List<KeyValuePair<GXDLMSObject, int>> list, CancellationToken ct = default)
        {
            byte[][] data = Client.WriteList(list);
            GXReplyData reply = new GXReplyData();
            foreach (byte[] it in data)
            {
                await ReadDataBlockAsync(it, reply, ct).ConfigureAwait(false);
                reply.Clear();
            }
        }

        public async Task WriteAsync(GXDLMSObject it, int attributeIndex, CancellationToken ct = default)
        {
            if (Client.CanWrite(it, attributeIndex))
            {
                GXReplyData reply = new GXReplyData();
                await ReadDataBlockAsync(Client.Write(it, attributeIndex), reply, ct).ConfigureAwait(false);
            }
        }

        public async Task MethodAsync(GXDLMSObject it, int attributeIndex, object value, DataType type, CancellationToken ct = default)
        {
            if (Client.CanInvoke(it, attributeIndex))
            {
                GXReplyData reply = new GXReplyData();
                await ReadDataBlockAsync(Client.Method(it, attributeIndex, value, type), reply, ct).ConfigureAwait(false);
            }
        }

        public async Task<object[]> ReadRowsByEntryAsync(GXDLMSProfileGeneric it, UInt32 index, UInt32 count, CancellationToken ct = default)
        {
            GXReplyData reply = new GXReplyData();
            await ReadDataBlockAsync(Client.ReadRowsByEntry(it, index, count), reply, ct).ConfigureAwait(false);
            return (object[])Client.UpdateValue(it, 2, reply.Value);
        }

        public async Task<object[]> ReadRowsByRangeAsync(GXDLMSProfileGeneric it, DateTime start, DateTime end, CancellationToken ct = default)
        {
            GXReplyData reply = new GXReplyData();
            await ReadDataBlockAsync(Client.ReadRowsByRange(it, start, end), reply, ct).ConfigureAwait(false);
            return (object[])Client.UpdateValue(it, 2, reply.Value);
        }

        public async Task ReadByAccessAsync(List<GXDLMSAccessItem> list, CancellationToken ct = default)
        {
            if (list.Count != 0)
            {
                GXReplyData reply = new GXReplyData();
                byte[][] data = Client.AccessRequest(DateTime.MinValue, list);
                await ReadDataBlockAsync(data, reply, ct).ConfigureAwait(false);
                Client.ParseAccessResponse(list, reply.Data);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_stream != null && Client != null)
            {
                try
                {
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine("Disconnecting from the meter.");
                    try
                    {
                        await ReleaseAsync().ConfigureAwait(false);
                    }
                    catch (Exception) { }
                    GXReplyData reply = new GXReplyData();
                    await ReadDLMSPacketAsync(Client.DisconnectRequest(), reply).ConfigureAwait(false);
                }
                catch { }
            }
        }

        public async Task ReleaseAsync()
        {
            if (_stream != null && Client != null)
            {
                try
                {
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine("Release from the meter.");
                    if (Client.InterfaceType == InterfaceType.WRAPPER ||
                        (Client.Ciphering.Security != (byte)Security.None &&
                        !Client.PreEstablishedConnection))
                    {
                        GXReplyData reply = new GXReplyData();
                        await ReadDataBlockAsync(Client.ReleaseRequest(), reply).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Release failed. " + ex.Message);
                }
            }
        }

        public async Task CloseAsync()
        {
            if (_stream != null && Client != null)
            {
                try
                {
                    if (Trace > TraceLevel.Info)
                        Console.WriteLine("Disconnecting from the meter.");
                    try
                    {
                        await ReleaseAsync().ConfigureAwait(false);
                    }
                    catch (Exception) { }
                    GXReplyData reply = new GXReplyData();
                    await ReadDLMSPacketAsync(Client.DisconnectRequest(), reply).ConfigureAwait(false);
                }
                catch { }
                await _stream.CloseAsync().ConfigureAwait(false);
            }
        }

        void WriteTrace(string line)
        {
            if (Trace > TraceLevel.Info)
                Console.WriteLine(line);
        }
    }
}
