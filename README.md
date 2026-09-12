# Castle Miner Z — Third-Person Mode

A toggleable, collision-aware third-person camera and free-camera mod for **Castle Miner Z 1.9.9.8**.

> **Unofficial community project:** Third-Person Mode is independently created and published by AnlionGamer. It is not an official Castle Miner Z release and is not affiliated with, sponsored by, approved by, or endorsed by the game's developers or publisher.

**Publisher:** AnlionGamer  
**Current release:** v1.1.5  
**Mod ID:** `cmz.thirdpersonmode`

## Highlights in v1.1.5

- **Free Camera starts immediately** when the camera key is pressed.
- Quickly press and release the camera key to switch between **First Person** and **Third Person**.
- Hold the same key to orbit freely around the character without changing the player's gameplay facing or attack direction.
- Configurable **Camera Tap Time** defaults to **180 ms** and can be adjusted from **120–360 ms**.
- Camera toggling works while sprint/movement modifiers are held; extra modifiers no longer block a plain configured key.
- Third-person ranged reticles keep accurate target/parallax positioning while avoiding first-person recoil-camera kick.
- Third-person crosshair size now scales with actual camera pullback/FOV rather than target distance.
- Fixed direction-dependent third-person reticle flicker and crosshair morphing.
- Non-scoped third-person firing no longer visually shakes the third-person camera or reticle center.

## Camera Controls

Default camera key: **C**

- **Quick press + release:** switch First Person ↔ Third Person.
- **Hold:** use temporary Free Camera immediately; release to return to the perspective you started from.
- While Free Camera is active, the player's facing/aim direction remains independent of the orbit camera. Movement and attacks continue using the gameplay direction that existed when Free Camera began, subject to normal game-driven changes.
- The reticle is hidden while Free Camera is active.

The camera key is configurable from the mod's Settings page. Examples include `C`, `F5`, `Ctrl+C`, `Alt+C`, and `Shift+C`. Required modifiers remain required, but unrelated held modifiers do not invalidate the binding.

## Features

- Toggle between first-person and third-person during gameplay.
- Immediate hold-to-use Free Camera from either perspective.
- Right-shoulder, centered, or left-shoulder camera placement.
- Configurable camera distance and height.
- Camera collision pulls the view inward near terrain.
- Preserves Castle Miner Z's gameplay targeting rather than redirecting shots or interactions through the third-person camera.
- Third-person reticle centers project onto the player's real target path, preserving shoulder-camera parallax accuracy.
- Third-person reticle gap uses CMZ's live weapon spread/inaccuracy but scales visually for the actual third-person camera pullback and FOV.
- Target/object distance does not alter reticle size.
- Third-person reticle shape remains symmetrical and no longer flickers at fixed headings.
- Non-scoped third-person camera/reticle presentation ignores first-person recoil-camera motion while real weapon recoil, spread, projectile direction, and damage remain unchanged.
- Tools, blocks, melee items, grenades, and other non-gun interactions retain the appropriate native crosshair/interaction behavior.
- Non-scoped gun ADS uses the compact `+` reticle behavior.
- Scoped weapons temporarily use Castle Miner Z's genuine first-person scope and vanilla scoped recoil behavior.

## Requirements

- Castle Miner Z **1.9.9.8** (Steam)
- CMZ Mod Manager / Mod Framework **1.0.0 or newer**
- Windows / x86 game process

## Installation

1. Download `CMZ_Third_Person_Mode_v1.1.5.cmzmod` from the GitHub Releases page.
2. Install the `.cmzmod` through CMZ Mod Manager.
3. Enable **Third-Person Mode** in the active profile.
4. Launch Castle Miner Z through the Mod Manager.

## Settings

| Setting | Default | Range / Choices |
| --- | --- | --- |
| Toggle Camera Hotkey | `C` | Configurable key/chord |
| Camera Tap Time (ms) | 180 | 120–360 |
| Starting Perspective | First Person | First Person / Third Person |
| Camera Side | Right Shoulder | Right Shoulder / Centered / Left Shoulder |
| Camera Distance | 3.25 | 1.5–6.0 |
| Camera Height | 0.25 | -0.5–1.0 |

**Camera Tap Time** controls how quickly the camera key must be released for the press to count as a perspective switch. Holding longer keeps Free Camera active. Free Camera itself starts immediately on key-down, so raising this value does not delay Free Camera activation.

## Multiplayer

The v1.0.1 release received limited real multiplayer testing: a modded client joined a remote game, switched between first- and third-person, and remained connected for an extended session without a Third-Person Mode/runtime failure being recorded.

v1.1.5 remains client-side camera/HUD/input-view code and introduces no custom network messages or save/network format changes, but **v1.1.5 has not yet received separate hosted/joined multiplayer validation**.

Third-person and Free Camera visibility can provide additional around-obstacle awareness compared with strict first person, so the mod should not be described as competitively neutral.

## Save / Removal Safety

The mod does not intentionally change world data or save formats. Disable or remove it through CMZ Mod Manager. Existing saves are not expected to require conversion.

## Source

The repository contains the exact v1.1.5 gameplay source as a release-specific compressed/Base64 snapshot under `Source/`, with reconstruction instructions and release-manifest references for published versions. The release builder is intentionally not included because builders are development artifacts rather than public release content.

## License and Attribution

The current repository `main` branch and Third-Person Mode v1.1.5 are governed by the **AnlionGamer Community Distribution Terms v1.0**. See [`LICENSE`](LICENSE).

The terms allow normal use, source inspection, and private modification. Public redistribution of the original project, source, packaged mod, forks, or modified builds requires **prior permission from AnlionGamer** and must remain **non-commercial**. Sale and paid access are prohibited without separate permission.

Copies already distributed under earlier documented terms retain the permissions that accompanied those copies; changing the repository license does not revoke earlier grants.

Castle Miner Z and its original game material remain the property of their respective rights holders. See [`NOTICE.md`](NOTICE.md) for project attribution and the full affiliation notice.

The v1.1.5 `.cmzmod` carries `LICENSE.txt` and `NOTICE.md` inside the package so the applicable terms and project notice remain attached when the mod is shared separately from GitHub.
