# 5. Refit pour le transport HTTP vers itch.io

- **Statut** : accepté
- **Date** : 2026-09-07

## Contexte

`ItchIoClient` faisait deux choses : parler HTTP (construire la requête, poser l'en-tête
`Authorization`, désérialiser, vérifier le code de statut) et traduire la réponse d'itch.io
en `Game`. Le premier bloc est de la mécanique sans décision ; le second porte toutes les
règles du site.

Cette cohabitation a un coût concret : chaque test de traduction — tri, plateformes, prix,
slug — devait monter un `HttpClient`, un handler de test et des `JsonSerializerOptions`
pour atteindre trois lignes de logique métier.

Le projet n'a **qu'un seul** appel sortant, vers **un seul** endpoint.

## Décision

Le transport passe par **Refit** (`Refit.HttpClientFactory`).

- `IItchIoApi` déclare le contrat HTTP — la route, l'en-tête d'autorisation, la forme de la
  réponse — et rien de plus. C'est un *Humble Object* : aucune décision ne s'y prend.
- `ItchIoClient` consomme `IItchIoApi` et ne contient plus que la traduction. Il reste la
  couche anti-corruption : c'est toujours le seul type qui connaît le vocabulaire d'itch.io.
- `ItchIoRefit` porte le câblage (adresse de base, délai d'expiration, `User-Agent`,
  résilience) et les réglages de sérialisation. Le format de date non standard d'itch.io est
  une connaissance du contrat externe : elle n'a pas à remonter dans `Program.cs`, qui se
  contente désormais d'un `AddItchIoCatalog()`.

**Coût** : une dépendance NuGet supplémentaire, un générateur de source, et une interface de
plus. **Payé par** : la disparition de la construction manuelle des requêtes et de la
désérialisation, un composition root allégé de douze lignes, et un contrat HTTP lisible d'un
coup d'œil au lieu d'être dispersé dans une méthode.

### Le type d'exception de Refit ne sort pas du dossier

Refit signale les codes HTTP d'erreur par `ApiException`, qui **ne dérive pas** de
`HttpRequestException`. Laisser ce type remonter aurait deux effets, tous deux inacceptables :
faire fuiter le SDK hors de `Games/ItchIo/`, et surtout casser silencieusement le repli sur
instantané — `GameCatalog` guette `HttpRequestException`, et une panne d'itch.io serait
devenue une erreur 500 au lieu d'un catalogue servi depuis le disque.

`ItchIoClient` traduit donc `ApiException` en `HttpRequestException`, code de statut et
exception d'origine conservés. Le contrat de `IItchIoClient` est inchangé, et un test le
verrouille.

## Conséquences

- Les tests de `ItchIoClient` branchent le **vrai** client Refit sur un
  `StubHttpMessageHandler` : la route, l'en-tête et la sérialisation traversés en test sont
  exactement ceux de production. Aucun mock de `HttpClient`.
- `StubHttpMessageHandler` rattache désormais la requête à la réponse
  (`response.RequestMessage`), ce qu'un vrai pipeline HTTP fait toujours et dont Refit a
  besoin pour construire ses exceptions.
- Le composition root reste unique (ADR 0002) : `AddItchIoCatalog()` est une décomposition
  de `Program.cs`, appelée par lui et seulement par lui, pas un second point de câblage.
- `ItchIoClient` et `IItchIoApi` sont `internal` : rien hors de l'assembly n'a de raison de
  les instancier. Le port `IItchIoClient` reste public.
- La montée de version de Refit devient un point de vigilance : c'est ce qui traduit le
  contrat externe. Les tests de `ItchIoClientTests` jouent le rôle de *learning tests*.

## Alternatives écartées

- **Conserver `HttpClient` à la main** : fonctionnait, mais mélangeait mécanique et décision
  dans une même classe et rendait chaque test de traduction inutilement coûteux.
- **Un générateur de client depuis OpenAPI** : itch.io ne publie pas de spécification, et le
  contrat utilisé se limite à un endpoint.
