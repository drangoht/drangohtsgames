using DrangohtGames.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DrangohtGames.Tests.Configuration;

/// <summary>
/// Le déploiement transmet toutes les clés du modèle, renseignées ou non. Une clé
/// facultative laissée vide ne doit pas empêcher le site de démarrer.
/// </summary>
public sealed class SiteOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GitHubUrl_QuandLaConfigurationEstVide_NEmpechePasLeDemarrage(string valeur)
    {
        var options = Construire(("Site:GitHubUrl", valeur));

        var demarrage = () => options.Value;

        demarrage.ShouldNotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GitHubUrl_QuandLaConfigurationEstVide_EstConsidereAbsent(string valeur)
    {
        var options = Construire(("Site:GitHubUrl", valeur));

        options.Value.GitHubUrl.ShouldBeNull();
    }

    [Fact]
    public void GitHubUrl_QuandLAdresseEstRenseignee_EstConservee()
    {
        var options = Construire(("Site:GitHubUrl", "https://github.com/drangoht"));

        options.Value.GitHubUrl.ShouldBe("https://github.com/drangoht");
    }

    [Fact]
    public void GitHubUrl_QuandLAdresseEstInvalide_FaitEchouerLeDemarrage()
    {
        var options = Construire(("Site:GitHubUrl", "pas-une-adresse"));

        var demarrage = () => options.Value;

        demarrage.ShouldThrow<OptionsValidationException>();
    }

    private static IOptions<SiteOptions> Construire(params (string Cle, string Valeur)[] reglages)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(reglages.Select(r => new KeyValuePair<string, string?>(r.Cle, r.Valeur)))
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<SiteOptions>()
            .Bind(configuration.GetSection(SiteOptions.SectionName))
            .ValidateDataAnnotations();

        return services.BuildServiceProvider().GetRequiredService<IOptions<SiteOptions>>();
    }
}
