# 4. Cache mémoire et repli sur instantané disque

- **Statut** : accepté
- **Date** : 2026-09-06

## Contexte

Chaque page du site a besoin de la liste des jeux. Interroger itch.io à chaque requête
serait à la fois lent et abusif envers un service tiers gratuit. Et itch.io peut être
indisponible — un site vitrine qui affiche une erreur pendant ce temps ne remplit plus sa
fonction.

## Décision

**Trois niveaux, dans cet ordre.**

1. **Cache mémoire** (`IMemoryCache`, TTL configurable, 30 minutes par défaut), protégé par
   un `SemaphoreSlim` : au démarrage à froid sous trafic, une seule requête rafraîchit le
   catalogue, les autres attendent son résultat.
2. **Appel à itch.io**, avec délai d'expiration explicite, reprise à back-off exponentiel et
   disjoncteur (`AddStandardResilienceHandler`). Une lecture est idempotente : la retenter
   est sans risque.
3. **Instantané disque**, écrit après chaque appel réussi dans un volume Docker, et relu
   quand l'appel échoue.

Trois choix méritent d'être explicités :

- **L'échec n'est jamais mis en cache.** Le figer pour la durée du TTL transformerait une
  coupure de trente secondes en panne de trente minutes.
- **Un catalogue vide ne remplace pas un instantané peuplé.** Défense en profondeur : le
  repli est le dernier filet du site, une réponse anormale ne doit pas le détruire.
- **L'écriture passe par un fichier temporaire puis un remplacement atomique.** Une coupure
  en cours d'écriture laisserait sinon un instantané tronqué — inutilisable précisément au
  moment où il compte.

Aucune opération du magasin d'instantanés ne lève : un volume non monté ou en lecture seule
dégrade le repli, il ne doit pas faire échouer une requête qui, elle, a réussi.

## Conséquences

- Le site reste consultable pendant une panne d'itch.io, avec un catalogue potentiellement
  périmé de quelques heures. Pour une vitrine, c'est très préférable à une page d'erreur.
- Le volume `snapshot` doit être monté : sans lui, l'instantané disparaît à chaque
  redéploiement, c'est-à-dire au moment où le cache mémoire est vide et où il servirait.
- Au tout premier démarrage, aucun instantané n'existe. Si itch.io est déjà en panne, la page
  d'accueil affiche un message explicite et journalise en `Critical`.
- **La sonde `/health` ne dépend pas d'itch.io.** La rendre dépendante ferait redémarrer le
  conteneur en boucle pendant une panne tierce, sans rien réparer.

## Alternatives écartées

- **`HybridCache`** : sérialise les entrées même sans cache distribué, ce qui obligerait à
  écrire et maintenir des convertisseurs JSON pour `GameSlug` et `Price`. Le site tourne sur
  une instance unique ; le second niveau de cache ne servirait rien.
- **`BackgroundService` de rafraîchissement périodique** : élimine la latence du premier
  appel, mais interroge itch.io indéfiniment même sans visiteur.
