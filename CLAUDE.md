# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is a Unity Editor package (`zbyhoo.prefablocker`) that provides collaborative file locking for `.prefab` and `.unity` assets. It communicates with a Flask backend server via REST API to coordinate locks between team members. Distributed as a Unity Package Manager (UPM) package requiring Unity 2022.3+.

## Code Style

- Namespace: `PrefabLocker.Editor`
- All code lives under `PrefabLocker/Editor/` (editor-only assembly)
- Classes: PascalCase, Methods: camelCase, private fields: `_camelCase`
- 4-space indentation
- Use `EditorCoroutineUtility.StartCoroutineOwnerless()` for async operations (dependency: `com.unity.editorcoroutines`)
- Uses `Newtonsoft.Json` for JSON deserialization (precompiled reference)
- Batch mode is detected and lock system is bypassed via `Application.isBatchMode`

## Architecture

**LockServiceClient** - HTTP client (abstract static class) that talks to the backend. Has both async (coroutine-based with `UnityWebRequest`) and sync (`WebClient`) variants of lock/unlock/status operations. Server URL comes from `PrefabLockerSettings` ScriptableObject.

**PrefabLockOverlay** - `[InitializeOnLoad]` static class that draws lock icons on prefabs/scenes in the Project window. Polls server every 10s. Maintains in-memory cache of all locked files (`Dictionary<string, LockEntry>`).

**PrefabSaveLockProcessor** - `AssetModificationProcessor` that intercepts saves. Auto-locks unlocked prefabs/scenes on save, blocks saves of assets locked by other users.

**AutoUnlockService** - `[InitializeOnLoad]` static class that periodically checks if the current user's locked assets have been committed and pushed (via git), and auto-unlocks them. Also runs on editor quit (sync path).

**GitProvider** - Executes git commands via `System.Diagnostics.Process` with 30s caching. Provides branch info, change detection, and push status. Working directory is `Application.dataPath`.

**PrefabLockWindow** - EditorWindow UI (`Tools/Prefab Locker/Manager`). Shows all locked files with sort, unlock buttons, and branch/user info.

**UserNameProvider** - Stores username in `EditorPrefs` under key `PrefabLockerUserName`.

**PrefabLockerSettings** - ScriptableObject at `Assets/PrefabLocker/Editor/PrefabLocker/PrefabLockerSettings.asset` storing server URL, port, and check interval.

## API Endpoints

The client calls these backend endpoints (all include `branch`, `origin`, `filePath`, `userName` params):
- `POST /lock` - lock an asset
- `POST /unlock` - unlock an asset
- `GET /status` - get lock status for a single asset
- `GET /lockedAssets` - get all locked assets

## Backend

The Flask backend lives in the parent repo (`prefab_locker/`). See parent CLAUDE.md for backend commands. Server runs on port 5005.
