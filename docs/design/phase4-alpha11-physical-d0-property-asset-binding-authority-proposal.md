# Alpha 1.1 Physical D0 PropertyRight asset binding authority proposal

Status: **Approved / adopted into normative authority**

Tracking: #240
Approval record: #240 comment `5650161684`
Implementation: #265
Normative authority: `phase4-alpha11-physical-d0-property-asset-binding-authority.md`
Downstream approved authority: `phase4-alpha11-society-property-right-authority-proposal.md`

## Decision history

This file preserves the review history for PR #330. The project owner explicitly approved the full P1/P2/P3 package on 2026-09-13.

The adopted decision is now normative in `phase4-alpha11-physical-d0-property-asset-binding-authority.md`. Implementations must use that file rather than treating this proposal as an independent authority source.

The approved package is:

- P1: 4,096 benchmark TileFrame support records in `spatial.world_frame`, one per canonical TileScope, using the exact `perf.tile-frame` identity recipe and world-aligned zero-transform payload;
- P2: Physical ordinals `0..49,999` bind one-to-one to actual `Resident[0..49,999]`, use the descriptor-tile TileFrame, existing addressable-random XY, canonical terrain-height Z, identity orientation, zero motion, no containment, and `perf.free-moving`;
- P3: Terrain-SDF shapes bind to the actual canonical Terrain root and actual canonical D3-anchor brick world-coordinate AABB for the descriptor tile.

The approval remains benchmark-only and does not define Physical ordinals `50,000..499,999`, a universal world-frame hierarchy, general Physical subject taxonomy, general ownership semantics, or Infrastructure facility identity.

Accepted Society/Governance accounting does not change merely because this authority was approved. The 50,000 PropertyRight records count as accepted only after the production proof and full current-head CI required by the normative authority succeed.