# 8. Porter la langue dans le chemin, et déclarer les versions l'une à l'autre

- **Statut** : accepté
- **Date** : 2026-09-11

## Contexte

Le site est publié en deux langues. Ce n'est pas une simple traduction d'interface : le
fichier éditorial porte un titre, une description et des tags par langue (`LocalizedText`),
si bien qu'une fiche de jeu en français et la même en anglais sont **deux contenus
distincts**, tous deux rédigés.

La langue, elle, ne vit nulle part dans l'URL. Elle est choisie par trois moyens, dans
l'ordre où ASP.NET Core les consulte : le paramètre de requête `?culture=`, le cookie
`.AspNetCore.Culture` que pose `POST /culture`, puis l'en-tête `Accept-Language`. À défaut,
l'anglais — le public d'itch.io est majoritairement anglophone.

Deux constats, vérifiés sur le site déployé, motivent cette décision.

**Le français n'est pas indexable.** Le paramètre de requête fonctionne déjà sans qu'une
ligne ait été écrite pour lui : `/?culture=fr` répond en français, avec `<html lang="fr">`
et `Content-Language: fr`. Mais depuis la mise en place des adresses canoniques, cette même
page déclare `<link rel="canonical" href="https://drangohtsgames.thognard.net/">` — elle se
présente donc au moteur comme une copie de la version anglaise. La moitié du contenu rédigé
est invisible en recherche, et c'est nous qui le disons explicitement.

**Un lien partagé n'ouvre pas la langue de ce qu'on partage.** Qui lit une fiche en français
et en copie l'adresse transmet `/games/snake-snack`, que son destinataire ouvrira en anglais
— ou dans la langue de son propre navigateur. L'aperçu de partage lui-même, que les robots
des réseaux sociaux réclament sans cookie ni `Accept-Language` utile, est toujours produit
en anglais.

Le site compte quatre routes publiques : `/`, `/games/{slug}`, `/games/{slug}/play` et
`/about`. C'est peu, et cela ne réaugmentera pas beaucoup — le catalogue grandit en nombre
de jeux, pas en nombre de gabarits.

Enfin, une contrainte qui pèse sur toutes les options : **une URL publiée est un contrat**.
Les adresses actuelles sont dans le plan du site depuis aujourd'hui ; elles seront bientôt
dans l'index de Google et dans les liens que d'autres auront posés. Changer d'avis après
coup coûte des redirections à maintenir pour toujours.

## Options envisagées

### A — Ne rien faire

Le cookie continue de porter la langue. Une seule version du site est indexée, en anglais,
ce qui est déjà le cas et n'a jamais gêné personne.

Le coût est nul et le risque aussi. En regard : le contenu français reste écrit pour rien du
point de vue de la recherche, et le problème du lien partagé demeure. Cette option se défend
d'autant mieux qu'aucune donnée ne dit aujourd'hui qu'un visiteur francophone cherche ces
jeux. Elle reste réversible — mais de moins en moins, à mesure que les URL actuelles
s'installent.

### B — Préfixe de chemin : `/en/…` et `/fr/…`

La culture devient un segment de chemin, détaché à l'entrée du pipeline et porté en
`PathBase`, de sorte qu'aucune route de page n'ait à le connaître. Chaque
page indexable existe à deux adresses, qui se déclarent mutuellement par `hreflang`, avec un
`x-default` pointant sur l'anglais. Le plan du site double.

C'est la forme que Google recommande explicitement, et celle que le visiteur reconnaît sans
explication : la langue se lit dans la barre d'adresse et survit à un copier-coller. Effet
de bord agréable, le sélecteur de langue redevient un simple lien vers la même page dans
l'autre préfixe — le cookie, l'endpoint `POST /culture` et son jeton antiforgery n'ont plus
de raison d'être.

Le prix est le plus élevé des quatre : toutes les routes bougent, tous les liens internes
doivent préfixer, la racine doit rediriger selon `Accept-Language`, et les adresses
actuelles — déjà publiées — doivent être redirigées ou se déclarer canoniques vers leur
version préfixée. Les ressources qui n'ont pas de langue (`/play/{slug}`, les fichiers
statiques, `/health`) doivent rester hors du préfixe, ce qui demande de distinguer deux
familles de routes là où il n'y en avait qu'une.

### C — Paramètre de requête : `?culture=fr`, rendu canonique

L'option la moins chère de très loin, puisque le mécanisme **fonctionne déjà**. Il resterait
à rendre l'adresse canonique consciente de la culture courante, à ajouter les `hreflang` et
`og:locale`, et à doubler les entrées du plan du site. Une demi-journée, sans toucher une
seule route.

Mais Google déconseille cette forme pour les variantes de langue : un paramètre se reconnaît
mal, se perd au partage et à la réécriture d'URL, et ne permet aucun ciblage dans la Search
Console. Ici s'ajoute une gêne propre au site : l'accueil utilise déjà la chaîne de requête
pour ses filtres, et `culture` viendrait mêler à ces critères de contenu un paramètre qui
n'en est pas un. Surtout, cette option **produit des URL qu'il faudra rediriger** le jour où
l'on passe à B : elle ne fait pas gagner du temps, elle en emprunte.

### D — Sous-domaine : `fr.drangohtsgames.thognard.net`

Séparation nette, ciblage géographique possible, et la forme recommandée quand les versions
sont hébergées séparément. Ce n'est pas notre cas : un seul conteneur, un seul reverse proxy.

Il faudrait un second nom, un second certificat, une entrée de plus dans nginx, et le cookie
de langue comme les jetons antiforgery seraient à repenser à cheval sur deux origines. Tout
ce coût pour deux langues servies par le même processus, à partir des mêmes fichiers.

## Décision

Nous retenons **B — le préfixe de chemin**.

Trois critères tranchent :

1. **Le contenu français existe déjà, écrit et relu.** Le rendre indexable ne crée rien : il
   cesse de le cacher. C'est ce qui distingue cette décision d'une extension spéculative.
2. **Les URL sont un contrat public, et c'est maintenant qu'elles coûtent le moins cher à
   changer.** Le site a quatre gabarits de page et son plan a été publié il y a quelques
   heures. Chaque semaine d'indexation rend B plus coûteuse, et C n'offre qu'un sursis
   qu'elle facture plus tard en redirections.
3. **La langue doit voyager avec le lien.** C'est le seul des deux problèmes que le visiteur
   rencontre vraiment, et aucune balise `hreflang` ne le résout tant que l'adresse ne porte
   pas la langue.

Cette décision dérogerait à YAGNI si elle anticipait un besoin : ce n'est pas le cas.
Elle répare un contenu rendu invisible par la décision précédente, et choisit la forme
d'URL pendant qu'un choix est encore gratuit. La règle du projet réserve précisément cette
exception aux **frontières et aux contrats publics**.

Si la décision est repoussée, l'option honnête n'est pas C mais **A** — ne rien faire, et
assumer un site anglophone dont la traduction française sert le visiteur mais pas la
recherche.

## Conséquences

**Positives**

- Les deux versions sont indexables, chacune sous sa propre adresse, et se désignent l'une
  l'autre par `hreflang` réciproque et `x-default`.
- Un lien copié ouvre la langue dans laquelle il a été lu, y compris pour les robots
  d'aperçu des réseaux sociaux, qui n'envoient ni cookie ni `Accept-Language` exploitable.
- Le sélecteur de langue redevient un lien. Disparaissent avec lui `POST /culture`, son
  jeton antiforgery, sa protection contre la redirection ouverte et le cookie de culture —
  c'est du code en moins, pas en plus.
- La langue d'une page se lit dans les journaux du serveur et dans la Search Console.

**Négatives**

- **Les adresses publiées aujourd'hui changent.** Il faut soit les rediriger en 301 vers la
  version préfixée, soit continuer à les servir en les déclarant canoniques vers elle. Dans
  les deux cas, c'est une compatibilité à porter indéfiniment.
- Toutes les routes de page et tous les liens internes sont touchés en une fois : c'est
  exactement le genre de changement large où l'on casse un lien sans s'en apercevoir.
- Il faut distinguer les routes qui portent une langue de celles qui n'en portent pas
  (`/play/{slug}`, fichiers statiques, `/health`, `/robots.txt`, `/sitemap.xml`). Cette
  frontière n'existe pas aujourd'hui.
- La racine `/` devient une redirection, donc une requête de plus pour tout premier visiteur.
- Le plan du site double de taille et doit porter les `xhtml:link` alternates — un plan dont
  les réciprocités sont incomplètes est ignoré par Google, silencieusement.
- Une troisième langue deviendrait bien plus coûteuse à ajouter qu'aujourd'hui : elle
  multiplierait les URL, pas seulement les fichiers `.resx`.

**À surveiller**

- Le signal qui ferait reconsidérer, dans un sens comme dans l'autre, est le trafic réel par
  langue dans la Search Console une fois les deux versions indexées. Si la version française
  ne reçoit rien au bout de quelques mois, c'est que A était la bonne réponse et que nous
  avons acheté de la complexité.
- La décision suppose que les deux versions restent **intégralement** traduites. Une fiche
  dont le texte français retombe sur l'anglais crée deux URL au contenu identique, que
  `hreflang` n'excuse pas. Le jour où une traduction manque, il faudra choisir entre ne pas
  publier l'URL française de cette fiche et la laisser dupliquer l'anglaise.

## Suivi

- [x] Les adresses sans préfixe redirigent en **301** vers leur version anglaise ; seule
      la racine négocie, en **302**, puisque sa destination dépend du visiteur.
- [x] Un test vérifie qu'une page indexable déclare un `hreflang` **réciproque** vers chaque
      autre langue, plus `x-default` — la réciprocité manquante est l'erreur classique, et
      elle ne se voit pas à l'œil.
- [x] Un test vérifie que `/health`, `/robots.txt` et `/sitemap.xml` restent accessibles
      **sans** préfixe, et qu'une feuille de style répond identiquement avec et sans.
- [x] Le plan du site porte une entrée par langue et par page. Les alternatives, elles,
      sont déclarées dans l'en-tête des pages : Google accepte l'une **ou** l'autre méthode,
      et les tenir aux deux endroits, c'est se donner deux occasions de les désaccorder.
- [x] `POST /culture`, le cookie de culture et leurs tests sont retirés : le sélecteur
      est devenu un lien, et rien d'autre ne les utilisait.
- [x] `README.md` (tableau des routes, section sur les langues) et `CLAUDE.md` sont à jour.
