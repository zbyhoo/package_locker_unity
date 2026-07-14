# Prefab Locker

A Unity Editor package for collaborative file locking of `.prefab` and `.unity` assets.
It talks to a shared Flask backend over REST so team members don't overwrite each other's
work. Requires Unity 2022.3+.

## Installation

Add the package via **Window → Package Manager → Add package from git URL**:

```
https://github.com/zbyhoo/package_locker_unity.git#1.6.2
```

Pin the tag (`#1.6.2`) so updates are deliberate. Dependency `com.unity.editorcoroutines`
is resolved automatically.

## Configuration

One shared backend serves every project; each project is isolated by a **slug** carried in
the URL. The backend runs on a fixed port **5055** (not configurable from the client).

Open **Tools → Prefab Locker → Settings** to edit the `PrefabLockerSettings` asset
(`Assets/PrefabLocker/Editor/PrefabLocker/PrefabLockerSettings.asset`):

| Field | Description |
| --- | --- |
| `Url` | Backend host only, without scheme or port (e.g. `locker.internal` or `192.168.1.10`). The client builds `http://{Url}:5055/p/{ProjectSlug}`. |
| `ProjectSlug` | **Required.** Unique identifier for this project. Must match `^[a-z0-9._-]{1,64}$`. Two projects with different slugs never share locks. |
| `CheckIntervalSeconds` | How often the Project window overlay refreshes lock icons (default 60). |

The settings asset lives in the game project's `Assets/`, so **commit it** — the whole team
then shares the same host and slug automatically.

> If `ProjectSlug` is empty or invalid, every lock/unlock/status call is blocked and the
> Editor logs an error (with a one-time dialog). Set the slug before using the tool.

Your **username** is stored per machine in `EditorPrefs` (`PrefabLockerUserName`) and is
prompted on first use — it is *not* part of the committed asset.

## Usage

- Locks are acquired/released automatically on save (see `PrefabSaveLockProcessor`); assets
  locked by others block saving.
- **Tools → Prefab Locker → Manager** lists all locked files with user/branch info and manual
  unlock.
- Lock icons are drawn on prefabs/scenes in the Project window.
- Your locks are auto-released once the assets are committed and pushed (`AutoUnlockService`).

## Backend

The Flask backend lives in a separate repo (`prefab_locker_backend`). Run a single instance;
projects are separated by slug (`data/<slug>.db` per project), served under
`/p/<slug>/{lock,unlock,status,lockedAssets}` on port 5055. `/health` is unprefixed.

## Migrating from the old per-project model

Earlier versions used a separate backend instance per project, each on its own port
configured in the client. From **1.6.0** there is one backend on port 5055 and a `ProjectSlug`
instead of a per-project port. After updating the package, set `Url` and `ProjectSlug` in the
settings asset; the removed `Port` field is ignored.
