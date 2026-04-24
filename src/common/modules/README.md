# Module Registry

This folder is the staging area for NPCMike's personal module registry work.

The first registry version is intentionally data-only. It mirrors the module interface DLL list that is currently hard-coded in `src/runner/main.cpp`, plus low-risk metadata such as display name, settings window name, category, and source.

## Current scope

- Keep the existing runner loading flow intact.
- Keep GPO, hotkey conflict handling, IPC, installer registration, and shell extensions explicitly registered in their current locations.
- Use `ModuleRegistry.json` as the single reviewable source for the future registry shape before wiring it into runtime code.

## Migration order

1. Record current module load metadata in `ModuleRegistry.json`.
2. Add an enabled-modules profile for the personal fork.
3. Teach the runner to read DLL load paths from the registry with a hard-coded fallback.
4. Move settings page mapping after the runner path is stable.
5. Leave GPO, installer, hotkey, and IPC migration for later explicit phases.

This keeps the first step low-risk while still giving future module add/remove work a concrete place to converge.
