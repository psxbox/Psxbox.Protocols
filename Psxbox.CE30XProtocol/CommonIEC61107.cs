using Psxbox.Streams;
using Psxbox.Utils;
using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Psxbox.CE30XProtocol
{
    public static class CommonIEC61107
    {
        public const byte SOH = 0x01;
        public const byte STX = 0x02;
        public const byte ETX = 0x03;
        public const byte BBR = 0x28; // (
        public const byte EBR = 0x29; // )
        public const string VALUES_REGEX_PATTERN = @"\((.*?)\)";

        /// <summary>
        /// { 0x0D, 0x0A, 0X03 }
        /// </summary>
        /// <value></value>
        public static readonly byte[] DEFAULT_END = [0x0D, 0x0A, ETX];

        public static byte[] GetCommand(CE30XCommand command, string? function, params string[] paramsArg)
        {
            // OPTIMIZED: ArrayBufferWriter bilan 3 o'rniga 1 marta allocation
            var cmdStr = command.ToString();
            var paramStr = paramsArg.Length > 0 ? string.Join(",", paramsArg) : null;

            // O'lchamni taxminiy hisoblash: SOH + cmd + STX + func + BBR + params + EBR + ETX + checksum
            int estimatedSize = 6 + cmdStr.Length + (function?.Length ?? 0) + (paramStr?.Length ?? 0);
            
            var writer = new ArrayBufferWriter<byte>(estimatedSize);
            var span = writer.GetSpan(estimatedSize);
            int position = 0;

            // SOH
            span[position++] = SOH;
            
            // Command
            int cmdBytes = Encoding.ASCII.GetBytes(cmdStr, span[position..]);
            position += cmdBytes;
            
            // STX
            span[position++] = STX;
            
            // Function
            if (function != null)
            {
                int funcBytes = Encoding.ASCII.GetBytes(function, span[position..]);
                position += funcBytes;
            }
            
            // BBR
            span[position++] = BBR;
            
            // Parameters
            if (paramStr != null)
            {
                int paramBytes = Encoding.ASCII.GetBytes(paramStr, span[position..]);
                position += paramBytes;
            }
            
            // EBR + ETX
            span[position++] = EBR;
            span[position++] = ETX;
            
            writer.Advance(position);
            
            // Checksum hisoblash va qo'shish
            var written = writer.WrittenSpan;
            byte checksum = Calculators.Cacl7BitSum(written[1..]);
            
            span = writer.GetSpan(1);
            span[0] = checksum;
            writer.Advance(1);

            return writer.WrittenSpan.ToArray();

            // ESKI KOD (3 allocation):
            // List<byte> result = new();
            // result.Add(SOH);
            // result.AddRange(Encoding.ASCII.GetBytes(cmdStr));
            // result.Add(STX);
            // if (function != null)
            //     result.AddRange(Encoding.ASCII.GetBytes(function));
            // result.Add(BBR);
            // if (paramsArg.Length > 0)
            //     result.AddRange(Encoding.ASCII.GetBytes(string.Join(",", paramsArg)));
            // result.Add(EBR);
            // result.Add(ETX);
            // result.Add(Calculators.Cacl7BitSum(result.ToArray()[1..]));  // allocation #2
            // return result.ToArray();  // allocation #3
        }

        public static bool CheckSumIsTrue(byte[] msg)
        {
            return msg[^1] == Calculators.Cacl7BitSum(msg[1..^1]);
        }

        public static async Task<bool> ConnectAndAuthorize(IStream stream, string id, string password = "")
        {
            var cmd = Encoding.ASCII.GetBytes($"/?{id}!\r\n");
            stream.Flush();
            await stream.WriteAsync(cmd);
            byte[] responce = await stream.ReadUntilAsync("\r\n");
            if (responce.Length == 0) throw new("Javob kelmadi");
            if (!responce.Contains((byte)'/'))
                throw new($"Kelgan javob noto'g'ri: {Encoding.ASCII.GetString(responce)}");

            cmd = Encoding.ASCII.GetBytes($"\u0006051\r\n");
            stream.Flush();
            await stream.WriteAsync(cmd);
            responce = await stream.ReadUntilAsync(ETX);
            _ = await stream.ReadAsync(); // read last checksum byte
            var responceStr = Encoding.ASCII.GetString(responce);

            if (!responceStr.Contains($"{(char)SOH}P0{(char)STX}"))
                throw new($"Kelgan javob noto'g'ri: {responceStr}");

            if (string.IsNullOrEmpty(password)) return true;

            cmd = GetCommand(CE30XCommand.P1, null, password);
            stream.Flush();
            await stream.WriteAsync(cmd);
            responce = await stream.ReadUntilAsync(0x06);
            if (responce.Length == 0) throw new("Kutilgan javob kelmadi!");

            return true;
        }

        public static async Task Disconnect(IStream stream)
        {
            var cmd = Encoding.ASCII.GetBytes($"{(char)SOH}B0{(char)ETX}u");
            stream.Flush();
            await stream.WriteAsync(cmd);
            await Task.Delay(1000);
            stream.Flush();
        }

        public static IEnumerable<string> ParseResponseValues(string input)
        {
            MatchCollection result = Regex.Matches(input, VALUES_REGEX_PATTERN);
            return result.Select(m => m.Groups[1].Value);
        }

        public static async Task<string> SendAndGet(IStream stream, CE30XCommand cmd, string func, byte[] waitingLastBytes, params string[] paramArg)
        {
            var sendData = GetCommand(cmd, func, paramArg);
            stream.Flush();
            await stream.WriteAsync(sendData);
            var result = await stream.ReadUntilAsync(waitingLastBytes);

            var checksum = await stream.ReadAsync();
            var startIndex = result.AsSpan().IndexOf(STX);
            if (startIndex == -1)
            {
                throw new Exception($"Kelgan javob noto'g'ri! So'rov: {BitConverter.ToString(sendData)} Javob: {BitConverter.ToString(result)}");
            }
            var calcChecksum = Calculators.Cacl7BitSum(result[(startIndex + 1)..]);

            if (calcChecksum != checksum)
                throw new($"Checksum noto'g'ri, kelgan: {checksum}, hisoblangan: {calcChecksum}");

            var resultStr = Encoding.ASCII.GetString(result);

            if (resultStr == "\u0002\u0003") return "";

            if (resultStr.Contains("ERR18")) return resultStr;

            if (!resultStr.Contains(func))
                throw new($"Kelgan javob noto'gri: {resultStr}");

            return resultStr;
        }
    }
}
