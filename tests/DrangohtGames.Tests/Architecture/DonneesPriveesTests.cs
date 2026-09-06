using System.Reflection;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.ItchIo;
using Shouldly;

namespace DrangohtGames.Tests.Architecture;

/// <summary>
/// L'API itch.io renvoie des données de compte que le site ne doit jamais publier.
/// Un commentaire ne tient pas six mois ; ce test, oui.
/// </summary>
public sealed class DonneesPriveesTests
{
    private static readonly string[] TermesInterdits =
    [
        "earning",
        "revenue",
        "purchase",
        "download",
        "view",
        "sale",
    ];

    [Fact]
    public void Game_NePorteAucunCompteurPriveDItchIo()
    {
        var membresSuspects = typeof(Game)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(EstUnTermeInterdit)
            .ToArray();

        membresSuspects.ShouldBeEmpty(
            $"Game est le contrat des vues : {string.Join(", ", membresSuspects)} exposerait des données de compte.");
    }

    [Fact]
    public void LeDtoDeLApiNeDeserialiseAucunCompteurPrive()
    {
        // Ce qui n'est pas désérialisé ne peut pas être exposé par accident plus tard.
        var dto = typeof(ItchIoClient).Assembly
            .GetType("DrangohtGames.Web.Games.ItchIo.ItchIoGameDto", throwOnError: true)!;

        var membresSuspects = dto
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(EstUnTermeInterdit)
            .ToArray();

        membresSuspects.ShouldBeEmpty(
            $"Le DTO ne doit pas capter {string.Join(", ", membresSuspects)} depuis la réponse itch.io.");
    }

    [Fact]
    public void AucunTypePublicDuNamespaceGamesNExposeDeCompteurPrive()
    {
        var membresSuspects = typeof(Game).Assembly
            .GetExportedTypes()
            .Where(t => t.Namespace?.StartsWith("DrangohtGames.Web.Games", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Select(p => $"{t.Name}.{p.Name}"))
            .Where(nom => EstUnTermeInterdit(nom.Split('.')[1]))
            .ToArray();

        membresSuspects.ShouldBeEmpty(string.Join(", ", membresSuspects));
    }

    private static bool EstUnTermeInterdit(string nomDeMembre) =>
        TermesInterdits.Any(terme => nomDeMembre.Contains(terme, StringComparison.OrdinalIgnoreCase));
}
