# CLAUDE.md — DJMAX Random Selector V

## Project Overview

DJMAX Random Selector V is a Windows desktop application that randomly selects music in the rhythm game **DJMAX RESPECT V**. The user sets filter criteria (button modes, difficulty, level range, categories), and the app picks a song and automatically navigates to it in the game via simulated keyboard input.

## Solution Structure

```
DjmaxRandomSelectorV.sln
├── DjmaxRandomSelectorV/           # WPF application (net6.0-windows, WinExe)
└── Dmrsv.RandomSelector/           # Core library (net6.0-windows)
```

### `Dmrsv.RandomSelector` — Core Library

Platform-agnostic logic for filtering and selecting music patterns.

```
Dmrsv.RandomSelector/
├── Track.cs                # Immutable record: music metadata + patterns array + IsPlayable
├── Pattern.cs              # Immutable record: MusicInfo + Button + Difficulty + Level
├── MusicInfo.cs            # Shared music metadata (Id, Title, Composer, Category)
├── IFilter.cs              # Interface: Filter(IEnumerable<Track>) → IEnumerable<Pattern>
├── FilterBase.cs           # Base class; tracks IsUpdated flag
├── BasicFilter.cs          # Query-based filter (button tunes, difficulties, categories, level range)
├── AdvancedFilter.cs       # Playlist-based filter (explicit ObservableCollection<Pattern>)
├── ISelector.cs            # Interface: Select(IList<Pattern>) → Pattern?
├── SelectorBase.cs         # Base random selector
├── SelectorWithHistory.cs  # Excludes recently played tracks from selection
├── IHistory.cs             # Interface for a bounded FIFO track history
├── History.cs              # Bounded queue of recent track IDs
├── PatternPicker.cs        # Applies a pick strategy (lowest/highest level per button, or free mode)
├── Locator.cs              # Win32 SendInput automation to navigate in-game
├── LocationInfo.cs         # Pre-computed navigation data for a track
├── TitleComparer.cs        # Sort comparer (Korean/English/symbols)
└── Enums/
    ├── ButtonTunes.cs      # B4, B5, B6, B8
    ├── Difficulty.cs       # NM, HD, MX, SC
    ├── FilterType.cs       # Query, Playlist
    ├── InputMethod.cs      # Default, WithAutoStart, NotInput
    ├── LevelPreference.cs  # None, Lowest, Highest
    └── MusicForm.cs        # Default (Freestyle), Free (Online)
```

### `DjmaxRandomSelectorV` — WPF Application

MVVM application built with Caliburn.Micro.

```
DjmaxRandomSelectorV/
├── App.xaml / App.xaml.cs
├── Bootstrapper.cs             # DI container (SimpleContainer), app startup/shutdown
├── RandomSelector.cs           # Orchestrates filter → pick → locate pipeline
├── TrackDB.cs                  # Loads and manages all tracks; handles DLC ownership
├── ExecutionHelper.cs          # Win32 hotkey registration + WndProc message hook
├── FileManager.cs              # JSON import/export + HTTP requests
├── UpdateManager.cs            # Version check; downloads AllTrackList.json + appdata.json
├── CategoryContainer.cs        # Holds the list of DLC categories
├── Dmrsv3Configuration.cs      # User configuration model (serialized to Config.json)
├── Dmrsv3AppData.cs            # App-data model (deserialized from appdata.json)
├── VersionContainer.cs         # Tracks current and latest app/data versions
├── WindowTitleHelper.cs        # Checks that the foreground window is DJMAX RESPECT V
├── Messages/                   # Caliburn.Micro IEventAggregator messages
│   ├── FilterMessage.cs        # record — carries IFilter to RandomSelector
│   ├── FilterOptionMessage.cs  # record — RecentsCount, MusicForm, InputMethod, LevelPreference
│   ├── SettingMessage.cs       # class  — FilterType, InputInterval, OwnedDlcs, SavesExclusion
│   ├── FavoriteMessage.cs      # record — Favorite list + Blacklist
│   ├── PatternMessage.cs       # record — the selected Pattern (for history display)
│   └── VArchiveMessage.cs      # record — command + items from V-Archive Wizard
├── Models/
│   ├── Category.cs             # record: Name, Id, SteamId, Type
│   ├── FavoriteItem.cs
│   ├── HistoryItem.cs
│   ├── LevelIndicator.cs       # UI helper for level range display
│   ├── LinkDiscItem.cs         # DLC dependency data
│   ├── ListUpdater.cs          # Syncs toggle state between a string list and an ObservableCollection
│   ├── Playlist.cs / PlaylistItem.cs
│   ├── PliCategory.cs          # PLI (playlist-info) category with minor sub-categories
│   └── VArchivePatternItem.cs
├── ViewModels/
│   ├── ShellViewModel.cs               # Root conductor (MainPanel + FilterOptionIndicator + FilterOptionPanel)
│   ├── MainViewModel.cs                # Switches between BasicFilterViewModel / AdvancedFilterViewModel
│   ├── BasicFilterViewModel.cs         # Query filter UI (buttons, difficulties, categories, levels)
│   ├── AdvancedFilterViewModel.cs      # Playlist filter UI (search, add, remove, sort)
│   ├── FilterOptionViewModel.cs        # Mode/Aider/Level preference + recents count
│   ├── FilterOptionIndicatorViewModel.cs
│   ├── HistoryViewModel.cs
│   ├── FavoriteViewModel.cs
│   ├── SettingViewModel.cs             # DLC selection, input delay, hotkey
│   ├── InfoViewModel.cs
│   └── VArchiveWizardViewModel.cs      # Imports patterns from the V-Archive website
├── Views/                      # Paired *.xaml + *.xaml.cs for each ViewModel above
├── DMRSV3_Data/                # Runtime data (CopyToOutputDirectory: PreserveNewest)
│   ├── AllTrackList.json       # Downloaded from V-Archive API
│   ├── appdata.json            # Downloaded from GitHub; categories + DLC + link-disc data
│   ├── Config.json             # User settings (owned DLCs, hotkey, filter options…)
│   ├── CurrentFilter.json      # Persisted basic-filter state
│   └── CurrentPlaylist.json    # Persisted advanced-filter playlist
├── Data/                       # Source/reference data (not copied to output)
│   ├── AllTrackList.csv
│   ├── Config.json
│   ├── CurrentFilter.json
│   ├── CurrentPlaylist.json
│   ├── Playlist/               # Example playlist presets
│   └── Preset/                 # Example filter presets (Ladder Match seasons)
├── Images/                     # PNG image resources
└── Fonts/                      # Lato, BebasNeue font resources
```

## Key Architectural Concepts

### MVVM with Caliburn.Micro

- View naming: `FooView.xaml` pairs automatically with `FooViewModel.cs`.
- All ViewModels registered per-request in `Bootstrapper.Configure()` via reflection (`Name.EndsWith("ViewModel")`).
- Singletons (`IWindowManager`, `IEventAggregator`, `IFileManager`, `Dmrsv3Configuration`, `TrackDB`, `CategoryContainer`, `VersionContainer`) registered as instances or singleton services.
- `IoC.Get<T>()` resolves dependencies from inside ViewModels when constructor injection is impractical.

### Event Aggregator (messaging)

Cross-component communication uses `IEventAggregator` with typed message records/classes:

| Message | Direction | Purpose |
|---|---|---|
| `FilterMessage` | ViewModel → `RandomSelector` | Active filter changed |
| `FilterOptionMessage` | ViewModel → `RandomSelector` + `ExecutionHelper` | Mode/aider/level/recents options changed |
| `SettingMessage` | `SettingViewModel` → `RandomSelector` + others | DLC list, input delay, filter type |
| `FavoriteMessage` | `FavoriteViewModel` → `BasicFilterViewModel` | Favorite/blacklist updated |
| `PatternMessage` | `RandomSelector` → `HistoryViewModel` | A pattern was selected |
| `VArchiveMessage` | `VArchiveWizardViewModel` → `AdvancedFilterViewModel` | Import patterns or close wizard |

### Selection Pipeline

On hotkey press (default **F7**):

```
ExecutionHelper.HwndHook()
  └─► RandomSelector.Start()
        ├─ IFilter.Filter(TrackDB.Playable)   → IEnumerable<Pattern>
        ├─ PatternPicker.Pick(patterns)        → optionally deduplicate by level preference
        ├─ SelectorWithHistory.Select(list)    → randomly choose, excluding recent track IDs
        └─ Locator.Locate(pattern)             → SendInput to navigate in-game
```

Alt+hotkey repeats the **last selected** pattern without re-rolling.

### Pattern Identity

```csharp
PatternId = TrackId * 100 + (int)Button * 10 + (int)Difficulty
// e.g., Track 42, 4B NM → 4200
Style = Button.AsString() + Difficulty.AsString()
// e.g., "4BNM", "8BSC"
```

Playlist files persist arrays of `PatternId` integers.

### Data Files at Runtime

| File | Source | Purpose |
|---|---|---|
| `DMRSV3_Data/AllTrackList.json` | Downloaded from `https://v-archive.net/db/songs.json` | Full track/pattern database |
| `DMRSV3_Data/appdata.json` | Downloaded from GitHub repo | Category definitions, DLC codes, link-disc rules |
| `DMRSV3_Data/Config.json` | Written on app exit | User preferences |
| `DMRSV3_Data/CurrentFilter.json` | Written when BasicFilter closes | Saved filter state |
| `DMRSV3_Data/CurrentPlaylist.json` | Written when AdvancedFilter closes | Saved playlist |

### Locator (In-Game Navigation)

`Locator.cs` simulates keyboard input via `user32.dll SendInput`:

1. Resets the music cursor (Shift key chord).
2. Types the initial letter of the song title to jump to that group.
3. Navigates up/down to the exact track.
4. Presses Ctrl+number to select button mode (4B/5B/6B/8B).
5. Navigates right to the correct difficulty order.
6. Optionally presses F5 to auto-start.

Requires DJMAX RESPECT V to be in **Freestyle mode**, **All Track** category, **Sort by Title A→Z**, **Korean** language.

## Build & Run

### Prerequisites

- .NET 6 SDK with Windows Desktop workload (`net6.0-windows`)
- Windows OS (required — uses WPF, Win32 P/Invoke)

### Build

```sh
dotnet build DjmaxRandomSelectorV.sln
```

Or specify configuration:

```sh
dotnet build DjmaxRandomSelectorV.sln -c Release
```

### Run

```sh
dotnet run --project DjmaxRandomSelectorV/DjmaxRandomSelectorV.csproj
```

The app prevents multiple instances using a named `Mutex` ("DjmaxRandomSelectorV").

On first launch it downloads `AllTrackList.json` and `appdata.json` from the internet. Ensure network access is available.

### Publish (single-file executable)

```sh
dotnet publish DjmaxRandomSelectorV/DjmaxRandomSelectorV.csproj -c Release -r win-x64
```

## Development Conventions

### Naming

- **ViewModels**: `<Name>ViewModel` in `DjmaxRandomSelectorV.ViewModels` namespace.
- **Views**: `<Name>View.xaml` in `DjmaxRandomSelectorV.Views` namespace — Caliburn.Micro auto-wires these.
- **Messages**: Thin `record` types for immutable events; use `class` only when mutation is needed (see `SettingMessage`).
- **Models** (library): `record` with `init`-only properties for immutability.
- **Configuration properties**: Group with comment headers (see `Dmrsv3Configuration.cs`).

### Serialization

- Use `System.Text.Json` with `JsonSerializerDefaults.Web` (camelCase, case-insensitive).
- Mark non-serialized properties with `[JsonIgnore]` (e.g., `BasicFilter.Favorite`, `BasicFilter.Blacklist`).
- Filter state JSON lives in `DMRSV3_Data/` and is accessed via `IFileManager`.

### Adding a New Filter Criterion

1. Add the property to `BasicFilter.cs` (in `Dmrsv.RandomSelector`).
2. Register `CollectionChanged` or a property setter to set `IsUpdated = true`.
3. Expose it in `BasicFilterViewModel.cs` with `NotifyOfPropertyChange()`.
4. Update `BasicFilterView.xaml` binding.

### Adding a New ViewModel/View

1. Create `ViewModels/<Name>ViewModel.cs` extending `Screen` or `Conductor<T>`.
2. Create `Views/<Name>View.xaml` + code-behind — Caliburn.Micro binds them by name convention.
3. Caliburn.Micro's `Bootstrapper.Configure()` registers all `*ViewModel` classes automatically.
4. Resolve with `IoC.Get<NameViewModel>()` or inject via constructor.

### DLC / Category System

- Categories have a `Type` field:
  - `0` — Regular DLC
  - `1`, `2` — Other paid content
  - `3` — PLI sub-category (internal, not shown directly in settings)
- `CategoryContainer.SetCategories()` merges main categories with PLI minors.
- `TrackDB.SetPlayable()` marks tracks as playable based on `OwnedDlcs` + `BasicCategories` (free tracks).

### Hotkey

- Default: VK `118` = F7.
- Registered as `RegisterHotKey` with `KeyModifiers.None` (select) and `KeyModifiers.Alt` (repeat last).
- Change via `SettingViewModel` → saved to `Config.json`.

## Testing

There are currently no automated test projects in the solution. Manual testing against the live DJMAX RESPECT V game is required for Locator behavior. Logic in `Dmrsv.RandomSelector` (filters, selector, history, picker) is unit-testable without a game instance.

## External Dependencies

| Dependency | Version | Purpose |
|---|---|---|
| Caliburn.Micro | 4.0.212 | MVVM framework, DI container, event aggregator |
| CsvHelper | 30.0.1 | CSV parsing (legacy `AllTrackList.csv`) |
| Microsoft.CSharp | 4.7.0 | Dynamic binding support |
| Microsoft.VisualBasic | 10.3.0 | Interaction helpers |
| Microsoft.DotNet.UpgradeAssistant.Extensions.Default.Analyzers | 0.4.355802 | Upgrade analyzer (dev-only) |

## Remote Data Sources

| URL | Purpose |
|---|---|
| `https://v-archive.net/db/songs.json` | Full track list / pattern data |
| `https://raw.githubusercontent.com/YoKi141/djmax-random-selector-v/main/Version3.txt` | Version check (app + appdata versions) |
| `https://raw.githubusercontent.com/YoKi141/djmax-random-selector-v/main/appdata.json` | Category / DLC / link-disc data |
| `https://github.com/wowvv0w/djmax-random-selector-v/releases` | Releases page (opened in browser) |
