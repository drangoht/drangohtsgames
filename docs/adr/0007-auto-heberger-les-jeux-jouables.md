# 7. Auto-héberger les jeux jouables, à partir des releases de leurs dépôts

- **Statut** : accepté
- **Date** : 2026-09-08

## Contexte

Les jeux du catalogue sont jouables en navigateur — quatre sur quatre portent `type: "html"`
côté itch.io, ce que le modèle expose déjà en `Game.IsEmbeddable`. Le site, lui, n'en montre
que la fiche : le bouton « Jouer » ouvre itch.io dans un nouvel onglet. Le visiteur part.

Deux familles de solutions existent, et elles diffèrent par bien plus qu'une balise `iframe`.

**Ce qu'offre itch.io.** Le point d'entrée jouable est `https://itch.io/embed-upload/{upload_id}`.
Il fonctionne : ouvert directement, il sert le jeu en disposition « widget ». Mais deux
constats pèsent.

- `my-games`, la seule route que le site appelle, **ne renvoie pas l'`upload_id`**. Il faudrait
  soit le saisir à la main dans le fichier éditorial, soit interroger une seconde API.
- Encadrée depuis le domaine du site, la même URL a répondu **503** — un challenge Cloudflare,
  que le cadre ne peut ni afficher ni résoudre. L'URL directe vers `html-classic.itch.zone`
  n'est pas une échappatoire : elle porte un jeton signé dans sa chaîne de requête et expire.

**Ce que nous avons déjà.** Les builds Web sont produits localement par des projets Unity 6
qui ont chacun leur dépôt GitHub. Leur format joue en notre faveur : ce sont des `.unityweb`
avec repli de décompression, donc servables en statique sans négocier de `Content-Encoding`,
et le template Web est une page autonome pensée pour le plein écran mobile. Les poids sont
modestes — 12 Mo, 20 Mo, 36 Mo. *Money Survivor* n'a pas de build Web sous la main.

**Deux contraintes cadrent le choix.** Le dépôt du site est public et Git conserve chaque
version d'un binaire pour toujours. Et le site tourne sur un petit VPS partagé, derrière un
reverse proxy.

## Options envisagées

### A — Encadrer le widget `embed-upload` d'itch.io

Rien à héberger, rien à mettre à jour : publier sur itch.io suffit à mettre le site à jour.
Les parties sont comptées par itch.io et la bande passante est la sienne.

Mais le 503 observé n'est pas un détail de configuration : il rend la fonctionnalité
dépendante d'un pare-feu tiers que nous ne pilotons pas, et qui peut la casser sans préavis
ni message d'erreur exploitable. S'ajoutent des cookies tiers déposés chez le visiteur et un
`upload_id` à tenir à jour à la main.

### B — Auto-héberger, builds versionnés dans le dépôt du site

Le plus simple à opérer : copier les fichiers, commiter, pousser. L'image est auto-portante,
le retour arrière trivial.

Le prix est irréversible : ~68 Mo aujourd'hui, et **chaque nouvelle version de chaque jeu
s'ajoute définitivement à l'historique d'un dépôt public**. Dix versions de *Chimera Protocol*
pèsent 360 Mo que plus rien n'enlève. Cela revient aussi à mêler dans un même dépôt deux
cycles de vie sans rapport : celui du site et celui des jeux.

### C — Auto-héberger, builds déposés sur un volume du serveur

Dépôt et image restent légers, le déploiement reste rapide. En échange l'état sort du
versionnement : le retour arrière du site ne rembobine plus les jeux, la mise à jour d'un jeu
redevient une manipulation manuelle sur le serveur, et rien ne garantit que ce qui tourne
correspond à ce qui est décrit.

### D — Auto-héberger, builds tirés des releases GitHub de chaque jeu

Chaque jeu publie son build Web en asset de release. Le site tient un manifeste
`slug → dépôt, étiquette, archive` et l'étape de construction de l'image télécharge puis
décompresse chaque build dans `wwwroot/play/{slug}`.

Le dépôt du site reste léger, l'image reste auto-portante, et la version du jeu servie est
inscrite dans le code du site — donc rembobinée avec lui. En échange, la construction de
l'image demande le réseau, et mettre un jeu à jour touche deux dépôts.

### E — Ne rien faire

Le site reste une vitrine qui envoie ses visiteurs ailleurs. C'est cohérent, et c'est
exactement ce qu'on cherche à changer.

## Décision

Nous retenons **D — auto-héberger à partir des releases GitHub**.

Trois critères ont tranché :

1. **L'affichage ne dépend plus d'un tiers.** Le 503 de Cloudflare disqualifie A : une
   fonctionnalité centrale ne peut pas reposer sur un pare-feu que nous ne pilotons pas.
2. **Les deux cycles de vie restent séparés.** Un jeu évolue dans son dépôt, le site dans le
   sien. B les confond, et le fait dans un historique public qu'on ne peut plus alléger.
3. **Ce qui est déployé reste décrit par le code.** C rompt ce lien ; D le conserve, dans le
   prolongement de l'ADR 0006.

Le manifeste ne sert **qu'à la construction**. À l'exécution, le site ne consulte aucune
déclaration : il constate la présence de `wwwroot/play/{slug}/index.html`. Un bouton
« Jouer » ne peut donc pas pointer vers un jeu absent — l'application observe ce qui est là
plutôt que de faire confiance à ce qui est écrit. C'est aussi ce qui donne son comportement
à *Money Survivor* sans une ligne de cas particulier : pas de build, donc lien vers itch.io,
comme aujourd'hui.

## Conséquences

**Positives**

- Le visiteur joue sans quitter le site, et sans qu'aucun cookie tiers ne soit déposé.
- La version du jeu servie est lisible dans le dépôt du site et suit ses retours arrière.
- Le dépôt public ne s'alourdit d'aucun binaire.
- Le format `.unityweb` évite toute négociation de `Content-Encoding` côté serveur.
- La politique de cache par défaut du pipeline statique suffit : `no-cache` avec un ETag fait
  revalider chaque fichier, et un jeu déjà visité répond 304 sans transférer un octet. Rien à
  régler à la main — vérifié sur le `.wasm` de 7,4 Mo.

**Négatives**

- **La construction de l'image exige le réseau vers `github.com`.** Un `docker build` hors
  ligne échoue — et il le doit : une image amputée de ses jeux serait déployée au vert.
- L'image grossit de ~68 Mo, re-téléchargés à chaque déploiement.
- Mettre un jeu à jour demande deux gestes dans deux dépôts : publier la release, puis
  déplacer l'étiquette dans le manifeste du site.
- La bande passante des parties passe désormais par le VPS.
- Nous perdons le comptage des parties par itch.io.
- `X-Frame-Options` passe de `DENY` à `SAMEORIGIN` : `DENY` interdit au site d'encadrer ses
  propres pages, y compris de même origine.
- `MapStaticAssets` fige son manifeste à la compilation : les builds, déposés ensuite, en sont
  absents et c'est le middleware de fichiers statiques qui les sert. Un `UseStaticFiles` dédié
  ne règle rien — il s'efface dès qu'un endpoint est sélectionné, et l'ordre du pipeline ne
  garantit pas qui répondra. Les extensions d'Unity (`.unityweb`) doivent donc être déclarées
  dans les options **globales** de fichiers statiques : sans elles, un 404 muet et un jeu qui
  ne démarre pas.

**À surveiller**

- **L'étiquette fait foi, sans empreinte.** Une étiquette déplacée change le contenu de
  l'image sans changer une ligne du site. Le contrôle d'empreinte est écarté pour l'instant :
  les dépôts sources sont les nôtres, et l'image est éprouvée avant d'être déployée
  (ADR 0006). Le premier build d'origine tierce doit faire reconsidérer ce point.
- Si le poids cumulé des builds rend le déploiement pénible, l'option C redevient
  défendable — mais alors comme décision assumée, pas comme dérive.

## Suivi

- [x] L'étape « Éprouver l'image » (ADR 0006) exige un `200` sur `/play/{slug}/index.html`
      pour au moins un jeu du manifeste : c'est le seul filet qui voie réellement l'archive
      décompressée dans l'image finale.
- [x] Un test vérifie qu'un jeu sans build n'affiche pas de bouton « Jouer » et que sa page
      de jeu répond 404.
- [x] `CLAUDE.md` mentionne le manifeste et le dossier `/play`.
