using System.Globalization;
using DrangohtGames.Web;
using DrangohtGames.Web.Components;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.Editorial;
using DrangohtGames.Web.Games.ItchIo;
using DrangohtGames.Web.Games.Snapshots;
using DrangohtGames.Web.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration fortement typée, validée au démarrage ---------------------------------
// Une clé d'API absente doit empêcher le conteneur de démarrer, pas produire une page
// d'erreur à la première visite.
builder.Services.AddOptions<ItchIoOptions>()
    .Bind(builder.Configuration.GetSection(ItchIoOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SnapshotOptions>()
    .Bind(builder.Configuration.GetSection(SnapshotOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EditorialOptions>()
    .Bind(builder.Configuration.GetSection(EditorialOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SiteOptions>()
    .Bind(builder.Configuration.GetSection(SiteOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- Accès à itch.io ----------------------------------------------------------------------
builder.Services.AddHttpClient<IItchIoClient, ItchIoClient>((provider, client) =>
    {
        var options = provider.GetRequiredService<IOptions<ItchIoOptions>>().Value;

        client.BaseAddress = new Uri("https://itch.io/");
        client.Timeout = options.Timeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DrangohtGames/1.0 (+https://github.com/drangoht)");
    })
    // Reprise avec back-off et jitter, plus un disjoncteur : une lecture est idempotente,
    // la retenter est sans risque.
    .AddStandardResilienceHandler();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<GameCatalogSnapshotStore>();
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<EditorialOptions>>().Value;
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(EditorialCatalogFile));

    return EditorialCatalogFile.Load(options.FilePath, logger);
});
builder.Services.AddSingleton<IGameCatalog, GameCatalog>();

// --- Localisation -------------------------------------------------------------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = SupportedCultures.All
        .Select(culture => new CultureInfo(culture))
        .ToArray();

    options.DefaultRequestCulture = new RequestCulture(SupportedCultures.Default);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});

builder.Services.AddRazorComponents();
builder.Services.AddHealthChecks();

// Le site tourne derrière un reverse proxy : sans cela, les URL générées et les journaux
// portent l'adresse du conteneur au lieu de celle du visiteur.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "DENY";
    await next().ConfigureAwait(false);
});

// Ré-exécution réservée à la navigation. Sur un POST, elle rejouerait tout le pipeline —
// dont la validation antiforgery, qui échouerait une seconde fois en levant cette fois une
// exception : un refus net se transformerait en erreur serveur.
app.UseWhen(
    static context => HttpMethods.IsGet(context.Request.Method)
                      || HttpMethods.IsHead(context.Request.Method),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

// Sonde de vivacité : volontairement indépendante d'itch.io. Marquer le conteneur malsain
// parce qu'une API tierce est tombée le ferait redémarrer en boucle sans rien réparer.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapCultureEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Point d'entrée, rendu visible pour les tests d'intégration.</summary>
public partial class Program;
