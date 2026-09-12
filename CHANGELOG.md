# Changelog

## v1.1.5

- Added immediate hold-to-use **Free Camera** on the existing camera key.
- Quick camera-key release switches First Person ↔ Third Person; holding keeps temporary Free Camera active until release.
- Added configurable **Camera Tap Time** with a **180 ms** default and **120–360 ms** range.
- Free Camera now begins on the initial key-down frame instead of waiting for a hold threshold.
- Mouse movement does not decide whether a camera-key press is a tap or hold; release timing alone determines the action.
- Free Camera works from either First Person or Third Person and returns to the perspective that was active before the hold.
- Free Camera locks player look input away from the orbit camera, keeping movement/attacks tied to gameplay facing instead of camera direction.
- Reticle is hidden while Free Camera is active.
- Fixed camera-hotkey handling so unrelated held modifiers such as sprint/Shift do not block a plain configured camera key.
- Fixed direction-dependent third-person ranged-reticle flicker caused by projected-depth boundary rejection.
- Removed unstable world-space four-point spread reprojection that caused the third-person crosshair to morph with camera direction.
- Preserved accurate third-person reticle target/parallax positioning from the player's real aim path.
- Separated reticle **position** from reticle **size**: target/object distance no longer changes crosshair gap.
- Added camera-pullback/FOV reticle-gap scaling using the actual view-axis camera pullback, with symmetrical horizontal/vertical spread.
- Camera collision naturally moves reticle size back toward first-person scale as the camera is forced closer.
- Removed first-person recoil-camera motion from non-scoped third-person camera and reticle-center presentation while preserving actual weapon recoil, spread, projectile direction, accuracy, and damage.
- Preserved native first-person scopes and vanilla scoped recoil behavior.
- Release package now carries `LICENSE.txt` and `NOTICE.md` and declares **AnlionGamer Community Distribution Terms v1.0** in the manifest.
- Release binary remains built without debug symbols or embedded local user-profile build paths.

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
