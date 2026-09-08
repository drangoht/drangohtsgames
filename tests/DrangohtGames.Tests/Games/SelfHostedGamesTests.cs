using DrangohtGames.Tests.Builders;
using DrangohtGames.Web.Games.SelfHosted;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class SelfHostedGamesTests
{
    [Fact]
    public void Apply_QuandLeJeuAUnBuild_LeMarqueAutoHeberge()
    {
        var selfHosted = SelfHostedGames.For(["snake-snack"]);

        var game = selfHosted.Apply(new GameBuilder().WithSlug("snake-snack").Build());

        game.IsSelfHosted.ShouldBeTrue();
    }

    [Fact]
    public void Apply_QuandLeJeuNAPasDeBuild_NeLeMarquePas()
    {
        var selfHosted = SelfHostedGames.For(["snake-snack"]);

        var game = selfHosted.Apply(new GameBuilder().WithSlug("money-survivor").Build());

        game.IsSelfHosted.ShouldBeFalse();
    }

    [Fact]
    public void Apply_IgnoreLaCasseDuNomDeDossier()
    {
        // Le slug d'un jeu est toujours en minuscules ; un dossier déposé à la main, non.
        var selfHosted = SelfHostedGames.For(["Snake-Snack"]);

        var game = selfHosted.Apply(new GameBuilder().WithSlug("snake-snack").Build());

        game.IsSelfHosted.ShouldBeTrue();
    }

    [Fact]
    public void None_NeMarqueAucunJeu()
    {
        var game = SelfHostedGames.None.Apply(new GameBuilder().WithSlug("snake-snack").Build());

        game.IsSelfHosted.ShouldBeFalse();
    }
}

public sealed class SelfHostedGamesDirectoryTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "drangohtgames-play", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_RetientLesDossiersQuiPortentIndexHtml()
    {
        CreateBuild("snake-snack");

        var selfHosted = SelfHostedGamesDirectory.Load(_directory, NullLogger.Instance);

        selfHosted.Apply(new GameBuilder().WithSlug("snake-snack").Build())
            .IsSelfHosted.ShouldBeTrue();
    }

    [Fact]
    public void Load_IgnoreUnDossierSansIndexHtml()
    {
        // Une archive mal formée ou à moitié décompressée ne doit pas produire un bouton
        // « Jouer » qui mène à une page blanche.
        Directory.CreateDirectory(Path.Combine(_directory, "snake-snack"));

        var selfHosted = SelfHostedGamesDirectory.Load(_directory, NullLogger.Instance);

        selfHosted.Apply(new GameBuilder().WithSlug("snake-snack").Build())
            .IsSelfHosted.ShouldBeFalse();
    }

    [Fact]
    public void Load_QuandLeRepertoireNexistePas_NeRetientAucunJeu()
    {
        // C'est le cas nominal en développement : aucun build n'est téléchargé localement.
        var selfHosted = SelfHostedGamesDirectory.Load(_directory, NullLogger.Instance);

        selfHosted.Apply(new GameBuilder().WithSlug("snake-snack").Build())
            .IsSelfHosted.ShouldBeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private void CreateBuild(string slug)
    {
        var buildDirectory = Path.Combine(_directory, slug);
        Directory.CreateDirectory(buildDirectory);
        File.WriteAllText(Path.Combine(buildDirectory, "index.html"), "<!DOCTYPE html>");
    }
}
