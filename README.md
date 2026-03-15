# Gix — Œil-Satellites 🛰️

**Gix** est une application mobile de réalité augmentée (AR) qui permet de visualiser des satellites artificiels en temps réel dans le ciel, depuis votre smartphone.

---

## Fonctionnalités

- 🔭 Visualisation AR des satellites en temps réel
- 📡 Données TLE actualisées (ISS, Starlink, satellites météo, etc.)
- 🌍 Calcul de trajectoire précis via [satellite.js](https://github.com/shashwatak/satellite-js)
- 📱 Support iOS & Android (Unity AR Foundation)

---

## Structure du dépôt

```
Gix/
├── .github/               # Workflows CI/CD, templates d'issues
├── docs/                  # Documentation technique
├── mobile/                # Code Unity (client AR)
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
├── backend/               # Serveur Node.js
│   ├── src/
│   ├── tests/
│   └── package.json
├── shared/                # Code partagé (constantes, types)
├── scripts/               # Outils de déploiement, calculs
├── .gitignore
├── README.md
└── LICENSE
```

---

## Démarrage rapide

### Backend

```bash
cd backend
npm install
npm start
```

Le serveur écoute par défaut sur le port `3000`.  
Endpoint de santé : `GET /health`

### Mobile (Unity)

Ouvrir le dossier `mobile/` avec Unity Hub (Unity 2022 LTS+).

---

## CI/CD

Le projet utilise GitHub Actions pour :

- ✅ Installer les dépendances et lancer les tests du backend à chaque push/PR
- 🔍 Vérifier le style de code (lint)

Voir [`.github/workflows/build.yml`](.github/workflows/build.yml).

---

## Contribuer

Consultez [CONTRIBUTING.md](CONTRIBUTING.md) pour les conventions, le workflow Git et les bonnes pratiques.

Rejoignez la discussion sur Discord avant d'ouvrir une issue ou une PR.

---

## Sécurité

**Ne committez jamais de clés API ou tokens dans le code.**  
Utilisez des variables d'environnement localement et GitHub Secrets en CI.

---

## Licence

[MIT](LICENSE) — Copyright © 2026 Si
