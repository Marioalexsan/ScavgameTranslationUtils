# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Calendar Versioning](https://calver.org/).

## [unreleased]

## [2026.04.26]

### Added
- TextMeshPro tags used by Casualties: Unknown are now parsed and rendered in the text display
- The application can now load sprites from Casualties: Unknown and display them in the tool
  - Sprite keys, such as note sprites, will render their respective sprite
  - `<sprite index=N>` tags render their respective icon
- Periodic backups that trigger every 5 minutes while translating, saved under the `periodic_backups` folder
- An error message is displayed in the start window in case a translation or the game assets could not be loaded
- Toggle to hide the "rendered" text in the third column of the Translation tab and maximize the space for the actual
  raw text

### Changed
- Reduced the max number of regular backups (those triggered when clicking `Begin translating!`) from 5 to 3,
  to account for the fact that there are now up to 3 additional periodic backups
- Searching by key, original, or translation now accepts single characters (previously it would only search if there
  were 2 characters or more)
- The UI now uses Unifont throughout all UI elements
- Cleaned up the UI layout a bit
- The translation progress now shows progress for each category (main, buildings, etc.)
- "Next untranslated" now actually cycles between untranslated keys; the previous functionality of jumping to the first
  untranslated key has been moved to the new "First untranslated" button

### Fixed
- Loading translation files in encodings other than UTF-8 would fail
- Disabled ligature functionality for the time being (it would turn sequences like "->" into "→")

## [2026.04.21]

### Added
- More locales are available to pick for new translations
- Aliases for `zh` locales to align with translations in scavgame-locale:
  - `zh-Hans-CN` => `zh-CN`
  - `zh-Hans-SG` => `zh-SG`
  - `zh-Hant-MO` => `zh-MO`
  - `zh-Hant-HK` => `zh-HK`
  - `zh-Hant-TW` => `zh-TW`
- Option to sort keys in the "Translation page" in EN.json order instead of alphabetically
- Searching through translation data by key, original text, or current translated text
- Allow resizing the columns in the Translation tab

### Changed
- Slightly adjusted the condition for considering a key as translated
- Allow selecting text in the original English text block

### Fixed
- Adding text for `character` keys on fresh translations didn't work correctly

## [2026.04.18]

### Added
- Initial project release