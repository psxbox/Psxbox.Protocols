namespace Psxbox.RocProtocol
{
    public class RocDeviceSettings
    {
        public ROCAddress RocAddress { get; set; } = new ROCAddress(240, 240);
        public ROCAddress HostAddress { get; set; } = new ROCAddress(3, 1);
    }
}