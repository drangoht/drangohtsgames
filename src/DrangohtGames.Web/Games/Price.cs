using System.Globalization;

namespace DrangohtGames.Web.Games;

/// <summary>
/// Prix plancher d'un jeu (le « pay what you want » d'itch.io part de ce montant).
/// </summary>
/// <remarks>
/// Le montant est conservé en centimes, tel que l'API itch.io le transmet : passer par un
/// <see cref="decimal"/> dès la désérialisation ferait apparaître des arrondis là où la
/// source est exacte.
/// </remarks>
public readonly record struct Price
{
    private static readonly Dictionary<string, string> SymbolsByCurrency = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "$",
        ["EUR"] = "€",
        ["GBP"] = "£",
        ["JPY"] = "¥",
    };

    private Price(int amountInCents, string currency)
    {
        AmountInCents = amountInCents;
        Currency = currency;
    }

    /// <summary>Montant plancher en centimes.</summary>
    public int AmountInCents { get; }

    /// <summary>Code ISO 4217 de la devise du jeu.</summary>
    public string Currency { get; }

    /// <summary>Indique que le jeu est proposé sans montant minimum.</summary>
    public bool IsFree => AmountInCents == 0;

    /// <summary>Montant plancher exprimé dans l'unité principale de la devise.</summary>
    public decimal Amount => AmountInCents / 100m;

    /// <summary>Construit un prix à partir du montant en centimes renvoyé par itch.io.</summary>
    public static Price FromCents(int amountInCents, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountInCents);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new Price(amountInCents, currency.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Rend le montant avec le symbole de la devise <em>du jeu</em>, en respectant les
    /// séparateurs de <paramref name="formatProvider"/>.
    /// </summary>
    /// <remarks>
    /// Un <c>ToString("C")</c> classique appliquerait le symbole de la culture du visiteur :
    /// un jeu vendu en dollars s'afficherait en euros pour un visiteur français.
    /// </remarks>
    public string ToDisplayString(IFormatProvider formatProvider)
    {
        var amount = Amount.ToString("N2", formatProvider);

        return SymbolsByCurrency.TryGetValue(Currency, out var symbol)
            ? symbol + amount
            : $"{Currency} {amount}";
    }
}
