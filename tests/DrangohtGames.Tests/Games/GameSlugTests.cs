using DrangohtGames.Web.Games;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class GameSlugTests
{
    [Theory]
    [InlineData("https://drangoht.itch.io/space-blaster", "space-blaster")]
    [InlineData("https://drangoht.itch.io/space-blaster/", "space-blaster")]
    [InlineData("http://drangoht.itch.io/Mon-Jeu", "mon-jeu")]
    public void FromItchUrl_QuandLUrlPorteUnChemin_UtiliseLeDernierSegmentEnMinuscules(
        string url,
        string attendu)
    {
        var slug = GameSlug.FromItchUrl(new Uri(url), gameId: 42);

        slug.Value.ShouldBe(attendu);
    }

    [Theory]
    [InlineData("https://drangoht.itch.io")]
    [InlineData("https://drangoht.itch.io/")]
    public void FromItchUrl_QuandLUrlNaPasDeChemin_RetombeSurLIdentifiantDuJeu(string url)
    {
        var slug = GameSlug.FromItchUrl(new Uri(url), gameId: 42);

        slug.Value.ShouldBe("42");
    }

    [Fact]
    public void FromItchUrl_QuandLUrlEstNulle_LeveArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => GameSlug.FromItchUrl(null!, gameId: 42));
    }

    [Fact]
    public void Equality_EstInsensibleALaCasse()
    {
        var minuscule = GameSlug.FromItchUrl(new Uri("https://drangoht.itch.io/jeu"), 1);
        var majuscule = GameSlug.FromItchUrl(new Uri("https://drangoht.itch.io/JEU"), 1);

        minuscule.ShouldBe(majuscule);
    }
}
