## EasySave – User Guide (EN)

### 1. Purpose of the application

EasySave is a file backup tool designed for professional or advanced use cases:

- full and differential backups of folders,
- execution of several backup jobs in parallel,
- real‑time progress tracking,
- detailed per‑file logging,
- optional encryption of selected file types,
- automatic blocking of backups while a “business” application is running,
- both a console interface (TUI/CLI) and a graphical interface (GUI).

The core backup logic (backup strategies, configuration format, real‑time state, logging) is shared between the Console and the GUI.

### 2. Projects and editions

- `src/EasySave.Console`: console application (TUI + CLI).
- `src/EasySave.Gui`: graphical application (Avalonia GUI).
- `src/EasySave.Core`: domain model and interfaces.
- `src/EasySave.Infrastructure`: file system, JSON persistence, encryption, backup execution.
- `src/EasyLog`: reusable daily log library (JSON / XML).
- `src/EasySave.LogServer`: central log server.

You can use the Console or the GUI against the same configuration files (`backup-config.json`).

### 3. Important files and base directory

EasySave always works from a **base directory**. In this directory you will find:

- `backup-config.json`: global configuration (jobs list, global options).
- `state.json`: real‑time backup state.
- `yyyy-MM-dd.json` or `yyyy-MM-dd.xml`: local daily log files.

At startup, the base directory is resolved in the following order:

1. **Environment variable `EASYSAVE_BASE_PATH`**  
   If defined and the directory exists, it is used by both Console and GUI.
2. **Last base path chosen from the GUI** (via the “Change folder…” button), if the directory still exists.  
   This path is stored in `%LocalAppData%\EasySave\gui-basepath.txt`.
3. **Executable directory** (`AppContext.BaseDirectory`) if nothing else is set.

In practice:

- In production, it is recommended to set `EASYSAVE_BASE_PATH` to control exactly where configuration / state / logs are stored.
- If you mainly use the GUI, you can use the “Change folder…” button in the Settings tab to move all these files to a user‑friendly folder (for example `Documents\EasySave`).

### 4. `backup-config.json` format

The configuration file is a human‑readable JSON document containing in particular:

- `logAndStateDirectory`: directory used to write `state.json` and daily logs.
- `logFileFormat`: `"Json"` or `"Xml"`.
- `logDestination`: `"Local"`, `"Centralized"` or `"LocalAndCentralized"`.
- `centralizedLogServerAddress`: central log server address (`host` or `host:port`).
- `encryptExtensions`: list of file extensions to encrypt (for example `[".doc", ".pdf"]`).
- `priorityExtensions`: list of priority extensions when several jobs run in parallel.
- `encryptionKeyPath`: path to the encryption key file.
- `businessSoftwareProcessName`: process name of the business software to monitor (for example `"Excel"` for `Excel.exe`).
- `useDarkTheme` / `textScalePercent`: GUI display preferences.
- `largeFileThresholdKb`: threshold above which a file is considered “large” for concurrency throttling.
- `jobs`: array of backup jobs (id, name, source, target, type, exclusions).

The file is maintained by the Console (TUI) and the GUI. You can edit it by hand if needed, but it is generally safer to use the UI to avoid format errors.

### 5. Console usage

#### 5.1. TUI mode

From a terminal in the publish directory:

```bash
EasySave.exe
```

With no arguments or with `--tui`, the Console starts in TUI mode (interactive menu):

- create a backup job,
- list existing jobs,
- run backups,
- delete or edit a job,
- view configuration and log paths.

#### 5.2. Direct CLI mode

You can run jobs directly from the command line without using the menu, by specifying job identifiers:

```bash
EasySave.exe 1-3       # jobs 1, 2, 3
EasySave.exe 1;3;5     # jobs 1, 3 and 5
EasySave.exe 1,3,5     # jobs 1, 3 and 5
EasySave.exe 1~3;5     # jobs 1, 2, 3 and 5
```

Notes:

- IDs must exist in `backup-config.json`; otherwise they are ignored.
- Multiple jobs can run **in parallel**.
- `Ctrl+C` cancels the current execution gracefully.

### 6. GUI usage

#### 6.1. Starting the GUI

Publish then launch the GUI executable (for example `EasySave.Gui.exe`).  
At startup, the GUI:

- resolves the base directory (see section 3),
- loads `backup-config.json` if it exists,
- displays the job list in the “Jobs” tab.

You can also start the GUI with job IDs to run immediately (same syntax as the Console):

```bash
EasySave.Gui.exe 1-3
EasySave.Gui.exe 1;3
```

The requested jobs are started automatically, and their progress appears in the Jobs tab.

#### 6.2. “Jobs” tab

Main actions:

- **List jobs**: each job shows its id, name, type (Full / Differential) and state (Inactive, Active, Paused, Completed, Error).
- **Create / edit a job**: via the dedicated tab (name, source, target, type, exclusions).
- **Run one or more jobs**: select one or more jobs and click the run button.
- **Pause / resume / stop**:
  - pause a subset of running jobs,
  - resume paused jobs,
  - stop running backups cleanly.
- **Detailed progress**:
  - per job: total file count, total size, current file, time estimate.

Priority extensions and large file thresholds (see section 6.3) are enforced by the infrastructure when multiple jobs run in parallel.

#### 6.3. “Settings” tab

The Settings tab groups all global options. Some of them have a strong impact on security and performance.

Key sections:

- **Paths**:
  - base path (current base directory),
  - configuration file path (`backup-config.json`),
  - state file path (`state.json`),
  - log directory.
  - **“Change folder…”** button:
    - copies `backup-config.json`, `state.json` and all logs from the old directory to the new one,
    - saves the new base path for next launches (unless `EASYSAVE_BASE_PATH` is set),
    - reloads the configuration.

- **Log format and destination**:
  - **Format**: JSON or XML.
  - **Destination**:
    - Local only,
    - Centralized server only,
    - Local and centralized.
  - **Centralized log server**:
    - `host` or `host:port` of the `EasySave.LogServer` instance.

- **Extensions to encrypt**:
  - list of file extensions (for example `.doc, .pdf`),
  - only files whose extension appears in this list are passed to the external encryption tool (CryptoSoft).

- **Priority extensions**:
  - extensions that should be processed first when multiple jobs run in parallel,
  - as long as at least one “priority” file remains to be transferred, non‑priority files are delayed.

- **Large file threshold**:
  - threshold in kilobytes,
  - when set, at most one “large” file (size strictly above the threshold) can be transferred at a time across all jobs.

- **Encryption key path**:
  - absolute path to the key file used by the external encryption tool.
  - **Why store the encryption key path here?**
    - to automatically encrypt sensitive files (documents, PDFs, archives, etc.) during backups,
    - to centralize this security parameter instead of passing it manually for each run,
    - to ensure that all backups on a given machine use the same key (consistency).
  - **Security best practices**:
    - store the key file in a protected location (restricted access),
    - never commit the key file into source control,
    - plan for key rotation if your security policy requires it.
  - If this field is empty, **no encryption** is performed, even if `encryptExtensions` contains extensions.

- **Business software**:
  - name of the process to monitor (for example `Excel` for `Excel.exe`),
  - if this process is running, EasySave prevents backups from starting or stops them gracefully,
  - this avoids backing up files that may be open in a critical application.

- **Display and accessibility**:
  - light / dark theme,
  - text scale (75 %, 100 %, 125 %, 150 %),
  - language (French / English),
  - audio description and volume.

### 7. Centralized log server

The `EasySave.LogServer` project can receive logs from several EasySave instances and store them in a single directory on a server.

- Project: `src/EasySave.LogServer/`
- Usual Docker command: `docker-compose up -d`  
  (see `src/EasySave.LogServer/README.md` for build, configuration and protocol details).

Console and GUI can send their logs to this server when:

- `logDestination` is set to `Centralized` or `LocalAndCentralized`,
- `centralizedLogServerAddress` is configured.

### 8. Publishing (distribution build)

Do not publish the whole `EasySave.sln` solution: this would output all projects (including tests) in the same folder with no usable entry point.

To publish the Console application only:

```bash
dotnet publish src/EasySave.Console/EasySave.Console.csproj -c Release -o publish
```

The `publish/` directory will contain `EasySave.exe` and all required DLLs.

To publish the GUI only:

```bash
dotnet publish src/EasySave.Gui/EasySave.Gui.csproj -c Release -o publish-gui
```

### 9. Definition of Ready (DoR) and Definition of Done (DoD)

The project uses a Definition of Ready (DoR) and a Definition of Done (DoD) for all tasks and pull requests.

- DoR: criteria that must be met before starting a task.  
  See `docs/DoR.md`.
- DoD: criteria that must be met before considering a task finished.  
  See `docs/DoD.md`.

### 10. Troubleshooting

- **Jobs disappear at startup**:
  - ensure you are editing the correct `backup-config.json` (from the actual base directory),
  - verify whether `EASYSAVE_BASE_PATH` forces another directory,
  - check file system permissions on the configuration folder.

- **Logs are missing**:
  - check the base directory and `logAndStateDirectory`,
  - verify the configured log format (`Json` / `Xml`) and destination.

- **Encryption seems inactive**:
  - confirm that `encryptExtensions` contains the expected extensions,
  - confirm that `encryptionKeyPath` points to an existing key file,
  - verify that the CryptoSoft executable is installed and works from the command line.

