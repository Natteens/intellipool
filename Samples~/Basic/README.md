# Basic Sample

Cube spawner demonstrating the IntelliPool runtime API.

## How to use

1. Import this sample through the Package Manager
2. Open `PooledCubeSpawner.unity`
3. Enter Play Mode and hold Space to spawn cubes

Cubes are taken with `Pool.Get("TestCube", ...)` and returned with `Pool.ReleaseDelayed`. `TestCube` implements `IPoolable` to reset physics on spawn. The spawner initializes the pool system from the sample `PoolDatabase` via `Pool.Initialize`.
