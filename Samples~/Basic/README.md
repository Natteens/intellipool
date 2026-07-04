# Basic Sample

Cube spawner demonstrating the IntelliPool runtime API. This sample ships source only - no scene, prefab, or `PoolDatabase` asset is included, so you build the test setup once in your own project.

## Manual setup

1. Import this sample through the Package Manager (copies `Code/` into your project).
2. Create a cube-like prefab with a `Rigidbody` and add the `TestCube` component to it.
3. Open **Tools -> IntelliPool -> Pool Manager** and click **Create Runtime Default** in the Runtime Database card. This creates the `PoolDatabase` at `Assets/Resources/IntelliPool/PoolDatabase.asset` so it's picked up by the default auto-bootstrap.
4. Add an entry to the database with id `TestCube` pointing at your prefab.
5. Add the `PoolTestSpawner` component to any GameObject in a scene.

## How to use

Enter Play Mode and either enable `Auto Spawn On Start` on the spawner, or right-click the component and choose **Spawn Once** from the context menu. `SpawnBurst(count)` is also available for scripted bursts.

Cubes are taken with `Pool.Get("TestCube", ...)` and returned with `Pool.ReleaseDelayed`. `TestCube` implements `IPoolable` to reset physics on spawn. `PoolTestSpawner` never references a `PoolDatabase` - it just calls `Pool.Get`/`Pool.ReleaseDelayed` by id and relies on the default auto-bootstrap.
