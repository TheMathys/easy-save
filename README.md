## EasySave – Guide utilisateur (FR)

### 1. Objectif du logiciel

EasySave est un outil de sauvegarde de fichiers pensé pour des usages professionnels ou avancés :

- sauvegardes complètes et différentielles de dossiers,
- exécution de plusieurs travaux (jobs) en parallèle,
- suivi temps réel de l’avancement,
- logs détaillés par fichier,
- chiffrement optionnel de certains types de fichiers,
- blocage automatique des sauvegardes si un logiciel métier est en cours d’utilisation,
- interface console (TUI) et interface graphique (GUI).

Les fonctionnalités métier (stratégies de sauvegarde, format de configuration, état temps réel, logs) sont communes à la console et à la GUI.

### 2. Projets et éditions

- `src/EasySave.Console` : application console (TUI + CLI).
- `src/EasySave.Gui` : application graphique (GUI Avalonia).
- `src/EasySave.Core` : cœur métier (entités, interfaces).
- `src/EasySave.Infrastructure` : implémentations fichiers, JSON, chiffrement, exécution des jobs.
- `src/EasyLog` : bibliothèque de logs journaliers (JSON / XML).
- `src/EasySave.LogServer` : service de centralisation des logs.

Vous pouvez utiliser indifféremment la Console ou la GUI sur les mêmes fichiers de configuration (`backup-config.json`).

### 3. Fichiers importants et répertoire de base

EasySave travaille toujours à partir d’un **répertoire de base** (base path). Dans ce répertoire, on trouve :

- `backup-config.json` : configuration globale (liste des jobs, paramètres globaux).
- `state.json` : état temps réel des sauvegardes.
- `yyyy-MM-dd.json` ou `yyyy-MM-dd.xml` : logs journaliers locaux.

Ordre de résolution du répertoire de base au démarrage :

1. **Variable d’environnement `EASYSAVE_BASE_PATH`**  
   Si définie et que le dossier existe, c’est la source de vérité pour Console et GUI.
2. **Chemin mémorisé par la GUI** (si vous avez utilisé « Change folder… » et que ce dossier existe encore).  
   Ce chemin est enregistré dans `%LocalAppData%\EasySave\gui-basepath.txt`.
3. **Dossier de l’exécutable** (`AppContext.BaseDirectory`) si rien d’autre n’est indiqué.

En pratique :

- En production, il est recommandé de définir `EASYSAVE_BASE_PATH` pour maîtriser précisément l’emplacement des fichiers de configuration / état / logs.
- Si vous travaillez avec la GUI uniquement, le bouton « Change folder… » (Paramètres) vous permet de déplacer ces fichiers dans un autre dossier utilisateur (par exemple dans `Documents\EasySave`).

### 4. Format du fichier `backup-config.json`

Le fichier de configuration est un JSON lisible comprenant notamment :

- `logAndStateDirectory` : dossier où écrire `state.json` et les logs journaliers (utilisé par l’infrastructure).
- `logFileFormat` : `"Json"` ou `"Xml"`.
- `logDestination` : `"Local"`, `"Centralized"` ou `"LocalAndCentralized"`.
- `centralizedLogServerAddress` : adresse du serveur de logs centralisé (`host` ou `host:port`).
- `encryptExtensions` : liste d’extensions à chiffrer (ex. `[".doc", ".pdf"]`).
- `priorityExtensions` : liste d’extensions prioritaires lorsque plusieurs jobs tournent.
- `encryptionKeyPath` : chemin du fichier de clé de chiffrement.
- `businessSoftwareProcessName` : nom du processus métier à surveiller (ex. `"Excel"` pour `Excel.exe`).
- `useDarkTheme` / `textScalePercent` : préférences d’affichage de la GUI.
- `largeFileThresholdKb` : seuil de « gros fichiers » pour la limitation de concurrence.
- `jobs` : tableau des travaux de sauvegarde (id, nom, source, cible, type, exclusions).

Le fichier est maintenu par la Console (TUI) et par la GUI. Vous pouvez l’éditer à la main si besoin, mais il est conseillé de passer par l’interface pour éviter les erreurs de format.

### 5. Utilisation – version Console

#### 5.1. Lancement du TUI

Dans un terminal positionné sur le dossier de publication :

```bash
EasySave.exe
```

Sans argument ou avec `--tui`, la Console démarre en mode TUI (menu interactif) :

- créer un travail de sauvegarde,
- lister les travaux,
- lancer des sauvegardes,
- supprimer / modifier un travail,
- voir les chemins (config / logs).

#### 5.2. Lancement direct en ligne de commande (CLI)

Vous pouvez exécuter des jobs directement depuis la ligne de commande, sans passer par le menu, en indiquant les identifiants de jobs :

```bash
EasySave.exe 1-3       # jobs 1, 2, 3
EasySave.exe 1;3;5     # jobs 1, 3 et 5
EasySave.exe 1,3,5     # jobs 1, 3 et 5
EasySave.exe 1~3;5     # jobs 1, 2, 3 et 5
```

Rappels :

- les IDs doivent exister dans `backup-config.json`, sinon ils sont ignorés,
- plusieurs jobs peuvent s’exécuter **en parallèle**,
- `Ctrl+C` permet d’annuler proprement l’exécution en cours.

### 6. Utilisation – version GUI

#### 6.1. Lancement

Publiez puis lancez l’exécutable GUI (par exemple `EasySave.Gui.exe`).  
Au démarrage, la GUI :

- résout le répertoire de base (voir section 3),
- charge `backup-config.json` s’il existe,
- affiche la liste des jobs dans l’onglet « Jobs ».

Vous pouvez aussi lancer la GUI en lui passant des IDs de jobs à exécuter dès l’ouverture (même format que la Console) :

```bash
EasySave.Gui.exe 1-3
EasySave.Gui.exe 1;3
```

Les jobs demandés démarrent automatiquement, et vous voyez leur progression dans l’onglet Jobs.

#### 6.2. Onglet « Jobs »

Principales actions :

- **Lister les travaux** : chaque job affiche son id, son nom, son type (Full / Differential) et son état (Inactif, En cours, En pause, Terminé, Erreur).
- **Créer / modifier un job** : via l’onglet dédié (nom, répertoire source, répertoire cible, type, exclusions).
- **Lancer un ou plusieurs jobs** : sélectionnez-en un ou plusieurs, puis utilisez le bouton de lancement.
- **Pause / reprise / arrêt** : lorsque des jobs tournent, vous pouvez :
  - mettre en pause certains jobs,
  - les reprendre,
  - arrêter proprement les sauvegardes en cours.
- **Progression détaillée** :
  - pour chaque job : nombre de fichiers, taille totale, fichier en cours, estimation de temps restant.

Les règles de priorité et de gros fichiers (extensions prioritaires, seuil en kilo‑octets) sont appliquées par l’infrastructure lorsque plusieurs jobs tournent en parallèle.

#### 6.3. Onglet « Settings »

Cet onglet regroupe les paramètres globaux, dont plusieurs ont un impact important sur la sécurité et les performances.

Principales sections :

- **Chemins** :
  - Base path (dossier de base),
  - chemin du fichier de configuration (`backup-config.json`),
  - chemin du fichier d’état (`state.json`),
  - dossier des logs journaliers.
  - bouton **« Change folder… »** :
    - copie `backup-config.json`, `state.json` et les logs du dossier actuel vers le nouveau,
    - enregistre ce nouveau chemin pour les prochains démarrages (sauf si `EASYSAVE_BASE_PATH` est défini),
    - recharge la configuration.

- **Format et destination des logs** :
  - **Format** : JSON ou XML.
  - **Destination** :
    - Local only,
    - Centralized server only,
    - Local and centralized.
  - **Serveur de logs centralisé** :
    - `host` ou `host:port` du `EasySave.LogServer`.

- **Extensions à chiffrer** :
  - liste d’extensions de fichiers (par exemple `.doc, .pdf`),
  - seuls les fichiers correspondant à ces extensions sont passés par l’outil de chiffrement externe (CryptoSoft).

- **Extensions prioritaires** :
  - extensions de fichiers qui doivent être traitées en priorité lorsqu’il y a plusieurs jobs en parallèle,
  - tant qu’il reste au moins un fichier « prioritaire » à transférer, aucun fichier non prioritaire n’est pris en charge.

- **Seuil des gros fichiers** :
  - valeur en kilo‑octets,
  - si définie, limite le nombre de gros fichiers transférés simultanément à 1 (les autres attendent), pour ne pas saturer le disque ou le réseau.

- **Chemin de la clé de chiffrement** :
  - chemin complet du fichier de clé utilisé par l’outil de chiffrement externe.
  - **Pourquoi définir une clé de chiffrement ici ?**
    - pour chiffrer automatiquement certains types de fichiers sensibles (documents bureautiques, PDF, archives, etc.),
    - pour centraliser ce paramètre de sécurité dans la configuration, au lieu de le passer manuellement à chaque commande,
    - pour garantir que toutes les sauvegardes d’un même poste utilisent la même clé (cohérence).
  - **Bonnes pratiques** :
    - stocker la clé dans un emplacement protégé (droit d’accès restreint),
    - ne jamais commiter ce fichier dans un dépôt Git,
    - prévoir un plan de rotation de clé si la sécurité l’exige.
  - Si ce champ est vide, **aucun chiffrement** n’est effectué, même si des extensions sont listées dans `encryptExtensions`.

- **Logiciel métier** :
  - nom du processus à surveiller (par exemple `Excel` pour `Excel.exe`),
  - si ce processus est actif, EasySave bloque le démarrage des sauvegardes ou interrompt proprement les jobs,
  - cela évite de sauvegarder des fichiers susceptibles d’être en cours d’édition dans une application critique.

- **Préférences d’affichage** :
  - thème clair / sombre,
  - échelle de texte (75 %, 100 %, 125 %, 150 %),
  - langue (français / anglais),
  - audio‑description et volume (accessibilité).

### 7. Service de centralisation des logs

Le projet `EasySave.LogServer` permet de recevoir les logs de plusieurs instances EasySave et de les stocker dans un dossier unique sur un serveur.

- Projet : `src/EasySave.LogServer/`
- Exécution habituelle avec Docker : `docker-compose up -d`  
  (voir `src/EasySave.LogServer/README.md` pour les détails de build, configuration et protocole).

La GUI et la Console peuvent envoyer leurs logs vers ce serveur en configurant :

- `logDestination = Centralized` ou `LocalAndCentralized`,
- `centralizedLogServerAddress` dans la configuration.

### 8. Publication (build de distribution)

Ne publiez pas la solution `EasySave.sln` complète : cela sortirait tous les projets (y compris les tests) dans un même dossier, sans exécutable exploitable.

Pour publier l’application console seule :

```bash
dotnet publish src/EasySave.Console/EasySave.Console.csproj -c Release -o publish
```

Le dossier `publish/` contiendra `EasySave.exe` et toutes les DLL nécessaires.

Pour publier la GUI seule :

```bash
dotnet publish src/EasySave.Gui/EasySave.Gui.csproj -c Release -o publish-gui
```

### 9. Definition of Ready (DoR) et Definition of Done (DoD)

Le projet applique une Definition of Ready (DoR) et une Definition of Done (DoD) communes à toutes les tâches et Pull Requests.

- DoR : critères à remplir avant de démarrer une tâche ou une évolution.  
  Voir `docs/DoR.md`.
- DoD : critères à remplir avant de considérer une tâche comme terminée.  
  Voir `docs/DoD.md`.

### 10. Dépannage rapide

- **Mes jobs disparaissent au lancement** :
  - vérifier que vous éditez le bon `backup-config.json` (celui du répertoire de base effectivement utilisé),
  - vérifier que `EASYSAVE_BASE_PATH` n’impose pas un autre dossier,
  - vérifier les permissions d’écriture dans le dossier.

- **Les logs ne sont pas générés** :
  - vérifier le répertoire de base et le champ `logAndStateDirectory`,
  - vérifier que le format log (`Json` / `Xml`) et la destination sont correctement configurés.

- **Le chiffrement ne semble pas actif** :
  - vérifier que `encryptExtensions` contient bien les extensions attendues,
  - vérifier que `encryptionKeyPath` pointe vers un fichier existant,
  - vérifier que l’exécutable CryptoSoft est accessible et fonctionne en ligne de commande.
