using System.Buffers.Binary;
using TheSingularityWorkshop.FSM_Serialization;

namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Binary envelope for a compiled MicroBundle assembly and its stable bundle identity.
/// </summary>
/// <remarks>
/// The payload format includes a magic signature, format version, bundle ID, byte length,
/// and the assembly bytes. It is a transport envelope, not a repository address.
/// </remarks>
public sealed class MicroBundleAssemblyPayload : IBinarySerializable
{
    private const uint FormatVersion = 1;
    private static readonly byte[] Magic = [0x46, 0x53, 0x4D, 0x42];

    /// <summary>Creates an envelope for a non-empty assembly payload.</summary>
    /// <param name="bundleId">Stable, non-zero MicroBundle ID.</param>
    /// <param name="assemblyBytes">Compiled assembly bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">The bundle ID is zero.</exception>
    /// <exception cref="ArgumentException">The assembly payload is empty.</exception>
    public MicroBundleAssemblyPayload(ulong bundleId, ReadOnlyMemory<byte> assemblyBytes)
    {
        if (bundleId == 0) throw new ArgumentOutOfRangeException(nameof(bundleId));
        if (assemblyBytes.IsEmpty) throw new ArgumentException("Assembly bytes are required.", nameof(assemblyBytes));
        BundleId = bundleId;
        AssemblyBytes = assemblyBytes.ToArray();
    }

    /// <summary>Gets the stable MicroBundle identity stored in the envelope.</summary>
    public ulong BundleId { get; private set; }

    /// <summary>Gets the compiled assembly bytes.</summary>
    public ReadOnlyMemory<byte> AssemblyBytes { get; private set; }

    /// <summary>Writes the envelope to a binary stream.</summary>
    /// <param name="stream">Destination stream.</param>
    public void Pack(IBinaryStream stream)
    {
        stream.Write(Magic);
        Span<byte> header = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(header[0..4], FormatVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(header[4..12], BundleId);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..16], checked((uint)AssemblyBytes.Length));
        stream.Write(header);
        stream.Write(AssemblyBytes.Span);
    }

    /// <summary>Reads and validates an envelope from a binary stream.</summary>
    /// <param name="stream">Source stream.</param>
    /// <exception cref="InvalidDataException">The signature, format version, identity, or payload boundary is invalid.</exception>
    /// <exception cref="EndOfStreamException">The stream ends before the envelope is complete.</exception>
    public void Unpack(IBinaryStream stream)
    {
        Span<byte> magic = stackalloc byte[4];
        ReadExactly(stream, magic);
        if (!magic.SequenceEqual(Magic)) throw new InvalidDataException("Invalid MicroBundle payload magic.");

        Span<byte> header = stackalloc byte[16];
        ReadExactly(stream, header);

        var version = BinaryPrimitives.ReadUInt32LittleEndian(header[0..4]);
        if (version != FormatVersion) throw new InvalidDataException($"Unsupported MicroBundle payload version: {version}.");

        var bundleId = BinaryPrimitives.ReadUInt64LittleEndian(header[4..12]);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header[12..16]);
        if (bundleId == 0) throw new InvalidDataException("Invalid MicroBundle ID.");

        var bytes = new byte[checked((int)length)];
        ReadExactly(stream, bytes);
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing bytes in MicroBundle payload.");

        BundleId = bundleId;
        AssemblyBytes = bytes;
    }

    /// <summary>Serializes this envelope to a new byte array.</summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryBinaryStream();
        Pack(stream);
        return stream.ToArray();
    }

    /// <summary>Deserializes and validates an envelope from bytes.</summary>
    /// <param name="bytes">Serialized envelope.</param>
    /// <returns>The decoded assembly payload.</returns>
    public static MicroBundleAssemblyPayload FromBytes(ReadOnlyMemory<byte> bytes)
    {
        using var stream = new MemoryBinaryStream(bytes.ToArray());
        var payload = new MicroBundleAssemblyPayload(1, new byte[] { 1 });
        payload.Unpack(stream);
        return payload;
    }

    private static void ReadExactly(IBinaryStream stream, Span<byte> buffer)
    {
        if (stream.Read(buffer) != buffer.Length) throw new EndOfStreamException();
    }
}
