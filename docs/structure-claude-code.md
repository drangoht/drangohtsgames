# Structure Claude Code — développement .NET senior

Base réutilisable : `CLAUDE.md`, règles, skills, sous-agents, orientés Clean Code, SOLID,
YAGNI, patterns GoF appliqués à .NET.

## Installation

Copier à la racine du dépôt .NET :

```
CLAUDE.md
.claude/
docs/adr/
```

Puis remplir la section **Projet** de `CLAUDE.md` et adapter les commandes.
Redémarrer Claude Code si `.claude/agents/` ou `.claude/skills/` n'existaient pas
au démarrage de la session.

## Arborescence

```
CLAUDE.md                        chargé à CHAQUE session — garder court
.claude/
  settings.json                  permissions + hook dotnet format
  rules/                         référence, lue à la demande
    00-principes.md              hiérarchie YAGNI > SOLID > patterns   (importé)
    10-solid.md                  SOLID + symptômes C# + contre-indications
    20-clean-code.md             nommage, fonctions, erreurs, commentaires
    30-design-patterns.md        patterns GoF ↔ idiomes .NET, anti-patterns
    40-architecture.md           règle de dépendance, DDD tactique, messagerie
    50-tests.md                  TDD, FIRST, doublures, intégration
    60-csharp.md                 conventions, nullable, analyzers, logging
    70-async-perf.md             async, EF Core, mesure, résilience
  skills/                        procédures — chargées à la demande, aussi en /slash
    choix-pattern/               filtre avant toute abstraction
    tdd/                         boucle red-green-refactor
    revue-clean-code/            rapport de revue priorisé
    refactoring/                 catalogue smell → refactoring
    adr/                         rédaction d'ADR
    nouvelle-feature/            workflow bout en bout
  agents/                        sous-agents, contexte isolé
    architecte.md                conçoit, n'implémente pas
    dev-tdd.md                   implémente en TDD strict
    revieweur.md                 lecture seule, mémoire projet
    refactorer.md                transforme sans changer le comportement
    coach-solid.md               exercices + correction, mémoire utilisateur
docs/adr/
```

## Le point important : le budget de contexte

`CLAUDE.md` et tout ce qu'il importe avec `@` sont chargés **à chaque session**, dans
chaque sous-agent. Y mettre 2 000 lignes de règles dégrade toutes les réponses.

D'où la répartition :

| Type | Chargement | Contenu |
|---|---|---|
| `CLAUDE.md` | toujours | ~100 lignes : commandes + règles non négociables + aiguillage |
| `rules/00-principes.md` | toujours (import `@`) | la hiérarchie de décision, courte |
| autres `rules/*.md` | à la demande | référence détaillée |
| `skills/*/SKILL.md` | quand la description matche, ou en `/slash` | procédures |

Pour importer une règle en permanence, ajouter `@.claude/rules/50-tests.md` dans
`CLAUDE.md` — en connaissant le coût.

## Usage

```
/choix-pattern        avant d'introduire une abstraction
/tdd                  implémenter un comportement
/revue-clean-code     relire le diff
/refactoring          nettoyer sans changer le comportement
/adr                  consigner une décision
/nouvelle-feature     workflow complet

@architecte  @dev-tdd  @revieweur  @refactorer  @coach-solid
```

Les skills se déclenchent aussi automatiquement : leur champ `description` indique à
Claude quand les charger.

## À adapter

- **`CLAUDE.md`** — nom du projet, commandes, cible .NET. Retirer les règles qui ne
  correspondent pas à votre contexte : une règle qu'on n'applique pas décrédibilise
  les autres.
- **`40-architecture.md`** — le découpage 4 projets n'est pas un dogme. Pour un service
  modeste, un projet + dossiers par feature suffit.
- **`settings.json`** — le hook `dotnet format` s'exécute après chaque édition de `.cs`.
  Le retirer s'il ralentit trop.
- **`revieweur` et `coach-solid`** ont `memory:` activé : ils accumulent les défauts
  récurrents entre sessions. `project` est versionné, `user` non.

## Références

- Robert C. Martin — *Clean Code*, *Clean Architecture*, *The Clean Coder*
- Martin Fowler — *Refactoring* (catalogue smell → transformation)
- Design patterns : <https://refactoring.guru/fr/design-patterns>
- Doc Claude Code : <https://code.claude.com/docs/en/sub-agents>
