using System.Text;

namespace MachiVerse.SimulationCore.Primitives;

public static class DomainHash
{
    public static Hash256 Compute(string label, IMvDcborValue value)
    {
        if (!StableToken.TryParse(label, out _))
        {
            throw new ArgumentException("Domain hash label must be a StableToken.", nameof(label));
        }

        var labelBytes = Encoding.ASCII.GetBytes(label);
        var encoded = MvDcbor.Encode(value);
        var preimage = new byte[labelBytes.Length + 1 + encoded.Length];
        labelBytes.CopyTo(preimage, 0);
        preimage[labelBytes.Length] = 0;
        encoded.CopyTo(preimage, labelBytes.Length + 1);
        return Hash256.Sha256(preimage);
    }
}
