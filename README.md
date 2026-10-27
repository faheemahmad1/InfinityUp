# POI System

Unity **2022.3.62f3**. The brief asks for 2022.3.62f2. That editor is not installed. Building on 62f3 was approved. This is the same 2022.3 LTS line with a newer patch.

The camera pitch is **90 degrees**, `Quaternion.Euler(90, 0, 0)`, looking straight down. Marker level of detail uses ground distance from the player on the XZ plane. Distance to the camera is not used, because a top-down camera puts every visible POI at nearly the same distance from the lens.

## What is in the scene

Open `Assets/Scenes/PoiDemo.unity` after Unity imports the project. The first editor load runs `POI/Setup Project`, which creates the config assets, placeholder sprites, marker prefab, Addressables entries, and the demo scene if they are missing.

WASD moves the player. The left panel changes POI count, type filters, minimum priority, camera height (50 m to 400 m), and the simulator. Add one, Move one, and Remove one call `PoiService`. The same config asset can be edited in the Inspector during Play. World size and cell size are locked while playing. Play Mode edits to the ScriptableObject are saved with the asset.

## Architecture

POIs are slots in `PoiStore`. There is no GameObject per POI. `UniformGridIndex` is a 400 m grid over the 16 km world. Each cell has a doubly linked list per `PoiPriority`. Add, remove, and move relink one slot. The simulator keeps an explicit mover list and integrates only that list.

`PoiSelector` is priority first, then nearest inside that priority:

1. Read only cells that overlap the camera footprint.
2. For each priority from Critical downward, examine every POI of that priority in those cells that passes the filters, the footprint, and the display range.
3. If the band is larger than the remaining budget, keep the nearest remainder with a size-K heap and stop.
4. A closer lower priority never replaces a farther higher priority.

That scan is not capped at 250. If a priority band inside the view contains 2,000 POIs, all 2,000 are examined. POIs outside the footprint are not read. The worst case, one priority and the whole dataset inside the view, examines every POI in the footprint and can approach N.

Let `B_p` be the number of priority `p` POIs in the overlapped cells that pass the filters and the display range, and let `p*` be the lowest priority examined. POIs examined = the sum of `B_p` from Critical through `p*`. Nearest-K inside the filling band is `O(B_p* log K)`.

The ground footprint intersects the camera frustum with `y = 0`. An edit-mode test compares that rectangle with `Camera.ViewportPointToRay`. `ViewportToWorldPoint` is the wrong check here, because its distance is along camera forward and misses the ground at the corners.

Marker instances are pooled, at most the budget (default 250). The prefab and icons load through Addressables. `MarkerBinding` drops a late icon when the marker was released, rebound, or the generation changed. Placeholder sprites live on `PoiPresentationCatalog` and can be replaced by assigning new sprites to the same `IconKey` addresses.

`IPoiFeed` is a C# event surface (`Added`, `Moved`, `Changed`, `Removed`). ScriptableObjects hold content and tuning. They are not the per-frame bus.

## Replacing placeholder art

Sprites are in `Assets/Poi/Art/Placeholder/`. Assign a replacement on `PoiPresentationCatalog`, keep the address (`Poi/Icon/Landmark` and the other `IconKey` names), and run `POI/Setup Project` so Addressables picks up the new asset. The marker prefab is `Assets/Poi/Art/Marker.prefab`, address `Poi/Marker`.

## Tests

Edit-mode tests are in `Assets/Poi/Tests/Editor/PoiSystemTests.cs`. They cover the grid, priority-first top-K, equal-priority examined counts, camera height, player movement, the real camera footprint, filters, rapid add/move/remove, the budget under churn, and stale icon completion. The 250,000 POI test asserts that a small footprint does not walk the whole store. It does not assert that the examined count is near 250.

Unity **2022.3.62f3** is not installed on this machine right now. Unity Hub only has 6000.4.7f1. These tests were not executed, and no benchmark numbers were recorded. Do not treat this README as a passing test report.

Run them from the Unity Test Runner on 2022.3.62f3, or:

```
Unity.exe -batchmode -nographics -projectPath <this folder> -runTests -testPlatform editmode -testResults Docs/editmode-results.xml
```

## Measurements

`POI/Run Split Benchmark` writes `Docs/Measurements/editmode-split.txt`.

That file is an edit-mode microbenchmark of lookup, mover integration, and grid relinks. It is not a frame-time measurement and it does not include the HUD. The table below stays blank until a play-mode capture exists. A cell is filled only from a recorder. Nothing here is estimated.

| POIs | Movers per frame | Avg frame ms | 95th pct frame ms | Lookup ms | Simulation ms | Grid update ms | POI-system GC bytes | Total frame GC bytes | Active markers |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1,000 |  |  |  |  |  |  |  |  |  |
| 25,000 |  |  |  |  |  |  |  |  |  |
| 250,000 | 12,500 |  |  |  |  |  |  |  |  |

The 250,000 row is specified as 5 percent moving, which is 12,500 movers per frame. Frame time, the 95th percentile, and total frame GC were not measured in this implementation pass if the play-mode table is still empty. Do not read the edit-mode file as those columns.

A Profiler `.data` capture was not saved, because a play-mode session with the profiler attached was not run.

## Trade-offs and limitations

- Examination cost grows with density inside the camera footprint. It does not grow when POIs are added outside that footprint.
- Growing the band buffer, creating POI name strings, and the first Addressables load allocate. Steady-state frames may still allocate. The HUD rebuilds its text four times a second and that allocation belongs in the total-frame number.
- Bulk count changes are batched, a few thousand POIs per frame, so a jump to 250,000 does not reload the scene and does not finish in one frame.
- Rebuilding mover membership when the moving percent changes scans the alive set once. Per-frame movement does not.
- World size and cell size cannot change during Play without rebuilding the grid.
- Off-screen indicators, declutter, click details, a mock network feed, and Jobs/Burst are not implemented.

## Submission

The project is the `main` branch. Create the bundle with:

```
git bundle create poi-faheem.bundle main
git clone poi-faheem.bundle poi-check
```
