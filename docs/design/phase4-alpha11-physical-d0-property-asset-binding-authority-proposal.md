# Alpha 1.1 Physical D0 PropertyRight asset binding authority proposal

Status: **Review-only proposal / not authoritative**

Tracking: #240
Implementation target after approval: #265
Downstream approved authority: `phase4-alpha11-society-property-right-authority-proposal.md`

## Purpose

The approved `society.property_right` authority requires `asset_ref = PhysicalPresence[p]` for `p = 0..49,999`, and requires those refs to close to **actual records produced through the canonical Physical D0 production materialization path**.

The current `Qa04PhysicalD0MaterializerV1` intentionally does not invent its external genesis inputs. It requires:

- `Qa04PhysicalPresenceGenesisBindingV1` for every Physical ordinal; and
- `Qa04PhysicalTerrainRootBindingV1` for each referenced regional tile.

Repository audit confirms that the shape/occupancy authority and Terrain root authority exist, but no existing normative benchmark authority chooses the exact `subject_ref`, `frame_ref`, pose/motion genesis, `presence_mode`, or final Terrain SDF occupancy bounds needed to materialize the first 50,000 Presence records. The existing generic smoke binding uses fabricated test IDs and is not production authority.

This proposal fixes only the minimum support authority required for the already-approved PropertyRight 50,000-record proof. It does **not** define the remaining Physical ordinals `50,000..499,999`.

## Existing authority reused unchanged

The proposal consumes these existing authorities without redefining them:

- `Qa04ReferenceLoadV1` Physical descriptor identity, regional tile assignment, and `perf.reference.position.v1` addressable random position source;
- canonical Resident identity/materialization, including D0 Resident ordinals `0..99,999`;
- `Qa04SpatialTileScopeAuthorityV1` 4,096 actual `spatial.scope_registry` regional TileScope records;
- `Qa04TerrainRootMaterializerV1` actual 4,096 Terrain roots and 4,096 D3 anchors;
- `Qa04TerrainCanonicalContentSourceV1` tile width and deterministic terrain height;
- `Qa04PhysicalShapeMaterializerV1` exact Physical shape mix and shape payloads;
- `Qa04PhysicalD0MaterializerV1` actual `physical.presence` + occupancy + collision-shape production materialization;
- existing QA-04 Physical descriptor RecordIds and existing domain record schemas.

No existing test-only opaque IDs are promoted to authority.

## P1 — benchmark TileFrame support authority

Materialize exactly 4,096 support records in the existing `spatial.world_frame` partition, one for each canonical regional tile `t = 0..4,095`.

Identity:

```text
TileFrameId[t] = DerivedIdentity(
  world_id      = Qa04ReferenceLoadV1.WorldId,
  creation_step = 0,
  domain        = spatial,
  subject_id    = TileScopeId[t],
  creation_kind = perf.tile-frame,
  ordinal       = 0)
```

Envelope:

```text
record_id    = TileFrameId[t]
revision     = 1
created_step = 0
retired_step = NONE
detail_level = D2
lineage_ref  = NONE
```

Payload:

```text
frame_kind         = perf.world-aligned-tile-frame
parent_frame       = NONE
translation        = (0, 0, 0)
rotation           = (0, 0, 0, 1<<30)
valid_scope        = TileScope[t]
transform_revision = 1
```

Exact meaning:

- these records are benchmark-only world-coordinate-aligned charts whose validity is restricted to their actual regional TileScope;
- zero translation and identity rotation mean Presence positions below are already expressed in the benchmark world metric coordinate basis;
- `parent_frame=NONE` deliberately avoids inventing an unmaterialized world-root frame or world-scope geometry record;
- this support set does **not** claim to complete the general `spatial.world_frame` hierarchy or define future local-frame semantics.

Required closure:

- every `valid_scope` must close to the actual `Qa04SpatialTileScopeAuthorityV1` record for the same tile;
- all 4,096 TileFrame RecordIds must be distinct;
- `(valid_scope, frame_kind)` must be unique in this benchmark support set.

## P2 — first-50,000 Physical Presence genesis binding

For Physical ordinal `p = 0..49,999`:

```text
d = Qa04ReferenceLoadV1.Record(physical.d0-presence, p)
t = d.RegionalTileIndex
(u, v) = Qa04ReferenceLoadV1.PositionWithinTile(d.RecordId, step=0)

row = floor(t / 64)
col = t mod 64
W   = Qa04TerrainCanonicalContentSourceV1.TileWidthMm  // 512,000 mm

x_mm = col * W + floor(u * W)
y_mm = row * W + floor(v * W)
z_mm = Qa04TerrainCanonicalContentSourceV1.HeightMm(x_mm, y_mm)
```

The exact binding is:

```text
subject_ref                  = Resident[p]
frame_ref                    = TileFrame[t]
position                     = (x_mm, y_mm, z_mm)
orientation                  = (0, 0, 0, 1<<30)
linear_velocity              = (0, 0, 0)
angular_rate_urad_per_second = (0, 0, 0)
containment_ref              = NONE
presence_mode                = perf.free-moving
```

Exact meaning:

- the first 50,000 selected Physical D0 benchmark records use the first 50,000 actual D0 Residents as their identity-bearing subjects, one-to-one by ordinal;
- this is a benchmark fixture only and does not assert that all Physical Presence records are Residents;
- it does not define ordinals `50,000..499,999`;
- `perf.free-moving` is benchmark vocabulary only and does not replace the future general PresenceMode ontology;
- position uses the already-normative descriptor tile distribution and addressable random source; it does not use iteration order;
- `z_mm` is the existing deterministic terrain surface height at the generated XY point; no additional clearance, support, collision-resolution, or navigation claim is implied;
- identity orientation and zero genesis velocities are exact benchmark initialization values only;
- no containment identity is fabricated.

The existing `Qa04PhysicalShapeMaterializerV1` shape mix remains unchanged, including `terrain_sdf_ref` at ordinals where `p mod 100 = 99`. Shape type does not alter the `subject_ref` mapping in this benchmark subset.

## P3 — Terrain SDF root binding and occupancy bounds

For every tile `t`, use the actual canonical Terrain authority:

```text
terrain_root_ref = TerrainRoot[t]
```

The occupancy AABB supplied to `Qa04PhysicalD0MaterializerV1` is the exact world-coordinate AABB of the existing D3 anchor brick for tile `t`.

Let `Anchor[t]` be the canonical D3 anchor produced by `Qa04TerrainRootMaterializerV1`. From its authoritative brick origin and sample spacing:

```text
min_x = Anchor.origin.X * Anchor.sample_spacing_mm
min_y = Anchor.origin.Y * Anchor.sample_spacing_mm
min_z = Anchor.origin.Z * Anchor.sample_spacing_mm

width = TerrainBrickV1.CellsPerAxis * Anchor.sample_spacing_mm  // 512,000 mm

max_x = min_x + width
max_y = min_y + width
max_z = min_z + width
```

Then:

```text
occupancy_aabb_min = (min_x, min_y, min_z)
occupancy_aabb_max = (max_x, max_y, max_z)
```

The implementation must derive this from the actual canonical D3 anchor material, not re-create an unrelated synthetic box.

Exact meaning:

- the AABB is a finite broad-phase benchmark bound for the existing Terrain root's canonical D3 anchor;
- it does not claim that the whole Terrain root is semantically limited to one D3 brick;
- it does not change Terrain geometry, root identity, hot-brick material, or recovery semantics.

## Mandatory production proof after approval

Approval of P1/P2/P3 permits implementation but does not itself change accepted accounting. #265 must prove on one current head:

1. exactly 4,096 actual TileFrame support records through the standard `spatial.world_frame` payload validator;
2. exact TileFrame identity/envelope/payload and closure to all 4,096 actual TileScopes;
3. exactly 50,000 actual canonical Resident subjects `Resident[0..49,999]`;
4. exactly 50,000 Presence records produced by `Qa04PhysicalD0MaterializerV1` for Physical ordinals `0..49,999`;
5. exact QA-04 Physical descriptor RecordId preservation for all Presence records;
6. exact one-to-one `Presence[p].subject_ref = Resident[p]`;
7. exact descriptor tile -> TileFrame mapping and TileFrame scope closure;
8. exact deterministic XY generation and terrain-height Z generation defined above;
9. exact identity orientation, zero velocities, NONE containment, and `perf.free-moving`;
10. all actual shape/occupancy closure already required by `Qa04PhysicalD0MaterializerV1`;
11. every Terrain-SDF shape closes to the actual Terrain root for the descriptor tile and uses the actual D3-anchor AABB;
12. fail-closed negative proof for missing/wrong Resident, wrong subject partition/ordinal, missing/wrong TileFrame, frame/scope mismatch, position drift, orientation/motion drift, containment injection, presence-mode drift, missing/wrong Terrain root, Terrain AABB drift, duplicate Presence RecordId, and population drift;
13. the already-approved `society.property_right` 50,000-record production proof then consumes these actual Presence records and satisfies every gate in `phase4-alpha11-society-property-right-authority-proposal.md`;
14. generic Simulation Core smoke and full current-head CI are green.

## Accounting gate

This proposal and its support records do not add a new accepted Society/Governance count.

Until PropertyRight itself passes all mandatory production proof:

```text
Society/Governance accepted = 1,601,200 / 2,000,000
Society/Governance remaining =   398,800
Society remaining            =   139,800
Governance remaining         =   259,000
```

Only after the approved PropertyRight 50,000-record proof and full current-head CI succeed:

```text
Society/Governance accepted = 1,651,200 / 2,000,000
Society/Governance remaining =   348,800
Society remaining            =    89,800
Governance remaining         =   259,000
```

All other reference-world, Infrastructure, workload, Operation, and release flags remain unchanged.

## Explicit non-decisions

This proposal does not decide:

- Physical ordinals `50,000..499,999`;
- the full mix of Resident / vehicle / item / animal / equipment subjects for Physical D0;
- a universal world-root frame identity or complete frame hierarchy;
- general collision/support semantics at genesis;
- ownership of the underlying Resident subject from ownership of its Presence;
- Infrastructure facility identity;
- any remaining Society/Governance partition semantics.

Until P1/P2/P3 receive explicit project-owner approval, this file is review-only and must not be synchronized to `develop` or implemented in #265.