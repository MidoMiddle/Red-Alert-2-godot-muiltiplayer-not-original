using System;
using System.Security.Cryptography;
using System.Text;

namespace Rts.Data.Maps;

/// <summary>
/// SHA-256 of the canonical text of a map (docs/implementation/M7_maps.md, 4.10), lowercase hex. The lobby compares it so
/// every player plays the same map; a hand-edited file with comments or another order hashes like its canonical form.
/// Used outside the simulation tick only.
/// </summary>
public static class MapHasher
{
    public static string Hash(MapDefinition map) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RtsMapWriter.Write(map)))).ToLowerInvariant();
}
