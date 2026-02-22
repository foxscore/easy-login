# Changelog

## [1.0.6] - 2026-02-22

### Added
- Added automatic update detection and a self-update option
- Added Discord and GitHub links to the settings page
- Added MOTDs
- Made settings read-only if it's a newer version than what we can handle

### Changed
- Slightly improved logging system, with logs now stored on disk
- Changed git-tag naming scheme *(from `X.Y.Z` to `vX.Y.Z`)*

### Fixed
- Updated to a newer version of the `foxscore/make-unitypackage` action. The old version was working correctly.
- Switched to `Harmony.AccessTools` for reflection in the `AccountWindowGUIHook` static-constructor, due to incompatibility issues with VRC-SDK `3.10.2-beta.1`
- Fixed an issue where the config / credentials file would reset due to a race condition
- Fixed "Add to VCC" button in `README.md`
- Fixed an error where if the `Rounded Radius` for profile-pictures was set to a very low value, we would make the entire icon (partially) invisible
- Fixed us generating the profile-picture masks every single frame instead of caching them, leading to a memory leak due to how the Unity Editor handles temporary Texture2D instances

### Removed
- Removed the `motd.json` file from zip builds

## [1.0.5] - 2025-09-11

### ADDED

- Optional Installation: Added automatic installation option for the `dev.pardeike.harmony` package if the ARM platform is detected and the current Harmony version is incompatible ([#11](https://github.com/foxscore/easy-login/issues/11))

### CHANGED

- Improved ARM detection to be universal across platforms

### FIXED

- Fixed missing imports in `PlatformUtils.cs` ([#10](https://github.com/foxscore/easy-login/issues/10))

## [1.0.4] - 2025-06-19

### ADDED

- Added a vertical scroll-view for when there are too many accounts ([#3](https://github.com/foxscore/easy-login/issues/3))

## [1.0.3] - 2025-02-10

### FIXED

- Fixed an issue where `BestHTTPSetup.Setup()` wasn't always called before using BestHTTP

### CHANGED

- Removed unused imports
