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
docker compose up -d      # tire l'image *déjà déployée* depuis GHCR
```

Pour faire tourner **le code en cours** dans le conteneur, et non la version en ligne,
superposer la surcharge de développement — elle construit l'image depuis le `Dockerfile`
du dépôt et publie le site sur `http://localhost:8081` :

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

Depuis Visual Studio, c'est le profil de lancement **« Conteneur Docker »** : même
commande, navigateur ouvert au bon endroit. Le port se change par `DEV_HTTP_PORT`,
distinct de `HTTP_PORT` qui décrit le serveur.

> Éprouver son changement dans l'image finale n'est pas un luxe : la sonde `/health` est
> volontairement indépendante d'itch.io, donc un catalogue en erreur la laisse verte
> pendant que toutes les pages répondent 500.

---

## Ce que fait le site

| Page | Contenu |
|---|---|
| `/` | Grille des jeux, avec recherche et filtres par tag, moteur et plateforme |
| `/games/{slug}` | Fiche du jeu : description, captures, tags, widget itch.io embarqué |
| `/about` | Présentation et liens |
| `/games/{slug}/play` | Le jeu lui-même, quand son build est hébergé ici (ADR 0007) |
| `/health` | Sonde de vivacité, volontairement indépendante d'itch.io |
| `/robots.txt`, `/sitemap.xml` | Ce que le site publie à l'intention des robots d'indexation |

Les filtres passent par la chaîne de requête : une sélection produit une **URL partageable**,
et l'ensemble fonctionne sans JavaScript.

Le sélecteur de langue mémorise le choix dans un cookie. Sans choix explicite, la langue
suit l'en-tête `Accept-Language` du navigateur, avec l'anglais par défaut.

Chaque page indexable désigne son **adresse canonique** et porte sa **carte de partage**
(Open Graph et Twitter) : sans cela, la combinatoire des filtres de l'accueil serait indexée
comme autant de copies. La fiche d'un jeu ajoute ses **données structurées** `VideoGame`
(schema.org), qui donnent au moteur de recherche le prix, les plateformes et la date de
sortie. Tout cela est regroupé dans le composant `SeoHead` — plusieurs `<HeadContent>` sur
une même page ne s'additionnent pas, le dernier rendu efface les précédents.

L'illustration de la vitrine et celle des cartes de partage suivent la même règle
(`Game.ShowcaseImageUrl`) : la première capture, à défaut la couverture itch.io — qui ne
fait que 315 pixels de large.

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
| `Site:Name`, `Site:ItchProfileUrl`, `Site:GitHubUrl` | voir `appsettings.json` | Identité du site |

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

**Prérequis serveur** : Docker Engine et le plugin **Docker Compose v2**. La v1 autonome,
hors support depuis juillet 2023, n'est pas prise en charge — le déploiement s'arrête avec un
message explicite si elle est la seule présente.

```bash
# Docker installé depuis le dépôt officiel Docker
sudo apt-get update && sudo apt-get install -y docker-compose-plugin

# Docker installé autrement (paquet Ubuntu docker.io, script…) : ce paquet n'existe pas
# dans les dépôts, on pose le plugin directement.
sudo mkdir -p /usr/local/lib/docker/cli-plugins
sudo curl -fsSL "https://github.com/docker/compose/releases/latest/download/docker-compose-linux-$(uname -m)"   -o /usr/local/lib/docker/cli-plugins/docker-compose
sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
```

**Secrets** requis :

| Secret | Rôle |
|---|---|
| `ITCHIO_API_KEY` | Clé itch.io, scope `profile:games` |
| `VPS_HOST` | Adresse du serveur |
| `VPS_USER` | Utilisateur SSH |
| `VPS_SSH_KEY` | Clé privée SSH, **sans passphrase** |

**Variables** (toutes facultatives, valeurs par défaut dans le workflow) : `SITE_URL`,
`DEPLOY_PATH` (défaut `/opt/drangohtgames`), `SITE_NAME`, `SITE_ITCH_URL`,
`SITE_GITHUB_URL`, `HTTP_PORT` (défaut `8081` — le port hôte, 8080 étant déjà pris
sur le serveur de production), `ITCHIO_CURRENCY`, `ITCHIO_CACHE_DURATION`.

Aucun secret de registre à gérer : GHCR s'authentifie avec le `GITHUB_TOKEN` natif.

```bash
gh secret set ITCHIO_API_KEY --repo drangoht/drangohtsgames
gh secret set VPS_HOST       --repo drangoht/drangohtsgames
gh secret set VPS_USER       --repo drangoht/drangohtsgames
gh secret set VPS_SSH_KEY    --repo drangoht/drangohtsgames
```

Les valeurs sont transmises au serveur par l'environnement SSH, jamais interpolées dans le
script : elles n'apparaissent donc pas dans la trace d'exécution.

> **Secret ou variable ?** Le workflow lit les secrets dans `secrets.*` et les variables
> dans `vars.*` : une valeur enregistrée du mauvais côté est **silencieusement ignorée**,
> les défauts s'appliquent, et rien ne le signale. Seules les quatre valeurs du tableau
> ci-dessus sont des secrets. Tout le reste — à commencer par `SITE_URL` — est une
> variable : une URL publique enregistrée en secret est masquée en `***` dans la trace, ce
> qui rend le moindre diagnostic impossible (`Could not resolve host: ***`).

### Publier le site derrière nginx

Le conteneur expose l'application **en HTTP clair** sur le port `8081` de l'hôte. Il ne
porte aucun certificat : le TLS s'arrête au reverse proxy.

Prérequis, dans cet ordre — un certificat ne peut pas être délivré avant que le nom résolve :

1. un enregistrement DNS `A` du sous-domaine vers l'adresse du serveur (et un `AAAA`
   **seulement** si le serveur répond vraiment en IPv6 : Let's Encrypt la privilégie et
   échouerait sinon) ;
2. `sudo certbot --nginx -d <domaine>`, qui crée le vhost et le certificat en une passe.

Le `location` généré par Certbot est à remplacer par celui-ci :

```nginx
location / {
    proxy_pass http://127.0.0.1:8081;

    proxy_set_header Host              $host;
    proxy_set_header X-Real-IP         $remote_addr;
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_http_version 1.1;
}
```

Chaque ligne répare une panne constatée :

| | |
|---|---|
| `http://`, pas `https://` | Kestrel écoute en clair dans le conteneur. Un `proxy_pass https://` provoque un **502** : nginx tente un handshake TLS en face d'un serveur qui n'en fait pas. |
| `127.0.0.1`, pas `localhost` | `localhost` peut se résoudre en `::1` d'abord ; l'adresse littérale supprime cet aléa. |
| Les quatre `proxy_set_header` | `Program.cs` appelle `UseForwardedHeaders`. Sans eux, l'application se croit servie en HTTP clair sur `127.0.0.1:8081` : plus d'en-tête HSTS, cookie antiforgery sans l'attribut `secure`, et URL absolues fausses. |

Vérification — les deux doivent répondre `200` :

```bash
curl -sS -o /dev/null -w '%{http_code}\n' https://<domaine>/health
ssh <serveur> "curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8081/health"
```

Le déploiement fait lui-même la seconde vérification à chaque exécution : si l'application
répond localement mais pas publiquement, la panne est dans le proxy, le DNS ou le
certificat — jamais dans le conteneur.

### Revenir à une version précédente

Chaque image est étiquetée par le SHA court du commit, en plus de `latest`. Lancer le
workflow manuellement (« Run workflow ») en renseignant `rollback_tag` avec ce SHA :
rien n'est reconstruit, l'image déjà publiée est redéployée.

---

## Développement

```bash
dotnet test  --solution src/DrangohtGames.slnx            # 100 tests
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
- **URL par langue et `hreflang`** : la langue vit dans un cookie, pas dans l'URL. Un moteur
  n'indexe donc qu'une seule version du site, et un lien partagé n'ouvre pas forcément la
  langue du contenu partagé. À trancher dans un ADR avant d'être implémenté.
- **Flux RSS/Atom des sorties** : le canal de suivi le moins coûteux pour une vitrine de jeux.
- **Analyse SonarCloud** dans la CI, comme sur `Algorithme-de-Huffman`.
