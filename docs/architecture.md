# Architecture Gix

## Vue d'ensemble

Gix est une application mobile de réalité augmentée (AR) qui permet de visualiser des satellites en temps réel dans le ciel.

```
┌─────────────────────────────────────────────────────────┐
│                     Client mobile (Unity AR)             │
│  ┌──────────────┐   ┌─────────────┐   ┌──────────────┐  │
│  │  AR Foundation│   │ satellite.js│   │  Interface   │  │
│  │  (ARKit/Core) │   │  (calculs)  │   │  utilisateur │  │
│  └──────┬───────┘   └──────┬──────┘   └──────┬───────┘  │
│         └──────────────────┴──────────────────┘          │
│                              │                           │
└──────────────────────────────┼───────────────────────────┘
                               │ HTTP/WebSocket
┌──────────────────────────────┼───────────────────────────┐
│              Backend Node.js  │                           │
│  ┌────────────────────────────▼─────────────────────────┐ │
│  │           API REST / WebSocket                        │ │
│  │  /satellites  /health  /tle                           │ │
│  └───────────────────────────────────────────────────── ┘ │
└─────────────────────────────────────────────────────────  ┘
                               │
                    ┌──────────▼──────────┐
                    │  Sources TLE        │
                    │  (CelesTrak, Space  │
                    │   Track)            │
                    └─────────────────────┘
```

## Composants

| Composant          | Technologie          | Rôle                                          |
|--------------------|----------------------|-----------------------------------------------|
| `mobile/`          | Unity + AR Foundation | Affichage AR, interface utilisateur           |
| `backend/`         | Node.js (HTTP)       | API de données satellites, gestion des TLEs   |
| `shared/`          | JS/TS                | Constantes et types partagés                  |
| `scripts/`         | Bash / Node.js       | Déploiement, outillage                        |

## Flux de données

1. Le backend récupère périodiquement les TLEs (Two-Line Elements) depuis CelesTrak.
2. Le client mobile interroge le backend pour obtenir les positions calculées.
3. L'application affiche les satellites en AR en superposition de la caméra.

## Sécurité

- Les clés API et tokens sont stockés dans des **variables d'environnement** (jamais dans le code).
- En production, utiliser GitHub Secrets pour les workflows CI/CD.
