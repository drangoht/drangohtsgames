# Drangoht Games

Site vitrine des jeux publiés sur [drangoht.itch.io](https://drangoht.itch.io/).
.NET 10, Blazor SSR statique, bilingue FR/EN, déployé en conteneur Docker.

Les jeux sont lus depuis l'API itch.io à l'exécution — publier un jeu suffit à le faire
apparaître, sans redéploiement.

---

## Démarrage rapide

### 1. Obtenir une clé d'API itch.io

Sur <https://itch.io/user/settings/api-keys>, générer une clé avec le scope `profile:games`.

> C'est un secret. Il ne doit jamais entrer dans `appsettings.json` ni dans le dépôt.

### 2. Lancer en local

```bash
cd src/DrangohtGames.Web
dotnet user-secrets set "ItchIo:ApiKey" "<votre-cle>"
dotnet run
```

Le site répond sur `http://localhost:5xxx` (voir `Properties/launchSettings.json`).

### 3. Lancer en conteneur

```bash
cp .env.example .env      # puis renseigner ITCHIO_API_KEY
docker compose up -d
```

---

## Ce que fait le site

| Page | Contenu |
|---|---|
| `/` | Grille des jeux, avec recherche et filtres par tag, moteur et plateforme |
| `/games/{slug}` | Fiche du jeu : description, captures, tags, widget itch.io embarqué |
| `/about` | Présentation et contact |
| `/health` | Sonde de vivacité, volontairement indépendante d'itch.io |

Les filtres passent par la chaîne de requête : une sélection produit une **URL partageable**,
et l'ensemble fonctionne sans JavaScript.

Le sélecteur de langue mémorise le choix dans un cookie. Sans choix explicite, la langue
suit l'en-tête `Accept-Language` du navigateur, avec l'anglais par défaut.

---

## Décrire ses jeux : `data/games.json`

L'API itch.io ne fournit **ni tags, ni description longue, ni captures d'écran**. Ce fichier
versionné les apporte. Il est indexé par *slug* — le dernier segment de l'URL itch.io :

```
https://drangoht.itch.io/mon-super-jeu   →   clé "mon-super-jeu"
```

```jsonc
{
  "games": {
    "mon-super-jeu": {
      "tags": ["Arcade", "Runner"],
      "engine": "Godot",
      "tagline": { "fr": "Traduction française de l'accroche itch.io" },
      "description": {
        "en": "First paragraph.\nSecond paragraph.",
        "fr": "Premier paragraphe.\nDeuxième paragraphe."
      },
      "screenshots": [
        { "url": "https://…/capture.png", "caption": { "en": "Level 1", "fr": "Niveau 1" } }
      ]
    }
  }
}
```

Tout est facultatif :

- un jeu **absent** du fichier s'affiche avec les seules données itch.io ;
- une entrée **partielle** complète ces données sans jamais les écraser — une traduction
  française s'ajoute à l'accroche anglaise plutôt que de la remplacer ;
- un slug qui ne correspond à **aucun jeu publié** est signalé dans les journaux au
  démarrage (c'est presque toujours une faute de frappe).

Les tags saisis ici alimentent les filtres de la page d'accueil.

---

## Configuration

Toutes les clés se surchargent par variable d'environnement, en remplaçant `:` par `__`
(`ItchIo:ApiKey` → `ItchIo__ApiKey`).

| Clé | Défaut | Rôle |
|---|---|---|
| `ItchIo:ApiKey` | — | **Requis.** Clé personnelle, scope `profile:games` |
| `ItchIo:Currency` | `USD` | Devise des prix plancher du compte |
| `ItchIo:CacheDuration` | `00:30:00` | Durée de réutilisation de la réponse itch.io |
| `ItchIo:Timeout` | `00:00:10` | Délai maximal d'un appel, reprises comprises |
| `Snapshot:Directory` | `/var/lib/drangohtgames` | Instantané de repli — **monter un volume** |
| `Editorial:FilePath` | `data/games.json` | Fichier de contenu éditorial |
| `Site:Name`, `Site:ItchProfileUrl`, `Site:ContactEmail`, `Site:GitHubUrl` | voir `appsettings.json` | Identité du site |

La configuration est validée **au démarrage** : une clé manquante empêche le conteneur de
démarrer, plutôt que de produire une page d'erreur à la première visite.

---

## Résilience

Trois niveaux, dans cet ordre (détail dans [ADR 0004](docs/adr/0004-cache-et-repli-sur-instantane.md)) :

1. **cache mémoire** — TTL de 30 minutes, une seule requête rafraîchit sous trafic ;
2. **appel à itch.io** — délai d'expiration explicite, reprise à back-off et disjoncteur ;
3. **instantané disque** — le dernier catalogue réussi, servi quand l'API est indisponible.

Deux comportements méritent d'être connus :

- **Un échec n'est jamais mis en cache.** Le figer transformerait une coupure de trente
  secondes en panne de trente minutes.
- **itch.io ne renvoie pas d'erreur HTTP sur une clé invalide** : il répond `200 OK` avec
  `{"errors":["invalid key"]}`. Le site le détecte et bascule sur l'instantané, au lieu de
  conclure que le compte n'a plus de jeu — ce qui écraserait le repli.

---

## Déploiement

`.github/workflows/ci-cd.yml` : tests → image Docker sur GHCR → déploiement SSH.

**Le serveur n'a aucune configuration à entretenir.** Le `.env` y est *généré* à chaque
déploiement à partir des secrets et variables du dépôt : rien à créer ni à maintenir à la
main, et aucun risque de BOM, de fins de ligne Windows ou de fichier déposé au mauvais
endroit.

**Secrets** requis :

| Secret | Rôle |
|---|---|
| `ITCHIO_API_KEY` | Clé itch.io, scope `profile:games` |
| `VPS_HOST` | Adresse du serveur |
| `VPS_USER` | Utilisateur SSH |
| `VPS_SSH_KEY` | Clé privée SSH, **sans passphrase** |

**Variables** (toutes facultatives, valeurs par défaut dans le workflow) : `SITE_URL`,
`DEPLOY_PATH` (défaut `/opt/drangohtgames`), `SITE_NAME`, `SITE_ITCH_URL`,
`SITE_CONTACT_EMAIL`, `SITE_GITHUB_URL`, `HTTP_PORT`, `ITCHIO_CURRENCY`,
`ITCHIO_CACHE_DURATION`.

Aucun secret de registre à gérer : GHCR s'authentifie avec le `GITHUB_TOKEN` natif.

```bash
gh secret set ITCHIO_API_KEY --repo drangoht/drangohtsgames
gh secret set VPS_HOST       --repo drangoht/drangohtsgames
gh secret set VPS_USER       --repo drangoht/drangohtsgames
gh secret set VPS_SSH_KEY    --repo drangoht/drangohtsgames
```

Les valeurs sont transmises au serveur par l'environnement SSH, jamais interpolées dans le
script : elles n'apparaissent donc pas dans la trace d'exécution.

### Revenir à une version précédente

Chaque image est étiquetée par le SHA court du commit, en plus de `latest`. Lancer le
workflow manuellement (« Run workflow ») en renseignant `rollback_tag` avec ce SHA :
rien n'est reconstruit, l'image déjà publiée est redéployée.

---

## Développement

```bash
dotnet test  --solution src/DrangohtGames.slnx            # 91 tests
dotnet format src/DrangohtGames.slnx --verify-no-changes  # avant commit

# Boucle rapide : tout sauf les tests d'intégration
dotnet test --solution src/DrangohtGames.slnx -- --filter-not-trait "Category=Integration"
```

Le projet compile en `TreatWarningsAsErrors` : un avertissement d'analyseur casse la build.

Trois familles de tests :

- **unitaires** — modèle, filtres, fusion éditoriale, instantané, adaptateur itch.io
  (avec un `HttpMessageHandler` de test, jamais de réseau) ;
- **architecture** — vérifient qu'aucun compteur privé du compte itch.io n'atteint le
  modèle exposé aux vues ;
- **intégration** — site complet en mémoire : routage, localisation, 404, antiforgery,
  en-têtes de sécurité.

### Conventions

- `docs/adr/` porte les décisions structurantes. Un ADR est immuable : on en écrit un
  nouveau qui supersède, on ne modifie pas l'ancien.
- Les règles de développement suivies sur ce dépôt sont dans `CLAUDE.md` et `.claude/rules/`.
  La structure Claude Code elle-même est documentée dans
  [`docs/structure-claude-code.md`](docs/structure-claude-code.md).

---

## Pistes non implémentées

- **Politique de sécurité de contenu (CSP)** : non posée, pour ne pas risquer de casser le
  widget itch.io embarqué et les scripts de Blazor. À ajouter en la validant page par page.
- **`sitemap.xml` et données structurées JSON-LD** : utiles au référencement d'une vitrine.
- **Analyse SonarCloud** dans la CI, comme sur `Algorithme-de-Huffman`.
