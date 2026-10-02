using System.Buffers.Binary;
using TheSingularityWorkshop.FSM_Serialization;

namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Deterministic binary envelope for a compiled MicroBundle assembly.
/// </summary>
/// <remarks>
/// The envelope is deliberately small and transport-oriented. It identifies the
/// MicroBundle and carries opaque assembly bytes; repository addressing and SHA-256
/// verification remain separate concerns.
/// </remarks>
public sealed class MicroBundleAssemblyPayload : IBinarySerializable
{
    private const uint FormatVersion = 1;
    private static readonly byte[] Magic = [0x46, 0x53, 0x4D, 0x42];

    /// <summary>
    /// Creates a payload from a MicroBundle ID and compiled assembly bytes.
    /// </summary>
    public MicroBundleAssemblyPayload(ulong bundleId, ReadOnlyMemory<byte> assemblyBytes)
    {
        Validate(bundleId, assemblyBytes);
        BundleId = bundleId;
        AssemblyBytes = assemblyBytes.ToArray();
    }

    private MicroBundleAssemblyPayload()
    {
    }

    /// <summary>Gets the MicroBundle identity carried by the payload.</summary>
    public ulong BundleId { get; private set; }

    /// <summary>Gets a read-only view of the compiled assembly bytes.</summary>
    public ReadOnlyMemory<byte> AssemblyBytes { get; private set; }

    /// <summary>Writes the deterministic payload representation to a binary stream.</summary>
    public void Pack(IBinaryStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(Magic);
        Span<byte> header = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(header[0..4], FormatVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(header[4..12], BundleId);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header[12..16],
            checked((uint)AssemblyBytes.Length));
        stream.Write(header);
        stream.Write(AssemblyBytes.Span);
    }

    /// <summary>Reads and validates one complete payload from a binary stream.</summary>
    public void Unpack(IBinaryStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> magic = stackalloc byte[4];
        ReadExactly(stream, magic);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Invalid MicroBundle payload magic.");

        Span<byte> header = stackalloc byte[16];
        ReadExactly(stream, header);

        var version = BinaryPrimitives.ReadUInt32LittleEndian(header[0..4]);
        if (version != FormatVersion)
            throw new InvalidDataException(
                $"Unsupported MicroBundle payload version: {version}.");

        var bundleId = BinaryPrimitives.ReadUInt64LittleEndian(header[4..12]);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header[12..16]);

        if (bundleId == 0)
            throw new InvalidDataException("Invalid MicroBundle ID.");

        if (length == 0)
            throw new InvalidDataException("MicroBundle assembly bytes are required.");

        if (length > int.MaxValue)
            throw new InvalidDataException("MicroBundle assembly payload is too large.");

        var bytes = new byte[(int)length];
        ReadExactly(stream, bytes);

        if (stream.Position != stream.Length)
            throw new InvalidDataException("Trailing bytes in MicroBundle payload.");

        BundleId = bundleId;
        AssemblyBytes = bytes;
    }

    /// <summary>Serializes this payload to a new byte array.</summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryBinaryStream();
        Pack(stream);
        return stream.ToArray();
    }

    /// <summary>Deserializes and validates one complete payload from bytes.</summary>
    public static MicroBundleAssemblyPayload FromBytes(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("Payload bytes are required.", nameof(bytes));

        using var stream = new MemoryBinaryStream(bytes);
        var payload = new MicroBundleAssemblyPayload();
        payload.Unpack(stream);
        return payload;
    }

    private static void Validate(ulong bundleId, ReadOnlyMemory<byte> assemblyBytes)
    {
        if (bundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(bundleId));

        if (assemblyBytes.IsEmpty)
            throw new ArgumentException(
                "Assembly bytes are required.",
                nameof(assemblyBytes));
    }

    private static void ReadExactly(IBinaryStream stream, Span<byte> buffer)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read <= 0)
                throw new EndOfStreamException();

            offset += read;
        }
    }
}
