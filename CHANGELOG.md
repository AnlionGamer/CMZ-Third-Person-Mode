# Changelog

## v1.0.1

- Corrected third-person crosshair fidelity to reuse Castle Miner Z's actual `CrossHairTick` visuals.
- Hip-fire guns now mirror vanilla gun inaccuracy-based crosshair spread at the projected real-aim point.
- Grenades, tools, blocks, melee items, and other non-gun interactions use the vanilla default crosshair spread at the projected interaction point.
- Kept the compact `+` reticle exclusive to non-scoped gun ADS.
- Preserved native first-person scopes for scoped weapons.
- Preserved the established camera, collision, targeting, hotkey, and scope-state architecture.
- Release packaging identifies both author and publisher as **AnlionGamer**.
- Release binary is built without debug symbols or embedded local user-profile build paths.

## v1.0.0

- Initial Third-Person Mode implementation.
- Added toggleable first-/third-person camera.
- Added shoulder/center camera positioning, distance and height configuration, and camera collision.
- Preserved vanilla gameplay targeting rather than redirecting attacks or interactions through the third-person camera.
