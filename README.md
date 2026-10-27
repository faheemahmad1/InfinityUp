# POI System

Unity **2022.3.62f2**. `ProjectSettings/ProjectVersion.txt` matches that editor. The edit-mode tests and the play-mode capture below were run in that editor.

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

On 27 Oct 2026 the edit-mode suite was run in Unity 2022.3.62f2. Result: **16 passed, 0 failed, 0 skipped**. The summary is `Docs/editmode-summary.txt`.

Run them again from the Unity Test Runner, or:

```
Unity.exe -batchmode -nographics -projectPath <this folder> -runTests -testPlatform editmode -testResults Docs/editmode-results.xml
```

## Measurements

`POI/Run Split Benchmark` writes `Docs/Measurements/editmode-split.txt`.

That file is an edit-mode microbenchmark of lookup, mover integration, and grid relinks. It is not a frame-time measurement and it does not include the HUD. The table below is the editor play-mode capture in `Docs/Measurements/playmode.txt`.

The capture used the demo scene: player moving, feed adding and removing 20 POIs per second, camera height 400 m, pitch 90, moving percent 5. Each row is 120 frames after 90 warmup frames, once the live count had reached the target. This is the editor, not a standalone player. Machine: Intel Core i7-10750H, 16 GB RAM, NVIDIA GeForce RTX 3070 Laptop GPU, Unity 2022.3.62f2.

Frame time is `Time.unscaledDeltaTime`. Lookup, simulation, and grid times are the POI stopwatches. POI-system GC is `GC.GetAllocatedBytesForCurrentThread` around the POI work in `Update`. Total frame GC is `ProfilerRecorder` on `GC Allocated In Frame`. Active markers and examined counts are averages over the same 120 frames. Mover counts are the measured averages at 5 percent; random membership does not land on exactly 50, 1,250, and 12,500.

| POIs | Movers per frame | Avg frame ms | 95th pct frame ms | Lookup ms | Simulation ms | Grid update ms | POI-system GC bytes | Total frame GC bytes | Active markers | Examined |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1,000 | 40.2 | 7.902 | 12.472 | 0.004 | 0.006 | 0.009 | 0 | 14,435 | 1.0 | 4.0 |
| 25,000 | 1,223.0 | 8.332 | 11.285 | 0.022 | 0.104 | 0.226 | 0 | 15,232 | 36.9 | 120.8 |
| 250,000 | 12,457.0 | 13.394 | 18.191 | 0.140 | 0.911 | 2.573 | 0 | 18,720 | 250.0 | 816.9 |

At 250,000 POIs the selector examined about 817 slots in the footprint and kept 250 markers. Examination is not capped at the marker budget. The POI-system counter read 0 bytes during these samples. The frame still allocated; that cost is the total-frame column.

The 250,000 sample wrote `Docs/Profiles/poi-250k.raw` and `Docs/Profiles/poi-250k.data` (21,393,780 bytes). The `.data` file was produced by loading that raw capture and saving it from the editor profiler.

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
