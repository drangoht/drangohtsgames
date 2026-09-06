using System.Globalization;
using DrangohtGames.Web.Games;
using Shouldly;

namespace DrangohtGames.Tests.Games;

public sealed class PriceTests
{
    [Fact]
    public void FromCents_QuandLeMontantEstNul_EstGratuit()
    {
        var price = Price.FromCents(0, "USD");

        price.IsFree.ShouldBeTrue();
        price.Amount.ShouldBe(0m);
    }

    [Fact]
    public void FromCents_QuandLeMontantEstPositif_ConvertitLesCentsEnUnites()
    {
        var price = Price.FromCents(499, "USD");

        price.IsFree.ShouldBeFalse();
        price.Amount.ShouldBe(4.99m);
    }

    [Fact]
    public void FromCents_QuandLeMontantEstNegatif_LeveArgumentOutOfRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Price.FromCents(-1, "USD"));
    }

    [Fact]
    public void FromCents_QuandLaDeviseEstVide_LeveArgumentException()
    {
        Should.Throw<ArgumentException>(() => Price.FromCents(499, "  "));
    }

    [Theory]
    [InlineData("USD", "$4.99")]
    [InlineData("EUR", "€4.99")]
    [InlineData("GBP", "£4.99")]
    [InlineData("CAD", "CAD 4.99")]
    public void ToDisplayString_UtiliseLeSymboleDeLaDeviseDuJeuPasCelleDeLaCulture(
        string currency,
        string attendu)
    {
        var price = Price.FromCents(499, currency);

        price.ToDisplayString(CultureInfo.InvariantCulture).ShouldBe(attendu);
    }

    [Fact]
    public void ToDisplayString_SuitLesSeparateursDeLaCultureDeLUtilisateur()
    {
        var price = Price.FromCents(129950, "EUR");

        var affichage = price.ToDisplayString(CultureInfo.GetCultureInfo("fr-FR"));

        // Le symbole vient de la devise du jeu, la virgule décimale de la culture du visiteur.
        affichage.ShouldStartWith("€");
        affichage.ShouldEndWith("299,50");
    }
}
