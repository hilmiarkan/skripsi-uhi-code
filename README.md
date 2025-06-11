# Malang Heat - Unity Setup Guide

## Struktur File Baru

Sistem telah dirombak total menjadi komponen yang lebih modular dan terorganisir:

### Core Components:
1. **DataManager.cs** - Mengelola paket data (raw + fuzzy + heatmap)
2. **OpenMeteoFetcher.cs** - Fetch data dari Open-Meteo API
3. **FuzzyCalculator.cs** - Perhitungan fuzzy logic
4. **UIManager.cs** - Manajemen semua UI elements
5. **AudioManager.cs** - Manajemen audio (BGM + footstep)
6. **GameController.cs** - Controller utama yang mengoordinasi semua komponen
7. **MarkerUpdater.cs** - Update teks pada marker
8. **HotspotManager.cs** - Mengelola hotspot (ring + marker)

## Setup GameObjects di Unity

### 1. GameController GameObject
Buat GameObject kosong bernama "GameController" dan attach script `GameController.cs`

**Setup Inspector:**
- Data Manager: Drag GameObject yang memiliki DataManager
- Open Meteo Fetcher: Drag GameObject yang memiliki OpenMeteoFetcher
- Fuzzy Calculator: Drag GameObject yang memiliki FuzzyCalculator
- Marker Updater: Drag GameObject yang memiliki MarkerUpdater
- Hotspot Manager: Drag GameObject yang memiliki HotspotManager
- UI Manager: Drag GameObject yang memiliki UIManager
- Auto Start Fetching: ✓ (checked)

### 2. DataManager GameObject
Buat GameObject kosong bernama "DataManager" dan attach script `DataManager.cs`

**Setup Inspector:**
- Raw Data Folder: "FetchedData"
- Fuzzy Data Folder: "FuzzyResults"
- Heatmap Folder: "HeatmapTextures"
- Prefetched Data Folder: "PrefetchedData"

### 3. OpenMeteoFetcher GameObject
Buat GameObject kosong bernama "OpenMeteoFetcher" dan attach script `OpenMeteoFetcher.cs`

**Setup Inspector:**
- Urban Csv: Drag file CSV urban coordinates
- Rural Csv: Drag file CSV rural coordinates

### 4. FuzzyCalculator GameObject
Buat GameObject kosong bernama "FuzzyCalculator" dan attach script `FuzzyCalculator.cs`
(Tidak perlu setup inspector, auto-initialized)

### 5. UIManager GameObject
Buat GameObject kosong bernama "UIManager" dan attach script `UIManager.cs`

**Setup Inspector:**
- Progress Text: Drag TextMeshProUGUI untuk progress
- Current Time Text: Drag TextMeshProUGUI untuk waktu Indonesia
- UHI Intensity Text: Drag TextMeshProUGUI untuk UHI intensity
- Hotspot Mitigation Text: Drag TextMeshProUGUI untuk hotspot mitigation
- Error Popup: Drag GameObject popup error
- Error Message Text: Drag TextMeshProUGUI dalam popup
- Try Again Button: Drag Button "Try Again"
- Use Prefetched Button: Drag Button "Use Prefetched Data"

### 6. AudioManager GameObject
Buat GameObject kosong bernama "AudioManager" dan attach script `AudioManager.cs`

**Setup Inspector:**
- Bg Audio Source: Drag AudioSource untuk BGM
- Main Menu Bg Clips: Array AudioClip untuk main menu music
- Game Bg Clips: Array AudioClip untuk in-game music
- Footstep Source: Drag AudioSource untuk footstep
- Footstep Clips: Array AudioClip untuk footstep sounds
- Player Rig: Drag XR Rig Transform
- Terrain: Drag Terrain GameObject
- Sfx Source: Drag AudioSource untuk SFX
- Click Sound: Drag AudioClip untuk click sound

### 7. MarkerUpdater GameObject
Buat GameObject kosong bernama "MarkerUpdater" dan attach script `MarkerUpdater.cs`

**Setup Inspector:**
- Raw Marker Parent: Drag Transform "Marker Real Data"
- Fuzzy Marker Parent: Drag Transform "Marker AT"

### 8. HotspotManager GameObject
Buat GameObject kosong bernama "HotspotManager" dan attach script `HotspotManager.cs`

**Setup Inspector:**
- Hotspot Ring Prefab: Drag prefab ring effect
- Hotspot Marker Prefab: Drag prefab rotating marker
- Action Mitigasi Prefab: Drag prefab ActionMitigasi
- Additional Prefab: Drag prefab tambahan (optional)
- Hotspot Parent: Drag Transform parent untuk semua hotspot

## UI Setup

### Progress Text (TextMeshProUGUI)
- Tampil di main menu dan saat proses berlangsung
- Format: "fetching.. 2m 13.4s" → "Fetching complete!" → "mengkalkulasi 32/1331 titik" → "Proses Selesai!"

### Error Popup (GameObject + Canvas)
Buat Canvas dengan:
- Background panel
- Error message TextMeshProUGUI
- "Try Again" Button
- "Use Prefetched Data" Button

### Game UI TextMeshProUGUI Elements:
1. **Current Time**: Format "HH:mm:ss WIB\ndd MMM yyyy"
2. **UHI Intensity**: Format "UHI Intensity: X.X°C"
3. **Hotspot Mitigation**: Format "X/5 Hotspot termitigasi"

## Folder Structure (Assets)

Buat folder-folder berikut di Assets:
```
Assets/
├── FetchedData/          (raw CSV data)
├── FuzzyResults/         (fuzzy calculation results)
├── HeatmapTextures/      (heatmap textures)
├── PrefetchedData/       (backup CSV files)
└── Scripts/              (semua script)
```

## Flow Sistem

1. **Game Start**: Otomatis mulai fetch data dari Open-Meteo
2. **Fetch Progress**: UI menampilkan "fetching.. Xm Xs"
3. **Fetch Complete**: UI menampilkan "Fetching complete!" selama 3 detik
4. **Fuzzy Start**: Otomatis mulai perhitungan fuzzy
5. **Fuzzy Progress**: UI menampilkan "mengkalkulasi X/total titik"
6. **Fuzzy Complete**: UI menampilkan "Proses Selesai!" selama 3 detik
7. **Hotspot Generation**: Hanya 5 hotspot fuzzy terpanas yang dibuat
8. **UHI Calculation**: Hitung dan tampilkan UHI intensity

## Error Handling

Jika fetch gagal:
- Popup error muncul dengan pesan error
- Button "Try Again" untuk retry
- Button "Use Prefetched Data" untuk gunakan data backup

## Audio Integration

- **Main Menu**: AudioManager.OnMainMenuEntered()
- **Game Mode**: AudioManager.OnGameModeEntered()
- **Prolog**: AudioManager.OnPrologStarted()

## Data Package System

Setiap sesi fetch menghasilkan paket data dengan timestamp:
- Raw CSV: `meteo_YYYYMMDD_HHMMSS.csv`
- Fuzzy CSV: `fuzzy_YYYYMMDD_HHMMSS.csv`
- Timestamp dapat ditampilkan: `DataManager.FormatTimestampForDisplay()`

## Debugging

Semua komponen memiliki debug log dengan prefix:
- `[GameController]`
- `[DataManager]`
- `[OpenMeteoFetcher]`
- `[FuzzyCalculator]`
- `[UIManager]`
- `[AudioManager]`
- `[MarkerUpdater]`
- `[HotspotManager]`

Gunakan Console window untuk monitoring.
