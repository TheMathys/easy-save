# EasySave Log Server

Service de centralisation des logs qui reçoit les entrées de log de sauvegarde envoyées par les clients EasySave via une **socket TCP** et les enregistre dans des fichiers JSON journaliers (même format que les logs locaux EasySave).

## Protocole

- **Transport** : TCP.
- **Format** : un objet JSON par ligne (newline-delimited JSON). Chaque ligne est une entrée de log avec la même structure que `LogEntry` EasySave 
- **Réponse** : pour chaque ligne reçue, le serveur envoie une ligne : `OK` ou `ERR`

### Structure JSON d’une entrée de log

```json
{
  "timeStamp": "2026-02-23T12:00:00Z",
  "backupName": "MyBackup",
  "sourcePath": "C:\\Source\\file.txt",
  "destinationPath": "D:\\Backup\\file.txt",
  "fileSizeBytes": 1024,
  "transferTimeMs": 150,
  "encryptionTimeMs": 0,
  "reason": null
}
```

- `transferTimeMs` : durée totale en millisecondes (nombre).
- `reason` : correspond à un arrêt par exemple lorsqu'un logiciel métier est lancé.

## Configuration (variables d’environnement)

| Variable           | Description                                    | Défaut   |
|--------------------|------------------------------------------------|----------|
| `LOG_SERVER_PORT`  | Port TCP d’écoute du serveur                   | `9050`   |
| `LOG_DIR`          | Répertoire des fichiers de log journaliers (UTC) | `/logs` |

## Compilation et exécution (en local)

Depuis la racine du dépôt :

```bash
dotnet build src/EasySave.LogServer/EasySave.LogServer.csproj -c Release
dotnet run --project src/EasySave.LogServer/EasySave.LogServer.csproj -c Release
```

Ou en définissant les options via les variables d’environnement :

```bash
$env:LOG_SERVER_PORT=9050; $env:LOG_DIR="./logs"; dotnet run --project src/EasySave.LogServer/EasySave.LogServer.csproj
```

## Docker

### Build et exécution avec Docker

Depuis la racine du dépôt :

```bash
docker build -f src/EasySave.LogServer/Dockerfile -t easysave-logserver .
docker run -p 9050:9050 -v easysave-logs:/logs easysave-logserver
```

### Build et exécution avec Docker Compose

Depuis la racine du dépôt :

```bash
docker-compose up -d
```

Cela construit l’image, démarre le service et monte un volume pour la persistance des logs. Le port de la socket est exposé sur `9050` (ou la valeur de `LOG_SERVER_PORT` sur l’hôte).

Pour utiliser un autre port sur l’hôte :

```bash
LOG_SERVER_PORT=9090 docker-compose up -d
```

### Persistance

Les logs sont stockés dans un volume Docker nommé `logdata` (voir `docker-compose.yml`). Pour l’inspecter :

```bash
docker volume inspect easysave_logdata
```

Pour lister le contenu du répertoire de logs dans un conteneur éphémère :

```bash
docker run --rm -v easysave_logdata:/logs alpine ls -la /logs
```

## Architecture (SOLID / GoF)

- **ILogEntryHandler** : contrat pour traiter une entrée de log (validation + persistance). Implémenté par `LogEntryHandler` (adaptation DTO → `LogEntry` + `ILogWriter`).
- **LogSocketListener** : accepte les connexions TCP et traite le JSON une ligne à la fois ; délègue chaque ligne à `ILogEntryHandler`. Une tâche par client (connexions concurrentes).
- **DailyLogWriter** (EasyLog) : fichiers JSON journaliers thread-safe ; un fichier par date UTC (`yyyy-MM-dd.json`), même format que les logs locaux EasySave.
