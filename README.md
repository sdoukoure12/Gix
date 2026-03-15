# Gix – Œil Satellites 🛰️

Prototype web fonctionnel de suivi de satellites en temps réel avec vue ciel interactive.

## Fonctionnalités

| Fonctionnalité | Description |
|---|---|
| 🗺️ **Carte Mondiale** | Carte interactive (CartoDB Dark) affichant la position en temps réel des satellites |
| ⭐ **Vue Ciel 2D** | Projection stéréographique du ciel depuis la position de l'observateur avec étoiles et satellites |
| 🛰️ **Suivi temps réel** | Propagation orbitale SGP4 via satellite.js, mise à jour toutes les 5 secondes |
| 📍 **Géolocalisation** | Détection automatique de la position de l'observateur (optionnel) |
| 📡 **Données TLE** | Source principale : [CelesTrak](https://celestrak.org) (satellites visuels) |
| 📖 **Catalogue stellaire** | 51 étoiles brillantes (catalogue HYG/Hipparcos) |

## Lancement rapide

Ce prototype est une application web statique — aucune installation requise.

```bash
# Option 1 : ouvrir directement dans le navigateur
open index.html

# Option 2 : serveur local (recommandé pour éviter les restrictions CORS)
npx serve .
# ou
python3 -m http.server 8080
```

Ensuite, ouvrez `http://localhost:8080` dans votre navigateur.

## Architecture technique

```
Gix/
├── index.html          # Page principale (SPA)
├── css/
│   └── style.css       # Thème sombre spatial
└── js/
    └── app.js          # Logique principale
                        #  ├── Propagation SGP4 (satellite.js)
                        #  ├── Conversion RA/Dec → Alt/Az
                        #  ├── Carte Leaflet + marqueurs dynamiques
                        #  └── Rendu canvas ciel 2D
```

### Dépendances (CDN)

| Bibliothèque | Version | Usage |
|---|---|---|
| [Leaflet.js](https://leafletjs.com) | 1.9.4 | Carte interactive |
| [satellite.js](https://github.com/shashwatak/satellite-js) | 5.0.0 | Propagation TLE/SGP4 |

## Feuille de route

- [x] **Étape 1** – Intégration des données de position (CelesTrak) sur carte mondiale
- [x] **Étape 2** – Catalogue stellaire et vue ciel 2D avec projection alt-azimutale
- [ ] **Étape 3** – Mode AR (Unity/ARFoundation) pour application mobile
- [ ] **Étape 4** – Alertes de passage et fonctionnalités sociales

## Licence

MIT — voir [LICENSE](LICENSE)
