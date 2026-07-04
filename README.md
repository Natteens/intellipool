# IntelliPool

Lightweight GameObject pooling for Unity built on `UnityEngine.Pool.ObjectPool<T>`, with a ScriptableObject pool database, prewarming, delayed release and editor tooling.

## Installation

Package Manager -> **+** -> **Add package from git URL...**

```
https://github.com/Natteens/intellipool.git
```

Or in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.natteens.intellipool": "https://github.com/Natteens/intellipool.git"
  }
}
```

Requires Unity 2023.1+ (uses `Awaitable` for delayed release).

## Quick start

For zero-scene-setup usage, create the runtime default database:

1. Open **Tools -> IntelliPool -> Pool Manager**.
2. In the **Runtime Database** card, click **Create Runtime Default**. This creates a `PoolDatabase` at `Assets/Resources/IntelliPool/PoolDatabase.asset` (the folder is created automatically if needed).
3. Add an entry: assign a prefab (**Auto Id From Prefab** fills the id from the prefab name), set prewarm/capacity/max size.
4. Spawn and release from code - no scene setup required:

```csharp
using IntelliPool;

var bullet = Pool.Get("Bullet", muzzle.position, muzzle.rotation);
Pool.ReleaseDelayed(bullet, 3f);

// or release manually
Pool.Release(bullet);
```

On first use, `Pool` creates a hidden `DontDestroyOnLoad` `PoolService` and loads the default database from `Resources/IntelliPool/PoolDatabase` (falls back to any `PoolDatabase` in a `Resources` folder).

If you already have a `PoolDatabase` asset elsewhere, either move it into a `Resources` folder yourself, or select it in the Pool Manager and click **Use Selected As Runtime Default** to move it to the canonical runtime path automatically. The Runtime Database card always shows whether a runtime-loadable database currently exists.

## PoolDatabase

A `ScriptableObject` holding a list of `PoolEntry`:

| Field | Meaning |
|---|---|
| `id` | Lookup key. Auto-filled from prefab name if empty. |
| `prefab` | Prefab to instantiate. |
| `prewarmCount` | Instances created inactive at initialization. |
| `defaultCapacity` | Initial capacity of the internal pool. |
| `maxSize` | Max **inactive** instances retained. Extra released instances are destroyed (Unity `ObjectPool` semantics - it does not cap active count). |
| `collectionCheck` | Detects double release into the internal pool (editor/dev safety; disable for release builds). |
| `clearOnSceneLoad` | Releases all active instances of this pool when a scene loads. |
| `containerName` | Optional name for the pool's container object (default `Pool_<id>`). |

## Manual setup (no Resources)

If you don't want the `Resources` fallback, initialize explicitly:

```csharp
// from a database reference you own
Pool.Initialize(myDatabase);

// or place a PoolService component in a scene, assign its database
// in the Inspector - it registers itself with the Pool facade on Awake.

// or fully manual, no static facade at all
var service = someGameObject.AddComponent<PoolService>();
service.Initialize(myDatabase);
var enemy = service.Get("Enemy", position, rotation);
service.Release(enemy);
```

The static `Pool` facade is a thin delegate to a single `PoolService`; all state lives in the service.

## API overview

```csharp
// static facade
Pool.Get(string id, Vector3 pos = default, Quaternion rot = default, Transform parent = null);
Pool.Get<T>(string id, ...) where T : Component;   // typed get
Pool.Get(GameObject prefab, ...);                  // prefab-keyed pool, auto-created on demand
Pool.Release(GameObject instance);                 // returns false if not pooled / already released
Pool.Release(Component component);
Pool.ReleaseDelayed(GameObject instance, float delay);
Pool.IsReady; Pool.Current; Pool.Service;
Pool.Initialize(PoolDatabase db); Pool.Use(PoolService s);

// PoolService adds
service.ReleaseAll(string id);   // release all active instances of a pool
service.Clear(string id);        // release all + destroy pooled instances
service.ClearAll();
service.TryGetStats(string id, out PoolStats stats);

// on spawned instances (added automatically)
pooledObject.Release();
pooledObject.ReleaseDelayed(2f);
pooledObject.PoolId; pooledObject.IsSpawned;
```

## Spawn/despawn callbacks

Implement `IPoolable` on any component of the prefab (optional):

```csharp
public class Enemy : MonoBehaviour, IPoolable
{
    public void OnSpawnedFromPool() { /* reset state */ }
    public void OnReturnedToPool() { /* stop effects, cancel timers */ }
}
```

`IPoolable` components are cached once per instance at creation (including inactive children); components added at runtime are not picked up.

## Editor

**Tools -> IntelliPool -> Pool Manager** (UI Toolkit, UXML/USS):

- **Toolbar** - assign/create/find/save the database being edited, validate it.
- **Runtime Database card** - shows whether a database exists at the runtime default path (`Assets/Resources/IntelliPool/PoolDatabase.asset`), with buttons to create it, promote the currently selected database to that path, ping it, or re-validate it.
- **Validation card** - status for the database currently being edited (entry count, problems found).
- **Pool Entries panel** - searchable list of entries (id, prefab name, prewarm/max, invalid marker), with Add/Remove/Duplicate.
- **Entry Details panel** - selected entry grouped into Identity, Prewarm & Capacity, and Behavior fields, with Auto Id From Prefab, Ping Prefab, and Validate Entry.
- **Runtime Stats panel** - per-pool active/pooled/total/max counts, refreshed every 500ms in Play Mode.

## Performance notes

- Backend is `UnityEngine.Pool.ObjectPool<T>`; after prewarm there are no `Instantiate`/`Destroy` calls during gameplay (until `maxSize` overflow or `Clear`).
- No LINQ, reflection, or per-frame allocations in the runtime hot paths. `ReleaseDelayed` uses `Awaitable.WaitForSecondsAsync`, no coroutines.
- Release uses the `PooledObject` handle directly - no id or instance-id lookups.
- Double release is ignored with a warning in editor/development builds and returns `false`; `collectionCheck` adds a second guard inside the Unity pool.
- Don't `Destroy` pooled instances manually - use `Release`, `ReleaseAll` or `Clear`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

MIT - see [LICENSE.md](LICENSE.md).
