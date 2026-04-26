# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Calendar Versioning](https://calver.org/).

## [unreleased]

### Added
- TextMeshPro tags used by Casualties: Unknown are now parsed and rendered in the text display
- The application can now load sprites from Casualties: Unknown and display them in the tool
  - Sprite keys, such as note sprites, will render their respective sprite
  - `<sprite index=N>` tags render their respective icon

### Fixed
- Loading translation files in encodings other than UTF-8 would fail

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