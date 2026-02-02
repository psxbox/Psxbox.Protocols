namespace Psxbox.Mercury;

public enum PowerType
{
    P = 0,
    Q = 1,
    S = 2
}

public static class PowerTypeExtensions
{
    public static byte ToByte(this PowerType powerType) => (byte)powerType;
}
