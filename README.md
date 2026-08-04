<div align="center">

# IntelliPool

**Configure prefab pools in one place, then get and return objects through a small runtime API.**

A lightweight GameObject pooling package built on Unity's `ObjectPool<T>`, with an Editor manager,
prewarming, delayed release and runtime visibility.

[![Release](https://img.shields.io/github/v/release/Natteens/intellipool?sort=semver&label=release&style=flat-square)](https://github.com/Natteens/intellipool/releases)
[![Unity](https://img.shields.io/badge/Unity-2023.1%2B-000000?style=flat-square&logo=unity)](https://unity.com)
[![License](https://img.shields.io/github/license/Natteens/intellipool?style=flat-square)](./LICENSE.md)

[Why IntelliPool?](#pooling-without-a-large-runtime) · [Installation](#installation) · [Quick Start](#quick-start) · [Documentation](#documentation)

</div>

---

## Pooling Without a Large Runtime

Object pooling is simple until every prefab invents its own cache, initialization rules and delayed
return logic. IntelliPool keeps those decisions in a shared database and delegates the actual pool
behavior to Unity's `ObjectPool<T>`.

The result is intentionally small: configure entries in the Pool Manager, request instances through
`Pool`, and opt into lifecycle callbacks only for objects that need them.

<table>
<tr>
<td width="50%"><strong>Central pool database</strong><br><sub>Prefab, prewarm and capacity settings remain visible and editable from one asset.</sub></td>
<td width="50%"><strong>Unity-backed pools</strong><br><sub>The runtime uses `UnityEngine.Pool.ObjectPool<T>` instead of maintaining a parallel pooling engine.</sub></td>
</tr>
<tr>
<td width="50%"><strong>Simple object lifecycle</strong><br><sub>Get, release and delayed release cover the common path without requiring a component-specific pool.</sub></td>
<td width="50%"><strong>Optional callbacks and stats</strong><br><sub>`IPoolable` and the Pool Manager expose extra lifecycle control only where it is useful.</sub></td>
</tr>
</table>

## Installation

Requires Unity **2023.1** or newer. This baseline is required by the Awaitable-based delayed release
path.

In the Package Manager, choose **Add package from git URL** and paste:

```text
https://github.com/Natteens/intellipool.git
```

Or declare it in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.natteens.intellipool": "https://github.com/Natteens/intellipool.git"
  }
}
```

Pin a release tag when the project needs reproducible pool behavior.

## Quick Start

Open **Tools > IntelliPool > Pool Manager**, create the runtime-default database and add a prefab
entry such as `Bullet`.

```csharp
using IntelliPool;
using UnityEngine;

GameObject bullet = Pool.Get("Bullet", muzzle.position, muzzle.rotation);
Pool.ReleaseDelayed(bullet, 3f);
```

Objects that implement `IPoolable` can receive spawn and return callbacks. Everything else can use
the pool without adopting an additional interface.

A basic sample is available from the Package Manager.

## Documentation

Database setup, initialization choices, callbacks, delayed release, capacity rules and performance
notes are documented in [Documentation](./Documentation~/index.md).

See the [changelog](./CHANGELOG.md) for release history and migration notes.

## License

MIT. See [LICENSE.md](./LICENSE.md).
