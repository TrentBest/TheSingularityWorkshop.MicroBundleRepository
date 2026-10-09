using System.Text.Json;
using TheSingularityWorkshop.Ontology;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>
/// Encodes optional semantic addresses in blob metadata without making them part
/// of the artifact's content or storage identity.
/// </summary>
internal static class SemanticAddressMetadata
{
    internal const string Key = "semantic-address-v1";

    internal static string Write(OntologyAddress address)
    {
        var payload = new Payload(
            address.OntologyId,
            address.OntologyVersion,
            address.Path,
            address.Index.IsScalar,
            address.Index.Coordinates.ToArray());
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    internal static OntologyAddress? Read(IDictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(Key, out var encoded))
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(Convert.FromBase64String(encoded))
                ?? throw new InvalidDataException("The semantic address metadata payload is empty.");

            if (payload.Coordinates is null || payload.Coordinates.Length == 0)
                throw new InvalidDataException("The semantic address metadata has no index coordinates.");

            OntologyIndex index;
            if (payload.IndexIsScalar)
            {
                if (payload.Coordinates.Length != 1)
                    throw new InvalidDataException("A scalar semantic index must contain exactly one coordinate.");

                index = new OntologyIndex(payload.Coordinates[0]);
            }
            else
            {
                if (payload.Coordinates.Length < 2)
                    throw new InvalidDataException("A multidimensional semantic index must contain at least two coordinates.");

                index = OntologyIndex.Create(payload.Coordinates);
            }

            return OntologyAddress.Create(
                payload.OntologyId,
                payload.OntologyVersion,
                payload.Path,
                index);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException or InvalidDataException)
        {
            throw new InvalidDataException("The stored semantic address metadata is invalid.", ex);
        }
    }

    private sealed record Payload(
        ulong OntologyId,
        string OntologyVersion,
        string Path,
        bool IndexIsScalar,
        ulong[] Coordinates);
}
