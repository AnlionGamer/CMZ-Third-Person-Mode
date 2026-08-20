# Castle Miner Z — Third-Person Mode

A toggleable, collision-aware third-person camera mod for **Castle Miner Z 1.9.9.8**.

**Publisher:** AnlionGamer  
**Current release:** v1.0.1  
**Mod ID:** `cmz.thirdpersonmode`

## Features

- Toggle between first-person and third-person during gameplay.
- Right-shoulder, centered, or left-shoulder camera placement.
- Configurable camera distance and height.
- Camera collision pulls the view inward near terrain.
- Preserves Castle Miner Z's real first-person gameplay aim for ranged attacks and world interactions.
- Third-person reticles project onto the real player aim path instead of redirecting shots toward the camera center.
- Hip-fire guns use CMZ's native `CrossHairTick` visuals and gun inaccuracy spread.
- Tools, blocks, melee items, grenades, and other non-gun interactions use CMZ's native crosshair behavior at the projected interaction point.
- The compact `+` reticle is reserved for non-scoped gun ADS.
- Scoped weapons temporarily use CMZ's genuine first-person scope.

## Requirements

- Castle Miner Z **1.9.9.8** (Steam)
- CMZ Mod Manager / Mod Framework **1.0.0 or newer**
- Windows / x86 game process

## Installation

1. Download `CMZ_Third_Person_Mode_v1.0.1.cmzmod` from the GitHub Releases page.
2. Install the `.cmzmod` through CMZ Mod Manager.
3. Enable **Third-Person Mode** in the active profile.
4. Launch Castle Miner Z through the Mod Manager.

## Controls

Default toggle: **C**

The hotkey is configurable from the mod's Settings page. Examples include `C`, `F5`, `Ctrl+C`, `Alt+C`, and `Shift+C`.

## Settings

| Setting | Default | Range / Choices |
| --- | --- | --- |
| Toggle Camera Hotkey | `C` | Configurable key/chord |
| Starting Perspective | First Person | First Person / Third Person |
| Camera Side | Right Shoulder | Right Shoulder / Centered / Left Shoulder |
| Camera Distance | 3.25 | 1.5–6.0 |
| Camera Height | 0.25 | -0.5–1.0 |

## Multiplayer

Limited real multiplayer testing has been successful: a modded client joined a remote game, switched between first- and third-person, and remained connected for an extended session without a Third-Person Mode/runtime failure being recorded.

The mod introduces no custom network messages and does not change save formats or network message formats. Broader host/client multiplayer validation is still ongoing.

Third-person visibility can provide additional around-obstacle awareness compared with first person, so the mod should not be described as competitively neutral.

## Save / Removal Safety

The mod does not intentionally change world data or save formats. Disable or remove it through CMZ Mod Manager. Existing saves are not expected to require conversion.

## Source

The repository contains the exact v1.0.1 gameplay source plus a reference copy of the v1.0.1 release manifest. The release builder is intentionally not included because it is not required to install, use, or review the mod.

## Release Integrity

Release downloads include `SHA256SUMS.txt` so the `.cmzmod` package can be verified after download.
