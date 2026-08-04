using System.Security.Cryptography;
using System.Text;

namespace Invoice.Automation;

internal static class DeterministicGuid
{
    public static Guid Create(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var namespaceBytes = namespaceId.ToByteArray();
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);
        var hash = SHA256.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16));
    }
}
