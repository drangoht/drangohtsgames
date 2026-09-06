using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrangohtGames.Web.Games.ItchIo;

/// <summary>
/// Lit les horodatages d'itch.io, transmis au format <c>yyyy-MM-dd HH:mm:ss</c>.
/// </summary>
/// <remarks>
/// Ce format n'est pas ISO 8601 (ni <c>T</c> séparateur, ni décalage horaire) : le lecteur
/// par défaut de <c>System.Text.Json</c> le rejette. itch.io publie ces dates en UTC.
/// </remarks>
internal sealed class ItchIoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Horodatage itch.io vide.");
        }

        if (DateTimeOffset.TryParseExact(
                value,
                Format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        // Repli : itch.io a déjà fait évoluer ce format par le passé.
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed))
        {
            return parsed;
        }

        throw new JsonException($"Horodatage itch.io illisible : « {value} ».");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }
}
