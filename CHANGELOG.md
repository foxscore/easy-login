# Changelog

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
