# 6. Éprouver l'image sur une page réelle, pas sur la sonde de vivacité

- **Statut** : accepté
- **Date** : 2026-09-07

## Contexte

Une migration du client HTTP a été déployée en production alors que **toutes** les pages
listant des jeux répondaient 500. La chaîne d'intégration est restée verte de bout en bout :

- la suite de tests passait — `SiteFactory` substitue `IItchIoClient` pour qu'aucun appel
  réseau ne parte de la suite, si bien que le client HTTP réel n'était jamais construit ;
- l'image se construisait — la faute était un mode d'enregistrement dans le conteneur DI,
  invisible à la compilation ;
- la vérification post-déploiement passait — elle n'interroge que `/health`, et cette sonde
  est **volontairement** indépendante d'itch.io (ADR 0004 : marquer le conteneur malsain
  parce qu'une API tierce est tombée le ferait redémarrer en boucle sans rien réparer).

Chaque maillon faisait exactement ce pour quoi il était conçu. Aucun ne regardait le site
tel qu'un visiteur le voit.

## Décision

**Trois filets, du plus rapide au plus fidèle.**

1. **`CompositionRootTests`** résout le graphe tel que `Program.cs` l'enregistre, sans
   aucune doublure. Il construit le client HTTP sans s'en servir : rien ne part sur le
   réseau, et la panne ci-dessus est détectée en 200 ms. C'est le filet qui compte, parce
   que c'est celui qui tourne à chaque exécution de la suite.
2. **L'étape « Éprouver l'image »** démarre l'image finale publiée et exige un `200` sur
   `/health` **et sur `/`**, avant tout déploiement. La clé d'API fournie est volontairement
   invalide : l'application doit servir un catalogue vide sans faillir, et aucun secret
   n'entre dans l'étape.
3. **La vérification post-déploiement** interroge elle aussi la page d'accueil. Selon la
   page qui échoue, le message d'erreur oriente vers le proxy ou vers les journaux du
   conteneur — les deux causes n'ont rien à voir.

Corollaire, valable au-delà de ce dépôt : **une sonde de vivacité ne prouve pas qu'un site
fonctionne.** Elle prouve que le processus est vivant. C'est sa raison d'être, et c'est
précisément pourquoi elle ne peut pas servir de critère de succès à un déploiement.

## Conséquences

- Le déploiement d'une image incapable de rendre sa page d'accueil est bloqué avant d'être
  mis en ligne, et les journaux du conteneur fautif sont versés à la trace d'exécution.
- La suite conserve sa doublure : les tests d'intégration ne doivent toujours pas appeler
  le réseau. Le filet nº 1 ne les remplace pas, il couvre ce qu'ils masquent par
  construction.
- `docker-compose.dev.yml` permet d'éprouver le code en cours dans l'image, en local et
  depuis Visual Studio. Le compose de déploiement, seul, fait tourner la version déjà en
  ligne — il ne dit donc rien du changement qu'on vient d'écrire.
- Le coût est d'environ trente secondes par exécution de la chaîne. Le déploiement de cette
  panne-là a coûté vingt minutes de site hors service.

## Alternatives écartées

- **Un `HEALTHCHECK` Docker plus riche** : l'image « chiseled » n'a ni shell, ni curl, ni
  wget pour l'exécuter, et une sonde qui dépend d'itch.io redémarrerait le conteneur en
  boucle pendant une panne du fournisseur.
- **Un test Testcontainers construisant l'image depuis la suite xUnit** : le `Dockerfile`
  lance `dotnet test` pendant la construction, si bien qu'un tel test se construirait
  lui-même en boucle. Il faudrait l'exclure par un trait de la suite jouée dans l'image,
  et reconstruire une image que la chaîne construit déjà juste après. L'étape de CI obtient
  la même garantie sur l'artefact réellement publié, sans duplication.
