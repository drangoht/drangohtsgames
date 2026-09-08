namespace DrangohtGames.Web.Games.SelfHosted;

/// <summary>
/// Recense les builds Web présents sur le disque.
/// </summary>
/// <remarks>
/// Objet humble : il regarde un répertoire et délègue tout le reste à
/// <see cref="SelfHostedGames"/>, qui reste testable sans I/O. Le recensement a lieu une
/// fois au démarrage — le contenu de l'image ne change pas en cours d'exécution.
/// </remarks>
public static partial class SelfHostedGamesDirectory
{
    /// <summary>Nom du fichier qui atteste qu'un build est exploitable.</summary>
    private const string EntryPoint = "index.html";

    /// <summary>
    /// Retient les sous-répertoires qui portent un <c>index.html</c>, leur nom valant slug.
    /// </summary>
    /// <remarks>
    /// L'absence du répertoire n'est pas une erreur : c'est le cas nominal en développement,
    /// où aucun build n'est téléchargé. Le site fonctionne alors comme avant l'ADR 0007.
    /// </remarks>
    public static SelfHostedGames Load(string directoryPath, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directoryPath))
        {
            LogNoBuildDirectory(logger, directoryPath);
            return SelfHostedGames.None;
        }

        var slugs = Directory
            .EnumerateDirectories(directoryPath)
            .Where(build => File.Exists(Path.Combine(build, EntryPoint)))
            .Select(build => Path.GetFileName(build))
            .ToArray();

        LogBuildsFound(logger, slugs.Length, directoryPath);

        return SelfHostedGames.For(slugs);
    }

    [LoggerMessage(EventId = 5000, Level = LogLevel.Information,
        Message = "Aucun jeu auto-hébergé en {Path} : les fiches renverront vers itch.io.")]
    private static partial void LogNoBuildDirectory(ILogger logger, string path);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "{Count} jeux jouables sur le site, chargés depuis {Path}.")]
    private static partial void LogBuildsFound(ILogger logger, int count, string path);
}
