# Gix
Oeil-Satellites

---

## Vue d'ensemble (Diagramme conceptuel)

```
+-------------------+       +-------------------+       +-------------------+
|   Client mobile   | <--> |    Backend Gix    | <--> | Services externes |
| (iOS/Android)     |       | (Node.js + Firebase)|       | (API métier)       |
+-------------------+       +-------------------+       +-------------------+
        ^                           ^                           ^
        | (données locales)          | (logs, analytics)         |
        v                           v                           v
+-------------------+       +-------------------+       +-------------------+
|   Stockage local  |       |   Base de données |       |   Cache Redis     |
| (SQLite, fichiers) |       |   (Firestore)     |       |   (optionnel)     |
+-------------------+       +-------------------+       +-------------------+
```

---

## 1. Client mobile (Frontend)

Le client est développé en multiplateforme pour couvrir iOS et Android avec un maximum de code partagé.

### Technologie principale

- Unity avec AR Foundation (qui encapsule ARKit pour iOS et ARCore pour Android).
- Pour les parties non-AR (cartes, interface 2D), on peut utiliser des plugins Unity ou des composants natifs via des bridges.

### Modules du client

#### a) Moteur AR (Ciel vivant)

- Gère la caméra, le tracking de mouvement et l'orientation.
- Superpose les éléments 3D (noms, constellations, trajectoires) en temps réel.
- Optimisation : niveau de détail (LOD) pour les objets lointains, instancing pour les étoiles.

#### b) Moteur de rendu céleste

- Affiche les étoiles et objets du ciel profond à partir d'un catalogue local.
- Utilise des shaders pour simuler la luminosité et la couleur des astres.
- Gère la précession et la nutation pour des positions précises.

#### c) Module de calcul de positions

- Pour les étoiles fixes : utilise une librairie comme SkyField (portée en C# pour Unity) ou des algorithmes intégrés.
- Pour les satellites : récupère les TLE (Two-Line Elements) depuis le backend et calcule les positions avec la librairie SGP4 (implémentation C# disponible).

#### d) Gestionnaire de cache local

- Stocke en base locale (SQLite) les données fréquemment utilisées :
  - Catalogues d'étoiles (indexés par coordonnées)
  - Dernières positions des satellites
  - Préférences utilisateur
- Permet le mode hors ligne (avec données pré-téléchargées).

#### e) Interface utilisateur (UI)

- Développée avec Unity UI (Canvas) ou avec des composants natifs via des plugins (ex: Embedded Browser pour afficher du contenu web).
- Gère les écrans : accueil, recherche, paramètres, notifications.

---

## 2. Backend (API & services)

Le backend assure la synchronisation des données, les calculs lourds, les notifications et la gestion des utilisateurs.

### Stack technique

- Node.js avec Express pour l'API REST.
- Firebase (ou Supabase) pour :
  - Authentification (Firebase Auth)
  - Base de données temps réel (Firestore) pour les données utilisateur et les événements
  - Notifications push (Firebase Cloud Messaging)
  - Stockage de fichiers (Firebase Storage) pour les assets (images, descriptions)

### Modules backend

#### a) API Gateway

- Point d'entrée unique pour les requêtes client.
- Gère l'authentification (JWT) et la limitation de débit.

#### b) Service de données satellitaires

- Récupère périodiquement les TLE (toutes les 6h pour les satellites en orbite basse, toutes les 12h pour les orbites plus stables) depuis :
  - CelesTrak (API publique)
  - Space-Track.org (nécessite un compte, plus complet)
- Stocke les TLE dans une base de données (PostgreSQL ou Firestore).
- Fournit des endpoints pour que le client récupère les TLE par satellite ou par groupe.

#### c) Service de données astronomiques

- Agrège les catalogues :
  - HYG Database (~120k étoiles, libre)
  - Messier et NGC/IC pour les objets du ciel profond
- Peut aussi interroger VizieR ou SIMBAD pour des données supplémentaires.
- Met en cache les résultats pour réduire les appels externes.

#### d) Service de calculs avancés

- Effectue des calculs lourds que le client ne peut pas gérer (ex : prévisions de visibilité sur une zone, calculs de conjonctions).
- Utilise des workers Node.js pour ne pas bloquer l'API.

#### e) Service de notifications

- Planifie des tâches cron pour détecter les événements (passage de l'ISS, éclipses) en fonction de la position des utilisateurs.
- Utilise Firebase Cloud Messaging pour envoyer des notifications push personnalisées.

#### f) Service de gestion des utilisateurs

- Stocke les préférences, les favoris, l'historique des observations.
- Permet la synchronisation entre plusieurs appareils.

---

## 3. Services externes

Gix s'appuie sur plusieurs API publiques et privées :

| Service | Données fournies | Utilisation |
|---|---|---|
| CelesTrak | TLE des satellites (public) | Source principale pour les orbites |
| Space-Track.org | TLE + données de mission | Source complémentaire (inscription requise) |
| VizieR / SIMBAD | Catalogues astronomiques | Enrichissement des objets (noms, type) |
| NASA APIs | Flux ISS, images, APOD | Pour la fonction "Ce que voit l'ISS" |
| OpenNotify | Position ISS (temps réel) | Alternative simple |
| Google Maps / Mapbox | Cartes pour la vue satellite | Affichage des traces au sol |

---

## 4. Gestion des données et flux

### a) Cycle de vie des données satellites

1. Un job cron sur le backend (toutes les 6h) interroge CelesTrak/Space-Track.
2. Les nouveaux TLE sont stockés en base (versionnés).
3. Le client, au démarrage, demande les TLE mis à jour (seulement les delta).
4. Le client calcule localement les positions avec SGP4.
5. En mode AR, les positions sont mises à jour en temps réel (rafraîchissement ~1s).

### b) Catalogues astronomiques

- Le catalogue HYG (120k étoiles) est pré-chargé dans l'application (fichier binaire optimisé pour la recherche par coordonnées).
- Pour les objets du ciel profond, on peut utiliser un fichier JSON compressé.
- Des mises à jour peuvent être poussées via le backend si nécessaire.

### c) Synchronisation utilisateur

- Les favoris et préférences sont stockés localement et synchronisés avec Firestore lorsque la connexion est disponible.
- Conflits résolus par "dernière modification".

---

## 5. Sécurité et performances

### Sécurité

- **Authentification** : Firebase Auth avec options (email, Google, Apple).
- **API sécurisée** : tokens JWT, validation des requêtes.
- **Données sensibles** : la géolocalisation n'est stockée que temporairement pour les notifications et anonymisée.
- **Respect RGPD** : consentement explicite pour la localisation, possibilité de supprimer son compte.

### Performances

- **Client** :
  - Mise en cache agressive des données.
  - Calculs SGP4 optimisés (implémentation en C# avec Burst Compiler si Unity).
  - Réduction de la précision des positions pour les satellites lointains.
- **Backend** :
  - Utilisation de Redis pour mettre en cache les TLE et les calculs fréquents.
  - Scaling horizontal avec Node.js (clusters) et load balancing.
  - CDN pour les assets statiques (images, descriptions).

---

## 6. Déploiement et infrastructure

- **Backend** : hébergé sur Google Cloud Run (conteneurisé) ou AWS Elastic Beanstalk pour l'élasticité.
- **Base de données** : Firestore pour les données utilisateur, PostgreSQL (optionnel) pour les TLE si besoin de requêtes complexes.
- **Stockage** : Firebase Storage pour les images utilisateur et les assets.
- **CI/CD** : GitHub Actions pour les tests et le déploiement automatique.

---

## 7. Alternatives et choix justifiés

- **Pourquoi Unity plutôt que natif Swift/Kotlin ?**
  - Unity permet de partager le code AR et de rendu entre iOS et Android.
  - AR Foundation simplifie l'abstraction des SDK AR.
  - Plus facile pour intégrer des shaders complexes et des effets visuels.
  - Cependant, cela augmente la taille de l'application (~30-50 Mo de plus). On peut optimiser via la compression des assets, le code stripping et des builds spécifiques par plateforme.
- **Pourquoi Firebase ?**
  - Solution "backend as a service" rapide à mettre en place.
  - Notifications push intégrées, synchronisation temps réel.
  - Idéal pour un MVP. Si le projet grossit, on pourra migrer vers une solution plus personnalisée.
- **Pourquoi des TLE et calculs locaux plutôt qu'une API de positions en temps réel ?**
  - Réduit la charge serveur et la latence.
  - Permet le mode hors ligne.
  - Les TLE sont suffisamment précis pour un usage grand public (erreur < 1 km dans les 24-48h suivant l'époque du TLE ; au-delà, les mises à jour régulières restent indispensables).

---

## 8. Défis techniques et solutions

| Défi | Solution |
|---|---|
| Grand nombre d'étoiles en AR | Utiliser un octree pour le culling, ne rendre que les étoiles visibles dans le champ de la caméra. |
| Précision des positions satellites | Implémenter SGP4 en double précision, mettre à jour les TLE régulièrement. |
| Synchronisation des données entre appareils | Utiliser Firestore avec des listeners en temps réel. |
| Consommation batterie | Limiter les mises à jour AR à 30 fps, arrêter le tracking quand l'écran est verrouillé. |
| Gestion des fuseaux horaires pour les événements | Stocker les événements en UTC et convertir côté client. |
