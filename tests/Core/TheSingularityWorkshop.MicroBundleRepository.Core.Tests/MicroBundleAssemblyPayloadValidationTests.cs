using System.Buffers.Binary;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleAssemblyPayloadValidationTests
{
    [Fact]
    public void Constructor_RejectsZeroBundleId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MicroBundleAssemblyPayload(0, [1]));
    }

    [Fact]
    public void Constructor_RejectsEmptyAssembly()
    {
        Assert.Throws<ArgumentException>(
            () => new MicroBundleAssemblyPayload(7001, ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void FromBytes_RejectsEmptyPayload()
    {
        Assert.Throws<ArgumentException>(
            () => MicroBundleAssemblyPayload.FromBytes(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void FromBytes_RejectsTrailingBytes()
    {
        var payload = new MicroBundleAssemblyPayload(7001, new byte[] { 1, 2, 3 });
        var bytes = payload.ToBytes().Concat(new byte[] { 0xFF }).ToArray();

        Assert.Throws<InvalidDataException>(
            () => MicroBundleAssemblyPayload.FromBytes(bytes));
    }

    [Fact]
    public void FromBytes_RejectsUnsupportedFormatVersion()
    {
        var payload = new MicroBundleAssemblyPayload(7001, new byte[] { 1 });
        var bytes = payload.ToBytes();

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 99);

        Assert.Throws<InvalidDataException>(
            () => MicroBundleAssemblyPayload.FromBytes(bytes));
    }

    [Fact]
    public void FromBytes_RejectsZeroBundleId()
    {
        var payload = new MicroBundleAssemblyPayload(7001, new byte[] { 1 });
        var bytes = payload.ToBytes();

        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), 0);

        Assert.Throws<InvalidDataException>(
            () => MicroBundleAssemblyPayload.FromBytes(bytes));
    }
}
