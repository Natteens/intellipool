<div align="center">

# IntelliPool

**A small GameObject pooling package built on Unity's `ObjectPool<T>`.**

Configure prefab pools in one place, prewarm them when needed, then get and return objects through a
compact runtime API. IntelliPool adds editor tooling and visibility without replacing Unity's own
pool implementation.

[![Version](https://img.shields.io/github/v/tag/Natteens/intellipool?sort=semver&label=version&style=flat-square)](https://github.com/Natteens/intellipool/tags)
[![Unity](https://img.shields.io/badge/Unity-2023.1%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](./LICENSE.md)

[Overview](#why-intellipool) · [Installation](#installation) · [Quick Start](#quick-start) · [Documentation](#documentation)

</div>

---

## Why IntelliPool

Unity already provides a solid pool implementation. IntelliPool builds around it instead of adding
another pooling engine.

The package keeps pool configuration in a shared database and gives you one place to manage prefab
entries, prewarming and capacity. At runtime, the common path stays simple: get an object, use it,
then return it. Objects only need to implement `IPoolable` when they actually need spawn or release
callbacks.

<table>
<tr>
<td width="50%"><strong>Central pool database</strong><br><sub>Keep prefab, prewarm and capacity settings together in one editable asset.</sub></td>
<td width="50%"><strong>Unity-backed pools</strong><br><sub>Uses `UnityEngine.Pool.ObjectPool<T>` instead of maintaining a separate pooling engine.</sub></td>
</tr>
<tr>
<td width="50%"><strong>Simple lifecycle</strong><br><sub>Get, release and delayed release cover the normal path without requiring a pool component on every prefab.</sub></td>
<td width="50%"><strong>Optional callbacks and stats</strong><br><sub>Use `IPoolable` when an object needs lifecycle hooks, and inspect pool state from the editor manager.</sub></td>
</tr>
</table>

## Installation

IntelliPool requires Unity **2023.1** or newer because delayed release uses Unity's Awaitable API.

In Package Manager, choose **Add package from git URL** and paste:

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

Append a version tag to the Git URL when you want the project pinned to a specific release.

## Quick Start

Open **Tools > IntelliPool > Pool Manager**, create the runtime-default database and add a prefab
entry such as `Bullet`.

```csharp
using IntelliPool;
using UnityEngine;

GameObject bullet = Pool.Get("Bullet", muzzle.position, muzzle.rotation);
Pool.ReleaseDelayed(bullet, 3f);
```

Objects that implement `IPoolable` receive spawn and return callbacks. Other objects can use the
pool without implementing an extra interface.

A basic sample is available from Package Manager.

## Documentation

Database setup, initialization, callbacks, delayed release, capacity rules and performance notes are
covered in [Documentation](./Documentation~/index.md).

See the [changelog](./CHANGELOG.md) for release history and migration notes.

## License

MIT. See [LICENSE.md](./LICENSE.md).
