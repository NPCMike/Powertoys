# Module Registry

This folder is the staging area for NPCMike's personal module registry work.

The first registry version is intentionally data-only. It mirrors the module interface DLL list that is currently hard-coded in `src/runner/main.cpp`, plus low-risk metadata such as display name, settings window name, category, and source.

## Current scope

- Keep the existing runner loading flow intact.
- Keep GPO, hotkey conflict handling, IPC, installer registration, and shell extensions explicitly registered in their current locations.
- Use `ModuleRegistry.json` as the single reviewable source for the future registry shape before wiring it into runtime code.
- Use `EnabledModules.personal.json` as the first personal profile for modules that should be loaded in this fork when they exist in the registry.

## Personal profile

`EnabledModules.personal.json` is intentionally allowed to mention planned modules before their implementations exist. The runner consumes the profile only as a whitelist over `ModuleRegistry.json`: matching registry entries are loaded, planned module ids are logged and ignored until they have registry entries, and invalid or missing profile data falls back to the full registry load list.

Current profile intent:

| Module | Status | Notes |
|--------|--------|-------|
| `Launcher` | Existing upstream module | Keep PowerToys Run first. |
| `PowerOCR` | Existing upstream module | Keep until `FastOCR` is ready. |
| `ColorPicker` | Existing upstream module | Keep until `FastColorPicker` is ready. |
| `PidShower` | Planned personal module | Build first to validate custom module plumbing. |
| `AltSnap` | Planned personal module | Wrap AltSnap-style behavior behind an isolated service. |
| `VoiceInput` | Planned personal module | Local Whisper-style speech-to-text. |
| `FastOCR` | Planned personal module | Future replacement for PowerOCR. |
| `FastColorPicker` | Planned personal module | Future replacement for Color Picker. |
| `SnippingStudio` | Planned personal module | Screenshot and annotation workflow. |

## Migration order

1. Record current module load metadata in `ModuleRegistry.json`.
2. Add an enabled-modules profile for the personal fork.
3. Teach the runner to read DLL load paths from the registry with a hard-coded fallback.
4. Filter runner module loading through `EnabledModules.personal.json` with registry fallback behavior.
5. Filter Settings UI navigation, dashboard, quick access, and search through the same personal profile.
6. Move settings page mapping fully into registry data after the runner and Settings UI paths are stable.
7. Leave GPO, installer, hotkey, and IPC migration for later explicit phases.

This keeps the first step low-risk while still giving future module add/remove work a concrete place to converge.
