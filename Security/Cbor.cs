using System.Text;

namespace SaccoManagementSystem.Security;

/// <summary>Minimal CBOR reader – enough for WebAuthn attestation objects and COSE keys.</summary>
public static class Cbor
{
    public static object? Decode(byte[] data)
    {
        int pos = 0;
        return Read(data, ref pos);
    }

    public static object? Decode(byte[] data, ref int pos) => Read(data, ref pos);

    private static object? Read(byte[] d, ref int p)
    {
        if (p >= d.Length) throw new FormatException("CBOR truncated.");
        byte ib = d[p++];
        int major = ib >> 5;
        int info = ib & 0x1F;

        if (major == 7)
        {
            return info switch
            {
                20 => false,
                21 => true,
                22 or 23 => null,
                _ => throw new FormatException("Unsupported CBOR simple/float.")
            };
        }

        long arg = ReadArg(d, ref p, info);
        switch (major)
        {
            case 0: return arg;
            case 1: return -1 - arg;
            case 2:
            {
                var b = Slice(d, ref p, arg);
                return b;
            }
            case 3:
                return Encoding.UTF8.GetString(Slice(d, ref p, arg));
            case 4:
            {
                var list = new List<object?>();
                for (long i = 0; i < arg; i++) list.Add(Read(d, ref p));
                return list;
            }
            case 5:
            {
                var map = new Dictionary<object, object?>();
                for (long i = 0; i < arg; i++)
                {
                    var k = Read(d, ref p) ?? throw new FormatException("Null CBOR key.");
                    map[k] = Read(d, ref p);
                }
                return map;
            }
            default:
                throw new FormatException("Unsupported CBOR major type.");
        }
    }

    private static long ReadArg(byte[] d, ref int p, int info)
    {
        if (info < 24) return info;
        int n = info switch { 24 => 1, 25 => 2, 26 => 4, 27 => 8, _ => throw new FormatException("Indefinite CBOR not supported.") };
        if (p + n > d.Length) throw new FormatException("CBOR truncated.");
        long v = 0;
        for (int i = 0; i < n; i++) v = (v << 8) | d[p++];
        return v;
    }

    private static byte[] Slice(byte[] d, ref int p, long len)
    {
        if (len < 0 || p + len > d.Length) throw new FormatException("CBOR truncated.");
        var b = new byte[len];
        Array.Copy(d, p, b, 0, len);
        p += (int)len;
        return b;
    }

    public static long Int(object? o) => o is long l ? l : throw new FormatException("Expected integer.");
    public static byte[] Bytes(object? o) => o as byte[] ?? throw new FormatException("Expected bytes.");
    public static string Text(object? o) => o as string ?? throw new FormatException("Expected text.");
    public static Dictionary<object, object?> Map(object? o) => o as Dictionary<object, object?> ?? throw new FormatException("Expected map.");
}
