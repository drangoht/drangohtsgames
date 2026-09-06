# CLAUDE.md

> Ce fichier est chargé **à chaque session**. Il reste court volontairement.
> Le détail vit dans `.claude/rules/` (référence) et `.claude/skills/` (procédures chargées à la demande).

## Projet

- **Nom** : `DrangohtGames`
- **Contexte métier** : site vitrine listant les jeux publiés sur <https://drangoht.itch.io/>.
  Les données viennent de l'API itch.io au runtime, complétées par un fichier éditorial
  versionné. Bilingue FR/EN, déployé en conteneur Docker.
- **Solution** : `src/DrangohtGames.slnx`
- **Cible** : .NET `10.0`

## Commandes

```bash
dotnet build src/DrangohtGames.slnx -warnaserror
dotnet test  --solution src/DrangohtGames.slnx
dotnet format src/DrangohtGames.slnx --verify-no-changes               # avant tout commit

# Boucle rapide : tout sauf les tests d'intégration
dotnet test --solution src/DrangohtGames.slnx -- --filter-not-trait "Category=Integration"

# Exécution locale — la clé d'API est un secret, jamais dans appsettings.json
cd src/DrangohtGames.Web
dotnet user-secrets set "ItchIo:ApiKey" "<cle>"    # première fois seulement
dotnet run

# Conteneur
docker build -t drangohtgames:local .
docker compose up -d          # lit .env (voir .env.example)
```

## Repères dans le code

| Où | Quoi |
|---|---|
| `Games/` | modèle (`Game`, `GameSlug`, `Price`, `LocalizedText`), port `IGameCatalog`, filtres |
| `Games/ItchIo/` | **seul** endroit qui parle le vocabulaire itch.io — couche anti-corruption |
| `Games/Editorial/` | fusion avec `data/games.json` (tags, descriptions, captures, traductions) |
| `Games/Snapshots/` | instantané de repli quand l'API est indisponible |
| `Resources/SharedResources*.resx` | traductions FR/EN — les deux fichiers portent les mêmes clés |
| `docs/adr/` | décisions structurantes, dont la dérogation mono-projet (ADR 0002) |

## Règles non négociables

1. **Aucun code de production sans test rouge préalable.** Cycle red → green → refactor.
2. **YAGNI > SOLID > patterns.** On n'abstrait pas sur une hypothèse. Règle de trois : la 1re occurrence on écrit, la 2e on duplique, la 3e on factorise.
3. **Ne jamais nommer un design pattern comme justification.** On décrit le problème, on cherche l'idiome .NET natif, on ne sort le pattern GoF qu'après. → skill `choix-pattern`.
4. **La règle de dépendance est absolue.** Le modèle du dossier `Games/` ne connaît ni HTTP, ni disque, ni ASP.NET. Une infraction = un test d'architecture qui casse, pas une discussion.
5. **Une interface appartient à son consommateur**, pas à son implémentation (DIP au sens de Robert C. Martin).
6. **Nullable + warnings-as-errors + analyzers.** Un warning est une erreur. Pas de `!` sans commentaire justifiant l'invariant.
7. **`CancellationToken` propagé partout.** Pas de `.Result`, pas de `.Wait()`, pas de `async void`.
8. **Refactoring et changement fonctionnel = deux commits.** Jamais mélangés.
9. **Ne pas exposer une entité de domaine dans un contrat public** (API, message, DTO de sortie).
10. **Si une règle ci-dessus doit être violée, l'écrire dans un ADR** (`docs/adr/`, skill `adr`), pas dans un commentaire.

## Spécifique à ce projet

- **Aucun compteur privé d'itch.io ne sort du dossier `Games/ItchIo/`.** `earnings`,
  `purchases_count`, `downloads_count`, `views_count` ne sont même pas désérialisés.
  `tests/…/Architecture/DonneesPriveesTests.cs` le vérifie — ne pas le contourner.
- **itch.io signale ses erreurs dans un corps `200 OK`** (`{"errors":[…]}`). Ne jamais
  déduire d'un code HTTP 200 que la réponse est exploitable.
- **Toute chaîne affichée passe par `IStringLocalizer<SharedResources>`.** Un texte en dur
  dans un composant casse la moitié du site.
- **Ajouter une clé de traduction, c'est l'ajouter dans les deux `.resx`.**
- Le rendu est **Blazor SSR statique** : pas d'interactivité côté client, les formulaires
  sont de vrais `<form>` HTML (GET pour les filtres, POST + antiforgery pour la langue).

## Réflexes attendus

| Situation | Faire |
|---|---|
| Je vais créer une interface / une abstraction | skill `choix-pattern` |
| Je vais implémenter un comportement | skill `tdd` |
| Je viens de modifier du code | skill `revue-clean-code` |
| Le code sent mauvais mais marche | skill `refactoring` |
| Choix structurant / arbitrage | skill `adr` |
| Feature complète | skill `nouvelle-feature` |

## Attitude

- Si une demande est ambiguë ou si l'exigence sent le sur-design, **le dire avant d'écrire du code**.
- Proposer la solution la plus simple qui passe les tests, puis mentionner l'évolution possible — sans l'implémenter.
- Pas de commentaire qui paraphrase le code. Le commentaire explique le **pourquoi**, jamais le **quoi**.
- Ne jamais désactiver un test ou un analyzer pour faire passer la CI.

## Référence détaillée (à charger si besoin)

@.claude/rules/00-principes.md

Les autres fichiers de `.claude/rules/` ne sont **pas** importés automatiquement (coût de contexte).
Les lire à la demande : `10-solid.md`, `20-clean-code.md`, `30-design-patterns.md`,
`40-architecture.md`, `50-tests.md`, `60-csharp.md`, `70-async-perf.md`.
