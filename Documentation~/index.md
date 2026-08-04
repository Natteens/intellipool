# IntelliPool documentation

IntelliPool is a GameObject pooling layer built on `UnityEngine.Pool.ObjectPool<T>`. It supports a ScriptableObject database, prewarming, delayed release and a UI Toolkit editor window.

## Runtime database

Open `Tools > IntelliPool > Pool Manager` and create the runtime default database. The canonical path is:

```text
Assets/Resources/IntelliPool/PoolDatabase.asset
```

Add an entry for each prefab and configure its id, prewarm count, initial capacity and maximum retained size.

## Basic usage

```csharp
using IntelliPool;

GameObject bullet = Pool.Get("Bullet", muzzle.position, muzzle.rotation);
Pool.ReleaseDelayed(bullet, 3f);
```

Release manually when the object finishes its work:

```csharp
Pool.Release(bullet);
```

The static `Pool` facade creates or delegates to a `PoolService`. Projects that do not want a Resources-based bootstrap can initialize a service explicitly with a `PoolDatabase`.

## Pool entries

A `PoolEntry` defines:

- `id`: lookup key.
- `prefab`: object created by the pool.
- `prewarmCount`: inactive instances created during initialization.
- `defaultCapacity`: initial internal capacity.
- `maxSize`: maximum inactive instances retained.
- `collectionCheck`: development-time double-release validation.
- `clearOnSceneLoad`: whether active instances return when a scene changes.
- `containerName`: optional hierarchy container name.

## Pool callbacks

Implement `IPoolable` on prefab components that need reset hooks:

```csharp
using IntelliPool;
using UnityEngine;

public sealed class Enemy : MonoBehaviour, IPoolable
{
    public void OnSpawnedFromPool()
    {
        // Reset runtime state.
    }

    public void OnReturnedToPool()
    {
        // Stop effects and cancel timers.
    }
}
```

Callbacks are cached from the instance hierarchy when the pooled object is created.

## Explicit service setup

```csharp
Pool.Initialize(database);

PoolService service = gameObject.AddComponent<PoolService>();
service.Initialize(database);

GameObject enemy = service.Get("Enemy", position, rotation);
service.Release(enemy);
```

Use an explicit service when the project owns initialization order or does not use Resources.

## Management operations

`PoolService` also supports releasing every active instance in one pool, clearing a pool, clearing all pools and reading runtime statistics.

## Delayed release

Delayed release uses Unity `Awaitable`. A new spawn or state change should invalidate stale delayed-release work so a reused instance is not returned unexpectedly.

## Editor window

The Pool Manager provides:

- Database creation and selection.
- Runtime-default database validation.
- Entry search, duplication and removal.
- Per-entry validation.
- Runtime active and inactive counts in Play Mode.

## Performance notes

- Prewarming avoids gameplay-time instantiation for the expected load.
- `maxSize` limits retained inactive instances, not active instances.
- Return pooled objects through the pool instead of destroying them directly.
- Enable collection checks during development when double release is a concern.
- Use `Clear` only when pooled instances and retained memory should be discarded.