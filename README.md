# Orbit — Centralisateur d'applications et de jeux Windows

Orbit est un **launcher local** pour Windows 10/11. Il permet d'enregistrer des
fichiers `.exe` (applications ou jeux), de les organiser dans une bibliothèque et
de les lancer depuis une interface unique, moderne et fluide.

Le fichier d'origine n'est jamais déplacé ni copié : Orbit ne mémorise qu'un
chemin, quelques métadonnées et une icône mise en cache.

---

## Sommaire

- [Fonctionnalités](#fonctionnalités)
- [Technologies](#technologies)
- [Prérequis](#prérequis)
- [Installation](#installation)
- [Lancer en développement](#lancer-en-développement)
- [Compilation](#compilation)
- [Publication (version distribuable)](#publication-version-distribuable)
- [Structure du projet](#structure-du-projet)
- [Fonctionnement général](#fonctionnement-général)
- [Emplacement des données](#emplacement-des-données)
- [Tests](#tests)
- [Limitations connues](#limitations-connues)
- [Prochaines évolutions possibles](#prochaines-évolutions-possibles)

---

## Fonctionnalités

- **Ajouter** une application ou un jeu à partir d'un fichier `.exe` choisi dans
  l'explorateur Windows ; le nom, la description et l'éditeur sont pré-remplis
  automatiquement à partir des métadonnées du fichier.
- **Icône automatique** : l'icône du `.exe` est extraite une seule fois, convertie
  en PNG et mise en cache. Une icône par défaut est utilisée en cas d'échec.
- **Lancer** une application (via `ShellExecute`, sans passer par `cmd.exe`), avec
  gestion des chemins contenant des espaces ou des accents, du dossier de travail,
  d'arguments optionnels, des refus d'accès et des demandes d'élévation UAC.
- **Statistiques** : nombre de lancements et date du dernier lancement.
- **Modifier** / **retirer** une entrée (le `.exe` d'origine n'est jamais
  supprimé), avec confirmation configurable.
- **Détection des exécutables manquants** : une entrée dont le fichier n'existe
  plus est signalée clairement et n'est jamais supprimée automatiquement ; on peut
  corriger le chemin en un clic.
- **Recherche** instantanée (nom, catégorie, description).
- **Filtres** par section (Bibliothèque / Jeux / Applications / Favoris) et par
  catégorie.
- **Tri** par nom, ajout récent, nombre de lancements ou dernier lancement.
- **Favoris**.
- **Thème** clair / sombre / système, appliqué à chaud.
- **Persistance locale** en SQLite : les données survivent à la fermeture, au
  redémarrage du PC et à une mise à jour de l'application.
- **Journalisation** dans des fichiers rotatifs quotidiens.
- **Réinitialisation** des données depuis les paramètres, avec double confirmation.

---

## Technologies

| Domaine | Choix | Raison |
|---|---|---|
| Langage / runtime | **C# 12 / .NET 8 (LTS)** | Cible pérenne Windows 10/11, support long terme. |
| UI | **WPF** (`net8.0-windows`) | Le plus mûr pour une app bureautique Windows : styles/`DataTemplate`, liaison de données riche, déploiement simple, accès direct aux API Win32 (icônes, registre). WinUI 3 impose MSIX/WindowsAppSDK ; Avalonia est inutile ici car la cible est mono-plateforme. |
| Patron | **MVVM** | `CommunityToolkit.Mvvm` (générateurs source `[ObservableProperty]` / `[RelayCommand]`, `ObservableValidator`). |
| Données | **SQLite** via `Microsoft.Data.Sqlite` | Requêtes/tri/filtre côté base, transactions, robustesse supérieure à un JSON réécrit en entier ; fichier unique, zéro serveur. Migrations via `PRAGMA user_version`. |
| Icônes | `SHGetFileInfo` + `Icon.ExtractAssociatedIcon` + `System.Drawing` | Extraction native, conversion PNG, cache disque. |
| Injection de dépendances | `Microsoft.Extensions.Hosting` / `DependencyInjection` | Composition centralisée. |
| Journalisation | **Serilog** (`Sinks.File`, `Sinks.Debug`) | Fichiers rotatifs, format lisible. |
| Tests | **xUnit** | 60 tests unitaires et d'intégration. |

---

## Prérequis

- **Windows 10 (1809+) ou Windows 11**, 64 bits.
- Pour exécuter une version publiée *self-contained* : rien d'autre.
- Pour compiler / exécuter en développement : **SDK .NET 8** (`dotnet --version`
  doit renvoyer `8.x`). Installation possible via :

  ```bash
  winget install Microsoft.DotNet.SDK.8
  ```

---

## Installation

### À partir d'une version publiée

1. Récupérer le dossier `publish/` (voir [Publication](#publication-version-distribuable)).
2. Lancer `Orbit.exe`. Aucune installation, aucune clé de registre : Orbit écrit
   uniquement dans `%LOCALAPPDATA%\Orbit`.

### À partir des sources

```bash
git clone <url-du-dépôt>
cd "Claude PROJECT"
dotnet restore
dotnet build -c Release
```

---

## Lancer en développement

```bash
dotnet run --project src/Orbit.App
```

---

## Compilation

```bash
# Compilation complète de la solution
dotnet build Orbit.sln -c Release

# Tests
dotnet test
```

---

## Publication (version distribuable)

**Exécutable autonome, un seul fichier** (ne nécessite pas .NET sur la machine
cible) :

```bash
dotnet publish src/Orbit.App/Orbit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

**Version *framework-dependent*** (plus légère, nécessite le runtime .NET 8
Desktop sur la machine cible) :

```bash
dotnet publish src/Orbit.App/Orbit.App.csproj -c Release -r win-x64 --self-contained false -o publish
```

Le binaire produit s'appelle `Orbit.exe`.

---

## Structure du projet

```
Orbit.sln
│
├── src/
│   ├── Orbit.Core/                 # Logique métier — aucune référence WPF
│   │   ├── Models/                 # AppEntry, AppKind, AppAvailability, LibraryItem
│   │   ├── Infrastructure/         # OrbitPaths, PathHelper
│   │   ├── Data/                   # SqliteConnectionFactory, DatabaseInitializer,
│   │   │                           #   IAppRepository, SqliteAppRepository
│   │   ├── Services/               # ExecutableInspector, ProcessLauncher,
│   │   │                           #   IconService, JsonSettingsService, LibraryService
│   │   └── OrbitCoreServiceCollectionExtensions.cs
│   │
│   └── Orbit.App/                  # Présentation WPF (net8.0-windows)
│       ├── App.xaml(.cs)           # Bootstrap : host DI, Serilog, thème, exceptions globales
│       ├── MainWindow.xaml(.cs)    # Coquille : rail de navigation + contenu + barre d'état
│       ├── Infrastructure/         # OrbitLogging, ThemeManager
│       ├── Converters/             # Convertisseurs de binding
│       ├── Services/               # DialogService, AddAppFlow
│       ├── ViewModels/             # Main, Home, Library, AppTile, Settings, Add/Edit
│       ├── Views/                  # HomeView, LibraryView, SettingsView, AppTile, AppFormWindow
│       ├── Resources/              # Themes/Light.xaml, Themes/Dark.xaml, Controls.xaml
│       └── Assets/                 # orbit.ico, default-app-icon.png
│
└── tests/
    └── Orbit.Tests/               # xUnit : PathHelper, base SQLite, services, icônes, intégration
```

### Séparation des responsabilités

- **`Orbit.Core`** ne connaît pas WPF. Il pourrait être réutilisé par une autre
  interface (CLI, service…).
- **`Orbit.App`** ne contient aucune logique métier : les vues se lient à des
  view-models, qui appellent `ILibraryService`.
- L'accès aux données est derrière `IAppRepository`. Les services système
  (`IProcessLauncher`, `IExecutableInspector`, `IIconService`) sont tous
  interfacés, ce qui rend le cœur testable sans fichiers réels.

---

## Fonctionnement général

1. **Ajout** — L'utilisateur choisit un `.exe`. `ExecutableInspector` vérifie
   l'existence et l'extension, lit les métadonnées de version. `IconService`
   extrait l'icône (`SHGetFileInfo`, repli sur `ExtractAssociatedIcon`), la
   convertit en PNG et l'écrit dans le cache (clé = hachage du chemin + date de
   modification + taille). `LibraryService` refuse les doublons puis persiste
   l'entrée via `SqliteAppRepository`.
2. **Affichage** — `LibraryService.LoadAsync` renvoie chaque entrée accompagnée
   de sa disponibilité recalculée à partir du disque (`Available` / `Missing` /
   `Invalid`). La disponibilité n'est jamais stockée.
3. **Lancement** — `ProcessLauncher` valide le chemin, construit un
   `ProcessStartInfo { UseShellExecute = true }` (pas de `cmd.exe`), gère le
   dossier de travail et les arguments, puis classe les erreurs Win32
   (introuvable, accès refusé, UAC annulé…). En cas de succès, le compteur de
   lancements et l'horodatage sont mis à jour de façon atomique.
4. **Fichier manquant** — L'entrée reste visible, marquée « Fichier introuvable ».
   L'utilisateur peut corriger le chemin ou retirer l'entrée. Rien n'est
   supprimé automatiquement.
5. **Persistance** — SQLite en mode WAL dans `%LOCALAPPDATA%\Orbit\orbit.db`.
   Le schéma est versionné (`PRAGMA user_version`) pour permettre des migrations.

---

## Emplacement des données

Tout est écrit sous **`%LOCALAPPDATA%\Orbit\`** (jamais dans le dossier
d'installation, qui peut être en lecture seule) :

| Fichier / dossier | Contenu |
|---|---|
| `orbit.db` | Base SQLite (entrées de la bibliothèque). |
| `settings.json` | Thème, tri par défaut, préférences. Un fichier corrompu est mis de côté en `settings.json.corrupt-<date>` et remplacé par les valeurs par défaut. |
| `icons/` | Icônes PNG mises en cache. |
| `logs/orbit-<date>.log` | Journaux (14 jours conservés). |

---

## Tests

```bash
dotnet test
```

**60 tests** (xUnit), notamment :

- **`PathHelperTests`** — normalisation (guillemets, espaces, variables
  d'environnement, accents), comparaison de chemins insensible à la casse et au
  séparateur, jeton de cache déterministe.
- **`SqliteAppRepositoryTests`** — aller-retour de tous les champs, **persistance
  après recréation complète de la connexion** (simulation d'un redémarrage),
  déduplication insensible à la casse, statistiques de lancement, suppression,
  chemins accentués/avec espaces conservés à l'identique.
- **`DatabaseTests`** — création du schéma, `user_version`, initialisation
  idempotente.
- **`LibraryServiceTests`** — validation à l'ajout, refus des doublons,
  disponibilité « manquant », lancement comptabilisé uniquement en cas de succès,
  ré-extraction de l'icône seulement si le chemin change, réinitialisation.
- **`ProcessLauncherTests`** — `UseShellExecute` sans `cmd.exe`, correspondance
  des codes d'erreur Win32 (5 → accès refusé, 1223 → annulé par l'utilisateur…),
  abandon si le fichier est absent ou n'est pas un `.exe`.
- **`JsonSettingsServiceTests`** — création des valeurs par défaut, aller-retour,
  récupération après fichier corrompu.
- **`ExecutableInspectorTests`** / **`IconServiceTests`** — sur le **système de
  fichiers réel** et un **vrai exécutable Windows** : lecture des métadonnées,
  extraction d'icône PNG et vérification du cache.
- **`LibraryIntegrationTests`** — scénario complet de bout en bout sur la vraie
  pile (SQLite + inspecteur + icônes) : ajout → redémarrage → disparition du
  fichier → correction du chemin → lancement ; et réinitialisation qui n'altère
  jamais le `.exe` source.

Résultat de la dernière exécution : **60 réussis, 0 échec**.

En complément, l'application a été **réellement démarrée** (build Release) :
création de `orbit.db` + migration, écriture de `settings.json`, initialisation de
la coquille et arrêt propre, sans exception dans les journaux.

---

## Limitations connues

- **Windows uniquement.** `Orbit.Core` cible `net8.0-windows` (icônes Win32,
  registre, `System.Drawing`). Aucune portabilité macOS/Linux prévue.
- Les métadonnées « jeu » enrichies (genre, éditeur, jaquette, temps de jeu) sont
  **présentes dans le schéma** mais **pas exposées dans l'interface** de la V1.
- Pas encore de détection automatique des jeux installés (Steam, Epic, GOG…).
- Pas d'import/export de la bibliothèque.
- L'extraction d'icône fournit une image jusqu'à 32×32 ; les icônes « jumbo »
  256×256 ne sont pas encore récupérées.
- Les tests portent sur `Orbit.Core` ; la couche XAML est vérifiée par la
  compilation du markup et un démarrage réel, pas par des tests d'UI automatisés.

---

## Prochaines évolutions possibles

L'architecture a été prévue pour les accueillir sans refonte :

- Connecteurs de launchers : **Steam, Epic Games, GOG, Ubisoft Connect, Xbox**.
- Détection automatique des jeux et suivi du **temps de jeu**.
- Métadonnées et jaquettes de jeux (colonnes déjà présentes).
- Catégories personnalisées, profils, raccourcis clavier.
- Import / export de la bibliothèque.
- Thèmes personnalisés.
- Statistiques avancées.
```
