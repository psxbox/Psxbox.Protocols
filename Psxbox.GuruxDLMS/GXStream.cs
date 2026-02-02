using Gurux.Common;
using Psxbox.Streams;
using System.Diagnostics;

namespace Psxbox.GuruxDLMS
{
    public class GXStream : IGXMedia, IDisposable
    {
        private readonly IStream stream;
        private readonly bool disposeStream;
        private bool disposedValue;

        public GXStream(IStream stream, bool disposeStream = true)
        {
            this.stream = stream;
            this.disposeStream = disposeStream;
        }


        public string Name => stream.Name;

        public TraceLevel Trace { get; set; }

        public bool IsOpen => stream.IsConnected;

        public string MediaType => "Psxbox.Stream";

        public bool Enabled { get; }

        public string Settings { get; set; }

        public object Synchronous => new object();

        public bool IsSynchronous => false;

        public ulong BytesSent { get; private set; }

        public ulong BytesReceived { get; private set; }

        public object Eop { get; set; }
        int IGXMedia.ConfigurableSettings { get; set; }
        object IGXMedia.Tag { get; set; }
        IGXMediaContainer IGXMedia.MediaContainer { get; set; }

        public object SyncRoot => null;

        public event ReceivedEventHandler OnReceived;
        public event Gurux.Common.ErrorEventHandler OnError;
        public event MediaStateChangeEventHandler OnMediaStateChange;
        public event ClientConnectedEventHandler OnClientConnected;
        public event ClientDisconnectedEventHandler OnClientDisconnected;
        public event TraceEventHandler OnTrace;

        public void Close()
        {
            stream.Close();
        }

        void IGXMedia.Copy(object target)
        {
            throw new NotImplementedException();
        }

        public void Open()
        {
            BytesReceived = 0;
            stream.Connect();
        }

        public bool Receive<T>(ReceiveParameters<T> args)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("The media is not open.");
            }

            if (args.Eop == null && args.Count == 0 && !args.AllData)
            {
                throw new ArgumentException("Either Count or Eop must be set.");
            }

            int nSize = 0;
            byte[] terminator = null;
            if (args.Eop != null)
            {
                if (args.Eop is Array)
                {
                    Array arr = args.Eop as Array;
                    terminator = GXCommon.GetAsByteArray(arr.GetValue(0));
                }
                else
                {
                    terminator = GXCommon.GetAsByteArray(args.Eop);
                }
                nSize = terminator.Length;
            }

            bool retValue = true;

            if (!args.AllData)
            {
                retValue = false;
            }

            _ = stream.ReadUntil(terminator);

            List<byte> dataList = [.. terminator];

            BytesReceived += 1;

            var readed = stream.ReadUntil(terminator);

            AddReadedToDataList(dataList, readed);

            // ✅ Cheksiz tsikldan himoya: maksimal 1000 marta o'qish
            int maxReadCount = 1000;
            int readCount = 0;
            
            while (stream.Available && readCount < maxReadCount)
            {
                readed = stream.ReadUntil(terminator);
                AddReadedToDataList(dataList, readed);
                readCount++;
                
                // ✅ Agar readed bo'sh bo'lsa, cheksiz tsikldan chiqish
                if (readed == null || readed.Length == 0)
                {
                    break;
                }
            }

            args.Count = 0;

            var data = Activator.CreateInstance(typeof(T), dataList.Count);

            Array.Copy(dataList.ToArray(), 0, (Array)data, 0, dataList.Count);

            if (args.Reply == null)
            {
                args.Reply = (T)data;
            }
            else if (args.Reply is Array)
            {
                Array oldArray = args.Reply as Array;
                if (data is not Array newArray)
                {
                    throw new ArgumentException("Data is not an array.");
                }
                int len = oldArray.Length + newArray.Length;
                Array arr = (Array)Activator.CreateInstance(typeof(T), len);
                //Copy old values.
                Array.Copy(args.Reply as Array, arr, oldArray.Length);
                //Copy new values.
                Array.Copy(newArray, 0, arr, oldArray.Length, newArray.Length);
                object tmp2 = arr;
                args.Reply = (T)tmp2;
            }
            else if (args.Reply is string)
            {
                string str = args.Reply as string;
                str += (string)data;
                data = str;
                args.Reply = (T)data;
            }

            return retValue;
        }

        private void AddReadedToDataList(List<byte> dataList, byte[] readed)
        {
            BytesReceived += (ulong)readed.LongLength;

            foreach (var item in readed)
            {
                dataList.Add(item);
            }
        }

        public void ResetByteCounters()
        {
            BytesSent = 0;
            BytesReceived = 0;
        }

        public void ResetSynchronousBuffer()
        {
            stream.Flush();
        }

        public void Send(object data)
        {
            (this as IGXMedia).Send(data, null);
        }

        void IGXMedia.Send(object data, string receiver)
        {
            // stream.Flush();

            byte[] tmp = (byte[])data;
            if (tmp != null)
            {
                stream.Write(tmp);
                BytesSent += (ulong)tmp.Length;
                Thread.Yield();
                Thread.Sleep(200);
            }
        }

        public void Validate()
        {
            return;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (disposeStream)
                    {
                        _ = stream.DisposeAsync();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
