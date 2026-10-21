using System;
using System.Security.Cryptography;
using System.Text;

namespace Dvx.Core.Identity;

/// <summary>Name-based UUID version 5 (RFC 4122 section 4.3).</summary>
public static class Uuid5
{
    public static Guid Create(Guid ns, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        byte[] nsBytes = ns.ToByteArray();
        SwapToNetworkOrder(nsBytes);

        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        byte[] input = new byte[nsBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(nsBytes, 0, input, 0, nsBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, input, nsBytes.Length, nameBytes.Length);

        byte[] hash = SHA1.HashData(input);
        byte[] g = new byte[16];
        Buffer.BlockCopy(hash, 0, g, 0, 16);
        g[6] = (byte)((g[6] & 0x0F) | 0x50); // version 5
        g[8] = (byte)((g[8] & 0x3F) | 0x80); // RFC 4122 variant
        SwapToNetworkOrder(g); // back to .NET layout (the swap is its own inverse)
        return new Guid(g);
    }

    // .NET stores the first three GUID fields little-endian; swapping converts in both directions.
    private static void SwapToNetworkOrder(byte[] b)
    {
        (b[0], b[3]) = (b[3], b[0]);
        (b[1], b[2]) = (b[2], b[1]);
        (b[4], b[5]) = (b[5], b[4]);
        (b[6], b[7]) = (b[7], b[6]);
    }
}
