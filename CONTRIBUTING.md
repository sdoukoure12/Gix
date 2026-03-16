# Guide de contribution — Gix

Merci de l'intérêt que vous portez à Gix ! Ce guide explique comment contribuer efficacement au projet.

---

## Table des matières

1. [Code de conduite](#code-de-conduite)
2. [Signaler un bug](#signaler-un-bug)
3. [Proposer une fonctionnalité](#proposer-une-fonctionnalité)
4. [Workflow Git](#workflow-git)
5. [Conventions de code](#conventions-de-code)
6. [Messages de commit](#messages-de-commit)
7. [Pull Requests](#pull-requests)

---

## Code de conduite

Ce projet respecte un [Code de Conduite](CODE_OF_CONDUCT.md). En participant, vous vous engagez à le respecter.

---

## Signaler un bug

1. **Vérifiez d'abord** que le bug n'a pas déjà été signalé dans les [issues ouvertes](../../issues).
2. Ouvrez une [nouvelle issue](../../issues/new/choose) en utilisant le template **Bug Report**.
3. Remplissez toutes les sections : description, étapes de reproduction, environnement.

> 💬 Si vous avez trouvé le bug via Discord (Korki-chat), une issue GitHub peut être créée automatiquement.

---

## Proposer une fonctionnalité

1. Ouvrez une [nouvelle issue](../../issues/new/choose) en utilisant le template **Feature Request**.
2. Discutez de la proposition avec l'équipe avant de commencer le développement.

---

## Workflow Git

Nous utilisons un workflow basé sur des branches courtes :

```
main         → branche stable, deployée en production
develop      → branche d'intégration
feature/*    → nouvelles fonctionnalités (ex: feature/filtre-satellites)
fix/*        → corrections de bugs (ex: fix/iss-ar-ios)
docs/*       → modifications de documentation
```

### Étapes

1. Créez une branche depuis `develop` :
   ```bash
   git checkout develop
   git pull
   git checkout -b feature/ma-fonctionnalite
   ```
2. Faites vos modifications (commits atomiques).
3. Poussez votre branche et ouvrez une Pull Request vers `develop`.

---

## Conventions de code

### Backend (Node.js)

- Node.js 20+, CommonJS
- Indentation : 2 espaces
- Pas de `var` (utiliser `const` / `let`)
- Pas de secrets dans le code (utiliser `.env` ou variables d'environnement)

### Mobile (Unity / C#)

- Suivre les [conventions C# de Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Nommage PascalCase pour les classes et méthodes publiques
- Commentaires XML sur les membres publics

---

## Messages de commit

Utiliser le format [Conventional Commits](https://www.conventionalcommits.org/) :

```
<type>(<scope>): <description courte>

[corps optionnel]

[pied de page optionnel, ex: Closes #42]
```

**Types :**

| Type       | Usage                                       |
|------------|---------------------------------------------|
| `feat`     | Nouvelle fonctionnalité                     |
| `fix`      | Correction de bug                           |
| `docs`     | Modification de documentation               |
| `refactor` | Refactoring sans ajout de fonctionnalité    |
| `test`     | Ajout ou modification de tests              |
| `chore`    | Maintenance (dépendances, CI, etc.)         |

**Exemple :**
```
fix(AR): correction du shader pour iOS 17

Le shader d'affichage AR ne gérait pas correctement le format
de texture sur les appareils iOS 17+.

Closes #42
```

> 💡 Référencer une issue avec `Fix #42` ou `Closes #42` la fermera automatiquement à la fusion de la PR.

---

## Pull Requests

- Ciblez la branche `develop` (sauf hotfix urgent vers `main`).
- La PR doit passer tous les checks CI avant d'être mergée.
- Au moins une revue de code est requise.
- Liez la PR à l'issue correspondante.
