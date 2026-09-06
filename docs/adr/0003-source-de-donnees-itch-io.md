# 3. API itch.io au runtime, complétée par un fichier éditorial versionné

- **Statut** : accepté
- **Date** : 2026-09-06

## Contexte

Le site doit lister les jeux publiés sur `drangoht.itch.io` sans qu'il faille redéployer à
chaque nouvelle sortie.

Vérification faite sur le contrat réel de `GET /api/1/KEY/my-games`
([documentation](https://github.com/itchio/itch.io/blob/master/docs/api/serverside.md)) :

| Ce que l'API fournit | Ce qu'elle ne fournit pas |
|---|---|
| `id`, `title`, `url`, `cover_url`, `short_text` | tags |
| `type`, `published`, `published_at`, `created_at` | description longue |
| `min_price`, `p_windows`, `p_linux`, `p_osx`, `p_android` | captures d'écran |
| | traductions |

Trois contraintes s'ajoutent :

1. **Aucun endpoint public** : une clé personnelle (scope `profile:games`) est obligatoire.
2. La réponse contient des **données privées de compte** : `earnings`, `purchases_count`,
   `downloads_count`, `views_count`.
3. Une **clé invalide ne produit pas d'erreur HTTP** : itch.io répond `200 OK` avec
   `{"errors":["invalid key"]}` (comportement constaté, pas documenté).

## Décision

**L'API est la source de vérité** pour ce qu'elle sait : existence, titre, URL, couverture,
accroche, plateformes, prix, date de publication.

**Un fichier `data/games.json`, versionné avec le code**, porte le reste : tags, moteur,
description longue, captures et traductions françaises. Il est indexé par slug — le dernier
segment de l'URL itch.io, donc l'identifiant qu'un humain reconnaît en éditant le fichier.

La fusion suit trois règles :

- une entrée absente n'est pas une erreur : le jeu s'affiche avec les seules données de l'API ;
- une entrée partielle **complète** sans jamais **écraser** — une traduction française
  s'ajoute à l'accroche anglaise d'itch.io au lieu de la remplacer ;
- les slugs orphelins (fautes de frappe) sont signalés, pas ignorés silencieusement.

L'enrichissement est appliqué **en sortie du catalogue**, après le cache et après la lecture
de l'instantané : corriger une description et redéployer prend effet immédiatement, même
pendant une panne d'itch.io.

## Conséquences

- Les filtres portent sur les tags et le moteur, donc sur des données **saisies à la main**.
  Un jeu sans entrée éditoriale n'est atteignable que par la recherche et le filtre de
  plateforme. C'est le prix de l'absence de tags dans l'API.
- Les compteurs privés **ne sont pas déclarés** dans le DTO de désérialisation : ce qui n'est
  jamais lu ne peut pas fuiter. Trois tests d'architecture et un test d'intégration
  verrouillent cette propriété.
- Le corps `errors` est traduit en `ItchIoApiException`. Sans ce contrôle, une clé révoquée
  se lirait comme « compte sans jeu », et le catalogue vide qui en résulterait écraserait
  l'instantané de repli — la panne détruirait son propre filet de sécurité.
- La clé d'API est un secret : fournie par variable d'environnement (`ItchIo__ApiKey`),
  jamais dans `appsettings.json`. Elle est transmise par en-tête `Authorization` et non dans
  le chemin, où elle atterrirait dans les journaux d'accès.

## Alternatives écartées

- **Scraping des pages itch.io** : fournirait les tags et les captures automatiquement, mais
  casse à chaque évolution du balisage, sans avertissement.
- **Base de données et back-office** : demanderait authentification, CRUD et migrations pour
  administrer une vingtaine de fiches que leur auteur est seul à modifier.
- **Fichier JSON seul, sans API** : imposerait un redéploiement à chaque publication de jeu.
