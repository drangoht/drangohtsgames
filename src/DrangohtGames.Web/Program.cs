using System.Globalization;
using DrangohtGames.Web;
using DrangohtGames.Web.Components;
using DrangohtGames.Web.Games;
using DrangohtGames.Web.Games.Editorial;
using DrangohtGames.Web.Games.ItchIo;
using DrangohtGames.Web.Games.SelfHosted;
using DrangohtGames.Web.Games.Snapshots;
using DrangohtGames.Web.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
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
// Le détail du transport (route, en-têtes, sérialisation, résilience) reste dans Games/ItchIo :
// le composition root n'a pas à connaître le format de date d'un fournisseur tiers.
builder.Services.AddItchIoCatalog();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<GameCatalogSnapshotStore>();
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<EditorialOptions>>().Value;
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(EditorialCatalogFile));

    return EditorialCatalogFile.Load(options.FilePath, logger);
});

// Les builds Web sont déposés dans l'image après le publish (ADR 0007). On recense une
// fois au démarrage ce qui s'y trouve réellement : le contenu de l'image ne bouge plus.
builder.Services.AddSingleton(provider =>
{
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SelfHostedGamesDirectory));

    return SelfHostedGamesDirectory.Load(PlayDirectory.Of(builder.Environment), logger);
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

// --- Jeux auto-hébergés (ADR 0007) --------------------------------------------------------
// Les builds sont déposés dans l'image après le publish : ils échappent au manifeste de
// `MapStaticAssets` et c'est le pipeline de fichiers statiques qui les sert. On règle donc
// ses options plutôt que d'empiler un second pipeline, dont l'ordre d'exécution ne serait
// garanti par rien — un `UseStaticFiles` dédié s'efface dès qu'un endpoint est sélectionné.
//
// La politique de cache est celle du pipeline : `Cache-Control: no-cache` avec un ETag,
// donc une revalidation par fichier qui se solde par un 304 — les mégaoctets du jeu ne
// repassent pas sur le réseau. C'est exactement ce que demande le template Unity, dont
// l'index.html ne doit jamais être servi depuis un cache.
builder.Services.Configure<StaticFileOptions>(options =>
{
    // Unity nomme ses ressources avec des extensions inconnues du serveur. Sans
    // correspondance déclarée, elles sont refusées en 404 : le jeu ne démarre pas, et rien
    // dans les journaux ne dit pourquoi.
    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".unityweb"] = "application/octet-stream";
    contentTypes.Mappings[".data"] = "application/octet-stream";
    contentTypes.Mappings[".wasm"] = "application/wasm";

    options.ContentTypeProvider = contentTypes;
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
    // SAMEORIGIN et non DENY : la page de jeu encadre le build servi par le site lui-même,
    // et DENY l'interdit y compris de même origine (ADR 0007).
    headers["X-Frame-Options"] = "SAMEORIGIN";
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

app.UseStaticFiles();
app.MapStaticAssets();
// Les pages ne déclarent que GET et POST : une requête HEAD y récolte un 404, alors que
// les services de supervision sondent avec cette méthode et signaleraient le site à terre.
// Kestrel se charge d'omettre le corps de la réponse.
app.MapRazorComponents<App>().Add(static endpoint =>
{
    var methodes = endpoint.Metadata.OfType<HttpMethodMetadata>().LastOrDefault();

    if (methodes is not null
        && methodes.HttpMethods.Contains(HttpMethods.Get)
        && !methodes.HttpMethods.Contains(HttpMethods.Head))
    {
        endpoint.Metadata.Add(new HttpMethodMetadata([.. methodes.HttpMethods, HttpMethods.Head]));
    }
});

// Sonde de vivacité : volontairement indépendante d'itch.io. Marquer le conteneur malsain
// parce qu'une API tierce est tombée le ferait redémarrer en boucle sans rien réparer.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapCultureEndpoints();

await app.RunAsync().ConfigureAwait(false);



/// <summary>Emplacement des builds Web auto-hébergés, sous la racine web.</summary>
internal static class PlayDirectory
{
    /// <summary>Segment d'URL et nom de répertoire qui portent les jeux.</summary>
    public const string Name = "play";

    /// <summary>
    /// Résout le répertoire des builds.
    /// </summary>
    /// <remarks>
    /// <c>WebRootPath</c> est nul tant que le répertoire n'existe pas — le cas des tests, qui
    /// ne publient aucun asset. On retombe alors sur le chemin conventionnel plutôt que de
    /// faire échouer le démarrage.
    /// </remarks>
    public static string Of(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return Path.Combine(
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            Name);
    }
}

/// <summary>Point d'entrée, rendu visible pour les tests d'intégration.</summary>
public partial class Program;
