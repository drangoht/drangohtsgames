namespace DrangohtGames.Web.Games;

/// <summary>Plateformes sur lesquelles un jeu est distribué.</summary>
[Flags]
public enum GamePlatforms
{
    /// <summary>Aucune plateforme téléchargeable déclarée (jeu jouable en navigateur, ou page vitrine).</summary>
    None = 0,

    /// <summary>Windows.</summary>
    Windows = 1,

    /// <summary>Linux.</summary>
    Linux = 1 << 1,

    /// <summary>macOS.</summary>
    MacOs = 1 << 2,

    /// <summary>Android.</summary>
    Android = 1 << 3,
}
