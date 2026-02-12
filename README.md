# Projet Easy Save

## Logs (JSON / XML)

EasySave écrit un log détaillé de chaque fichier copié dans un fichier quotidien.  
Depuis la version 1.1, l’utilisateur peut choisir :

- le format **JSON** : fichiers `yyyy-MM-dd.json`
- ou le format **XML** : fichiers `yyyy-MM-dd.xml`

Le choix du format est stocké dans la configuration (`backup-config.json`) et peut être modifié directement depuis le menu TUI, option « View paths (config and logs) ».

## Publication (build de distribution)

**Ne pas publier la solution** (`EasySave.sln`) : cela publie tous les projets (y compris les tests) dans le même dossier et aucun exécutable utilisable n’est produit.

Pour obtenir un dossier prêt à l’emploi avec `EasySave.exe` et ses dépendances :

```bash
dotnet publish src/EasySave.Console/EasySave.Console.csproj -c Release -o publish
```

Le dossier `publish/` à la racine du dépôt contiendra alors `EasySave.exe` et les DLL nécessaires.

Sous Visual Studio / Cursor : clic droit sur le projet **EasySave.Console** (et non sur la solution) → **Publish** → choisir le profil **FolderProfile** si proposé. Le résultat sera dans le dossier `publish/` à la racine.

## Definition of Ready (DoR)

Afin de garantir un niveau de qualité homogène, le projet applique une **Definition of Ready (DoR)** commune à toutes les tâches proposées.

Toute tâches doit respecter l’ensemble de ces critères avant d’être considérée comme prête.

📄 La DoR est définie ici :  
➡️ [docs/DoR.md](docs/DoR.md)

## Definition of Done (DoD)

Afin de garantir un niveau de qualité homogène, le projet applique une **Definition of Done (DoD)** commune à toutes les tâches et Pull Requests.

Toute issue ou PR doit respecter l’ensemble de ces critères avant d’être considérée comme terminée.

📄 La DoD est définie ici :  
➡️ [docs/DoD.md](docs/DoD.md)
