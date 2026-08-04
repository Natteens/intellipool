<div align="center">

# IntelliPool

Lightweight GameObject pooling built on Unity's `ObjectPool<T>`.

[![Release](https://img.shields.io/github/v/release/Natteens/intellipool?style=flat-square)](https://github.com/Natteens/intellipool/releases)
[![Unity](https://img.shields.io/badge/Unity-2023.1%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![License](https://img.shields.io/github/license/Natteens/intellipool?style=flat-square)](LICENSE.md)

</div>

IntelliPool provides a small runtime API, a ScriptableObject pool database and an editor window for configuring, validating and observing pools.

## Features

- Prefab pools backed by `UnityEngine.Pool.ObjectPool<T>`.
- Prewarming and delayed release.
- Optional spawn and return callbacks through `IPoolable`.
- Runtime statistics in the Pool Manager.

## Installation

Add the package through `Window > Package Manager > Add package from git URL`:

```text
https://github.com/Natteens/intellipool.git
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.natteens.intellipool": "https://github.com/Natteens/intellipool.git"
  }
}
```

## Quick start

1. Open `Tools > IntelliPool > Pool Manager`.
2. Create the runtime default database.
3. Add an entry and assign a prefab.
4. Spawn and return objects through `Pool`.

```csharp
using IntelliPool;

GameObject bullet = Pool.Get("Bullet", muzzle.position, muzzle.rotation);
Pool.ReleaseDelayed(bullet, 3f);
```

A basic sample is available from the Package Manager.

## Documentation

Database setup, callbacks, explicit initialization and performance notes are available in [Documentation](Documentation~/index.md).

## License

MIT. See [LICENSE.md](LICENSE.md).