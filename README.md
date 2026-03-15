# Gix – Augmented Reality Sky Observatory

> _"Connecter les humains à l'univers"_

Gix is a mobile AR application (iOS & Android) that superimposes stars, constellations, and live satellites onto the real sky through the device camera. Built with Unity (ARFoundation / ARKit / ARCore), it combines precise astronomical calculations with real-time satellite tracking.

---

## Repository Structure

```
Gix/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── AREngine.cs            – Main AR pipeline orchestrator
│   │   │   ├── CelestialCalculator.cs – Equatorial ↔ Horizontal coordinate transforms
│   │   │   ├── SGP4Calculator.cs      – Satellite propagator (SGP4 + J2 perturbations)
│   │   │   └── StarCatalog.cs         – Catalog manager with LOD partitioning
│   │   ├── Data/
│   │   │   └── StarData.cs            – StarData, SatelliteData, HorizontalCoordinates
│   │   ├── Culling/
│   │   │   ├── OctreeCuller.cs        – Octree for static star frustum culling
│   │   │   └── SpatialGrid.cs         – Uniform az/alt grid for satellite culling
│   │   ├── Rendering/
│   │   │   ├── StarRenderer.cs        – GPU-instanced star billboard renderer
│   │   │   ├── ConstellationRenderer.cs – LineRenderer constellation figures
│   │   │   ├── SatelliteRenderer.cs   – Per-satellite icons with Kalman-smoothed labels
│   │   │   └── LODManager.cs          – LOD budget and visibility decisions
│   │   ├── UI/
│   │   │   ├── AROverlay.cs           – AR HUD (toolbar, night mode, filters)
│   │   │   ├── InfoPanel.cs           – Contextual info panel (tap on object)
│   │   │   ├── SatelliteMap.cs        – 3D globe with satellite tracks
│   │   │   └── ISSViewController.cs   – "What does the ISS see?" feature
│   │   └── Utils/
│   │       ├── KalmanFilter.cs        – 1D/2D Kalman filter for label stabilisation
│   │       └── TLEParser.cs           – Two-Line Element set parser
│   ├── Shaders/
│   │   └── StarBillboard.shader       – GPU-instanced star billboard (additive blend)
│   └── Tests/
│       └── EditMode/
│           ├── CelestialCalculatorTests.cs
│           ├── TLEParserTests.cs
│           ├── KalmanFilterTests.cs
│           ├── SGP4CalculatorTests.cs
│           ├── SpatialGridTests.cs
│           └── StarCatalogTests.cs
├── Packages/
│   └── manifest.json                  – Unity package dependencies
└── ProjectSettings/
    └── ProjectSettings.asset
```

---

## AR Engine Pipeline

Each frame (target < 33 ms at 30 fps):

```
[Camera / ARFoundation tracking]
         │
         ▼
[Pre-computed star positions]   ←── Background worker thread (every 60 s)
         │
         ▼
[SGP4 satellite propagation]    ←── Main thread (real-time UTC timestamp)
         │
         ▼
[Frustum culling]
  • Stars:      OctreeCuller (static octree on celestial sphere)
  • Satellites: SpatialGrid  (az/alt grid, cleared each frame)
         │
         ▼
[LOD selection]
  mag < 3  → bright sprite + label   (LOD 0)
  mag 3–6  → simple point            (LOD 1)
  mag > 6  → not rendered in AR      (LOD 2, map mode only)
         │
         ▼
[Render]
  • StarRenderer        – single DrawMeshInstanced call per LOD level
  • ConstellationRenderer – LineRenderer per constellation
  • SatelliteRenderer   – billboarded icons, Kalman-filtered labels
```

---

## Key Technical Components

### CelestialCalculator
Converts equatorial (RA/Dec) to local horizontal (Az/Alt) coordinates using the GMST/LST formulation from *Meeus, Astronomical Algorithms (1998)*. Includes atmospheric refraction correction (Meeus p.106).

### SGP4Calculator
Simplified SGP4 propagator with:
- **LEO objects** (period < 225 min): J2 secular perturbations + first-order atmospheric drag (Bstar / mean-motion dot).
- **Deep-space objects**: pure Keplerian propagation.
- Outputs ECI position/velocity (km, km/s) and convenience geodetic/horizontal accessors.

### TLEParser
Full TLE checksum validation + parsing of all standard fields including BSTAR decimal-point encoding.

### KalmanFilter / KalmanFilter2D
Scalar and 2-D Kalman filters used to stabilise projected label positions and eliminate jitter at high frame rates.

### OctreeCuller / SpatialGrid
- **OctreeCuller**: builds once at startup; queries only the octree nodes whose AABB intersects the AR camera frustum.
- **SpatialGrid**: az/alt uniform grid cleared and re-populated every frame for fast satellite culling.

---

## LOD & Rendering Budget

| LOD | Magnitude | Rendering | Labels |
|-----|-----------|-----------|--------|
| 0   | < 3       | Detailed sprite | Named stars only |
| 1   | 3 – 6     | Point quad      | None |
| 2   | > 6       | Not rendered (AR) | Map mode |

Maximum instance budget: **5 000 objects/frame** (configurable in `LODManager`).

---

## Business Model

| Tier | Price | Catalogue | Satellites |
|------|-------|-----------|------------|
| Free | €0    | 10 000 stars | ISS + 10 popular |
| Gix+ | €4.99/mo · €39.99/yr | 120 000 stars + 8 000 deep-sky | 20 000+ with alerts |

One-time purchases: visual themes (€1.99), data packs (€2.99), Time Shift (€3.99).

---

## Product Roadmap

| Phase | Timeline | Milestones |
|-------|----------|------------|
| MVP   | 3 months | AR (1 000 stars, ISS + 10 sats), GPS localisation, push notifications |
| V1    | 6 months | 20 k stars, 2 000 deep-sky objects, real-time satellite tracking, partial offline |
| V2    | 9 months | "What does the ISS see?", community photo sharing, premium subscription |
| V3    | 12 months | Telescope integration, Time Shift, educational API, B2B partnerships |

---

## Running Tests

The core logic (no Unity Editor required) can be tested with the .NET SDK:

```bash
# From repo root – create a test project pointing at the source
cd Assets/Tests/EditMode
dotnet test
```

Tests cover:
- `CelestialCalculatorTests` – Julian Date, GMST, Az/Alt transforms, angular separation, refraction
- `TLEParserTests` – checksum validation, all TLE fields, epoch conversion
- `KalmanFilterTests` – convergence, smoothing, 2D independence
- `SGP4CalculatorTests` – ISS position/velocity/geodetic/horizontal
- `SpatialGridTests` – insert/query, wrap-around, large populations
- `StarCatalogTests` – LOD partitioning, HIP lookup, pre-computation

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| com.unity.xr.arfoundation | 5.1.0 | AR camera & tracking |
| com.unity.xr.arkit | 5.1.0 | iOS ARKit backend |
| com.unity.xr.arcore | 5.1.0 | Android ARCore backend |
| com.unity.burst | 1.8.12 | SIMD-optimised jobs |
| com.unity.collections | 2.2.1 | NativeArray for job system |
| com.unity.mathematics | 1.3.1 | SIMD-friendly maths |
| com.unity.textmeshpro | 3.0.6 | High-quality UI text |

---

## License

MIT © 2026 Si / Gix
