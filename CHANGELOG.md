# Changelog

## 1.0.6 - 2026-09-13

### Changed
- Added the standard AWL Gaming badge to the package icon for consistent storefront presentation.
- Bumped package and plugin version metadata to 1.0.6; gameplay behavior is unchanged from the validated 1.0.5 build.

## 1.0.5 - 2026-09-13

### Fixed
- Restored compatibility with the current Valheim 1.0 `Vagon`, `Character`, and `BaseAI` APIs.
- Restored cart attachment to eligible tamed animals using the current vanilla attachment path.
- Preserved player attachment fallback and correct cart `InUse` behavior.
- Added null-safe handling for stale or missing attachment joints and character network views.

### Build
- Replaced obsolete machine-specific project references with a portable .NET Framework 4.7.2 project driven by explicit BepInEx and Valheim reference paths.

## 1.0.4 - 2021-04-19

Upstream release. See `README_UPSTREAM.md` and the original repository for the historical changelog.
