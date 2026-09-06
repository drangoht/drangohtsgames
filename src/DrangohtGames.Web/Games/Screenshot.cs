namespace DrangohtGames.Web.Games;

/// <summary>Capture d'écran d'un jeu, renseignée par les métadonnées locales.</summary>
/// <param name="Url">Adresse de l'image.</param>
/// <param name="Caption">Texte alternatif, obligatoire pour l'accessibilité.</param>
public sealed record Screenshot(Uri Url, LocalizedText Caption);
