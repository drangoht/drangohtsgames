using System.ComponentModel.DataAnnotations;

namespace DrangohtGames.Web;

/// <summary>Identité du site et liens affichés dans le pied de page.</summary>
public sealed class SiteOptions
{
    /// <summary>Section de configuration correspondante.</summary>
    public const string SectionName = "Site";

    /// <summary>Nom affiché dans l'en-tête et le titre des pages.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Name { get; init; } = "Drangoht Games";

    /// <summary>Page itch.io du créateur.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string ItchProfileUrl { get; init; } = "https://drangoht.itch.io/";

    /// <summary>Profil GitHub, facultatif.</summary>
    [Url]
    public string? GitHubUrl { get; init; }
}
