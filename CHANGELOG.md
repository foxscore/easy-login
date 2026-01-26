# Changelog

## [1.0.6-beta.5] - 2026-01-22

### Added

- Verifying `zipSHA256` hash of update before installation
- Added Discord and GitHub links to the settings page

### CHANGED

- Allowing the user to hide a given MOTD permanently
- Storing MOTD cache in the project's `Temp` directory instead of `%localappdata%` 

## [1.0.6-beta.4] - 2026-01-21

### ADDED

- Storing logs on disk

### CHANGED

- Improved logging system
- Completed self update system

## [1.0.6-beta.3] - 2026-01-19

### ADDED

- Added automatic update detection
- Added additional MOTD filters
- Made settings read-only if it's a newer version than what we can handle

### CHANGED

- Loading and storing MOTD on disk

### REMOVED

- Removed the `motd.json` file from zip builds

## [1.0.6-beta.2] - 2026-01-19

### FIXED

- Fixed an issue where the config / credentials file would reset due to a race condition

## [1.0.6-beta.1] - 2025-12-29

### ADDED

- Added MOTDs that can be published via the main branch of this packages repository.

### FIXED

- Fixed "Add to VCC" button in `README.md`. It does no longer open the image in a new tab, instead of opening the intended link.
- Fixed an error where if the `Rounded Radius` for profile-pictures was set to a very low value, we would make the entire icon (partially) invisible.
- Fixed us generating the profile-picture masks every single frame instead of caching them, leading to a memory leak due to how the Unity Editor handles temporary Texture2D instances.

### CHANGED

- Updated to a newer version of the `foxscore/make-unitypackage` action. The old version was not working correctly.

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
