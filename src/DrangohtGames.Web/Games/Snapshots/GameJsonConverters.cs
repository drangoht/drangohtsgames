using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrangohtGames.Web.Games.Snapshots;

/// <summary>
/// Sérialise un <see cref="GameSlug"/> comme une simple chaîne.
/// </summary>
/// <remarks>
/// <see cref="GameSlug"/> se construit par une fabrique, pas par ses propriétés :
/// sans ce convertisseur, il ne se relit pas.
/// </remarks>
internal sealed class GameSlugJsonConverter : JsonConverter<GameSlug>
{
    public override GameSlug Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("Slug de jeu absent dans l'instantané.");

        // Le slug est déjà normalisé à l'écriture ; l'identifiant de repli est sans objet ici.
        return GameSlug.FromItchUrl(new Uri($"https://itch.io/{value}"), gameId: 0);
    }

    public override void Write(Utf8JsonWriter writer, GameSlug value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.Value);
    }
}

/// <summary>Sérialise un <see cref="Price"/> sous la forme <c>{ "cents": 499, "currency": "EUR" }</c>.</summary>
internal sealed class PriceJsonConverter : JsonConverter<Price>
{
    public override Price Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var cents = 0;
        var currency = "USD";

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "cents":
                    cents = reader.GetInt32();
                    break;
                case "currency":
                    currency = reader.GetString() ?? currency;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return Price.FromCents(cents, currency);
    }

    public override void Write(Utf8JsonWriter writer, Price value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteNumber("cents", value.AmountInCents);
        writer.WriteString("currency", value.Currency);
        writer.WriteEndObject();
    }
}
