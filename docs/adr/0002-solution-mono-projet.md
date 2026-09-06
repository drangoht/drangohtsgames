# 2. Solution mono-projet plutôt que découpage en quatre couches

- **Statut** : accepté
- **Date** : 2026-09-06

## Contexte

`.claude/rules/40-architecture.md` décrit un découpage `Domain` / `Application` /
`Infrastructure` / `Api`, assorti de projets de tests séparés, et impose d'écrire un ADR
en cas de dérogation. Le présent projet est une vitrine : elle liste les jeux publiés sur
un compte itch.io, les présente et renvoie vers itch.io pour le téléchargement.

Sa réalité mesurée :

- **une seule source de données** en lecture seule, sans écriture ni transaction ;
- **aucun invariant métier** : pas d'agrégat, pas de règle de gestion, pas de cohérence à
  maintenir — les seuls calculs sont du filtrage et de la mise en forme ;
- **aucune base de données**, donc ni ORM ni migrations ;
- **un seul déployable**, un seul conteneur.

## Décision

Un projet applicatif, `src/DrangohtGames.Web`, découpé **par feature** (`Games/`,
`Localization/`), et un projet de tests, `tests/DrangohtGames.Tests`.

La règle de dépendance est préservée à l'intérieur du projet par le sens des références :

- `Games/Game.cs`, `GameSlug`, `Price`, `LocalizedText`, `GameFilter` ne connaissent ni
  HTTP, ni système de fichiers, ni ASP.NET ;
- `Games/ItchIo/` est la couche anti-corruption : c'est le seul dossier qui parle le
  vocabulaire d'itch.io, et rien n'en ressort d'autre que des `Game` ;
- `IGameCatalog` et `IItchIoClient` sont déclarés **par leurs consommateurs**, pas à côté
  de leurs implémentations ;
- le composition root reste unique, dans `Program.cs`.

## Conséquences

**Ce qu'on gagne** : quatre `csproj` de moins à maintenir, un temps de compilation et une
image Docker plus légers, et aucune indirection à traverser pour lire une fonctionnalité de
bout en bout.

**Ce qu'on perd** : la règle de dépendance n'est plus vérifiable par le compilateur. Un
`using Microsoft.AspNetCore.Http` ajouté dans `Games/Game.cs` compilerait.

**Ce qui compense** : `tests/DrangohtGames.Tests/Architecture/` vérifie par réflexion les
invariants qui comptent réellement ici — au premier chef, qu'aucun type exposé aux vues ne
porte les compteurs privés du compte itch.io.

**Quand revenir sur cette décision** : à l'apparition d'une base de données, d'un second
déployable, ou de règles métier portant des invariants. Le découpage par feature rend alors
l'extraction en projets mécanique — chaque dossier part d'un bloc.
