namespace Psxbox.RocProtocol
{
    public class ROCAddress
    {
        public byte Unit { get; set; }
        public byte Group { get; set; }

        public ROCAddress() { }
        public ROCAddress(byte unit, byte group)
        {
            Unit = unit;
            Group = group;
        }

        public byte[] GetAddress => new byte[] { Unit, Group };

        public override string ToString() => BitConverter.ToString(GetAddress);

        public override bool Equals(object? obj)
        {
            if (obj is not ROCAddress rocAddress) return false;
            return this.Unit == rocAddress.Unit && this.Group == rocAddress.Group;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}