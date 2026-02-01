# Architecture EasySave v1.0

Ce document décrit l’architecture du logiciel EasySave v1.0 conçue selon les principes **SOLID** et **POO** et préparée pour une évolution vers une interface graphique en v2.0.


## 1. Principes appliqués

### 1.1 SOLID

| Principe | Application dans EasySave |
|----------|---------------------------|
| **S** – Single Responsibility | Chaque classe a une seule raison de changer : `BackupExecutor` exécute les sauvegardes, `StateWriter` gère l’état temps réel, `DailyLogWriter` (EasyLog) gère le log journalier. |
| **O** – Open/Closed | Ouvert à l’extension (nouvelles stratégies de sauvegarde, nouveaux types de log) sans modifier le code existant, via interfaces (`IBackupStrategy`, `ILogWriter`). |
| **L** – Liskov Substitution | Toute implémentation d’`IBackupStrategy` (complète, différentielle) peut remplacer une autre sans casser le comportement attendu. |
| **I** – Interface Segregation | Interfaces fines et ciblées : `IBackupExecutor`, `IStateWriter`, `IConfigurationRepository`, `IFileSystemService`, etc. Pas d’interface « fourre-tout ». |
| **D** – Dependency Inversion | Les couches hautes (Console, Application) dépendent d’abstractions (interfaces) ; les implémentations concrètes sont injectées (Infrastructure). |

### 1.2 Séparation des couches (Clean Architecture simplifiée)

- **Domain (Core)** : entités, énumérations, contrats (interfaces). Aucune dépendance vers l’infrastructure ou la présentation.
- **Application** : orchestration des cas d’usage (ex. « exécuter les sauvegardes 1 à 3 »). Dépend uniquement du Domain.
- **Infrastructure** : implémentations (fichiers, disques, réseau, EasyLog). Dépend du Domain et éventuellement d’EasyLog.
- **Presentation (Console)** : point d’entrée, parsing des arguments, affichage, composition (DI). Dépend du Domain et de l’Application/Infrastructure via des interfaces.

Cela permet plus tard d’ajouter une couche **Presentation (WPF MVVM)** sans toucher au cœur métier.

---

## 2. Structure de la solution

```
easy-save/
├── src/
│   ├── EasyLog/                    # Bibliothèque réutilisable (DLL séparée)
│   │   ├── EasyLog.csproj
│   │   ├── ILogWriter.cs
│   │   ├── DailyLogWriter.cs
│   │   └── LogEntry.cs
│   │
│   ├── EasySave.Core/              # Domaine + interfaces (aucune dépendance externe)
│   │   ├── EasySave.Core.csproj
│   │   ├── Entities/
│   │   │   ├── BackupJob.cs
│   │   │   └── BackupProgress.cs
│   │   ├── Enums/
│   │   │   ├── BackupType.cs
│   │   │   └── BackupState.cs
│   │   └── Interfaces/
│   │       ├── IBackupStrategy.cs
│   │       ├── IBackupExecutor.cs
│   │       ├── IStateWriter.cs
│   │       ├── IConfigurationRepository.cs
│   │       └── IFileSystemService.cs
│   │
│   ├── EasySave.Infrastructure/    # Implémentations (fichiers, état, config)
│   │   ├── EasySave.Infrastructure.csproj
│   │   ├── Backup/
│   │   │   ├── FullBackupStrategy.cs
│   │   │   ├── DifferentialBackupStrategy.cs
│   │   │   └── BackupExecutor.cs
│   │   ├── Persistence/
│   │   │   ├── JsonConfigurationRepository.cs
│   │   │   └── JsonStateWriter.cs
│   │   └── FileSystem/
│   │       └── FileSystemService.cs
│   │
│   └── EasySave.Console/           # Application console (point d'entrée)
│       ├── EasySave.Console.csproj
│       ├── Program.cs
│       ├── CompositionRoot.cs     # Configuration DI
│       ├── Cli/
│       │   └── CommandLineParser.cs
│       └── Resources/              # i18n FR/EN
│           ├── Strings.resx
│           └── Strings.fr.resx
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── DoD.md
│   └── DoR.md
├── EasySave.sln
└── README.md
```

**Remarque sur EasyLog** : La bibliothèque EasyLog est un projet séparé dans la solution. Pour un « choix réfléchi dans GIT », on peut soit la garder dans le même dépôt (dossier `src/EasyLog`) soit la placer dans un **dépôt Git dédié** et la référencer comme sous-module ou package NuGet privé, afin que ses évolutions restent compatibles avec EasySave v1.0.

Dans notre cas nous laissons EasyLog dans le même dépôt pour simplifier la gestion.

---

## 3. Dépendances entre projets

```
EasySave.Console     → EasySave.Core, EasySave.Infrastructure
EasySave.Infrastructure → EasySave.Core, EasyLog
EasySave.Core        → (aucune)
EasyLog              → (aucune)
```

Le domaine (Core) ne dépend d’aucun autre projet. L’infrastructure et la console dépendent des abstractions définies dans Core.

---

## 4. Flux principaux

### 4.1 Démarrage (ligne de commande)

1. `Program.Main(args)` → parsing des arguments (`CommandLineParser`).
2. Interprétation : `1-3` (sauvegardes 1 à 3) ou `1;3` (sauvegardes 1 et 3).
3. Chargement de la configuration (chemins, liste des 5 travaux max) via `IConfigurationRepository`.
4. Composition des services (DI) dans `CompositionRoot`.
5. Exécution des travaux demandés via `IBackupExecutor`.

### 4.2 Exécution d’un travail de sauvegarde

1. `IBackupExecutor.ExecuteAsync(jobIds)` pour chaque travail.
2. Pour chaque travail : résolution de la stratégie (`IBackupStrategy`) selon le type (complète / différentielle).
3. Énumération des fichiers éligibles via `IFileSystemService`.
4. Pour chaque fichier : copie, mise à jour de l’état temps réel (`IStateWriter`), écriture dans le log journalier (EasyLog).
5. Gestion des erreurs (temps de transfert négatif dans le log en cas d’erreur).

### 4.3 Fichiers produits (hors code)

**Configuration** : un fichier JSON (ex. `backup-config.json`) dont le chemin est défini par variable d’environnement ou convention (ex. répertoire de l’exécutable ou dossier utilisateur). Pas de chemin en dur type `c:\temp\`.

**Log journalier** : écrit par EasyLog, format JSON, un fichier par jour (ex. `2026-01-27.json`), avec retours à la ligne entre les entrées pour lecture dans Notepad.

**État temps réel** : un seul fichier (ex. `state.json`), mis à jour pendant les sauvegardes, même convention d’emplacement que la configuration.

---

## 5. Internationalisation (i18n)

Toutes les Ressources dans `EasySave.Console/Resources` (`.resx` pour FR et EN).

Langue déterminée par la langue du système ou variable d’environnement.

Tous les textes présentés à l’utilisateur passent par les ressources (aucune chaîne en dur dans la console).

---

## 6. Préparation à la version 2.0 (MVVM)

La logique métier et les cas d’usage sont dans **Core** et **Infrastructure** ; ils ne dépendent pas de la console.

En v2.0, on pourra ajouter un projet **EasySave.Wpf** (ou **EasySave.App**) qui :
  - Réutilise **EasySave.Core** et **EasySave.Infrastructure**.
  - Introduit des ViewModels qui appellent les mêmes interfaces (`IBackupExecutor`, `IConfigurationRepository`, etc.).
  - Conserve la même configuration, le même état et le même log (EasyLog), sans dupliquer la logique.

**EasySave.Console** reste un point d’entrée minimaliste qui utilise **Core** et **Infrastructure** pour la logique métier.

La séparation claire des responsabilités facilite cette transition vers une interface graphique sans réécrire la logique existante.

---

## 7. Récapitulatif des responsabilités

| Composant | Responsabilité |
|-----------|----------------|
| **EasyLog** | Écriture du log journalier (horodatage, source, destination, taille, temps de transfert), format JSON, un fichier par jour. |
| **EasySave.Core** | Modèle métier (BackupJob, BackupProgress, BackupType, BackupState) et contrats (interfaces). |
| **EasySave.Infrastructure** | Copie de fichiers, stratégies complète/différentielle, lecture/écriture configuration et état JSON, conversion chemins UNC. |
| **EasySave.Console** | Point d’entrée, parsing CLI, affichage, chargement des ressources i18n, composition (DI). |

Cette répartition respecte SOLID et permet une évolution maîtrisée vers la v2.0 avec interface graphique.

---

## 8. Exécution et configuration

### 8.1 Compilation et lancement

A venir...

### 8.2 Répertoire de base (config, log, état)

Aucun chemin en dur type `c:\temp\`. Le répertoire de base est déterminé dans l’ordre suivant :

1. Variable d’environnement **`EASYSAVE_BASE_PATH`** (recommandé en production)
2. Sinon : répertoire de l’exécutable (`AppContext.BaseDirectory`)

Dans ce répertoire sont créés ou lus :

- `backup-config.json` : liste des travaux (max 5), chemins, type (Full/Differential)
- `state.json` : état temps réel des travaux
- `yyyy-MM-dd.json` : log journalier (un fichier par jour, ex. `2026-01-27.json`)
