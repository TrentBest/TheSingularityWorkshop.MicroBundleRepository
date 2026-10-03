using System.Buffers.Binary;
using TheSingularityWorkshop.FSM_Serialization;

namespace TheSingularityWorkshop.MicroBundleRepository.Core;

public sealed class MicroBundleAssemblyPayload : IBinarySerializable
{
    private const uint FormatVersion = 1;
    private static readonly byte[] Magic = [0x46, 0x53, 0x4D, 0x42];

    public MicroBundleAssemblyPayload(ulong bundleId, ReadOnlyMemory<byte> assemblyBytes)
    {
        if (bundleId == 0) throw new ArgumentOutOfRangeException(nameof(bundleId));
        if (assemblyBytes.IsEmpty) throw new ArgumentException("Assembly bytes are required.", nameof(assemblyBytes));
        BundleId = bundleId;
        AssemblyBytes = assemblyBytes.ToArray();
    }

    public ulong BundleId { get; private set; }
    public ReadOnlyMemory<byte> AssemblyBytes { get; private set; }

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

    public byte[] ToBytes()
    {
        using var stream = new MemoryBinaryStream();
        Pack(stream);
        return stream.ToArray();
    }

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
