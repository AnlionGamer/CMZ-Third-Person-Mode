using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using CMZ.ModSDK;
using DNA.CastleMinerZ;
using DNA.CastleMinerZ.Inventory;
using DNA.CastleMinerZ.Terrain;
using DNA.CastleMinerZ.UI;
using DNA.CastleMinerZ.Utils.Trace;
using DNA.Drawing;
using DNA.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;

[assembly: AssemblyTitle("Castle Miner Z Third-Person Mode")]
[assembly: AssemblyDescription("Toggleable third-person camera for Castle Miner Z 1.9.9.8")]
[assembly: AssemblyCompany("AnlionGamer")]
[assembly: AssemblyProduct("Castle Miner Z Third-Person Mode")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: AssemblyInformationalVersion("1.0.1")]

namespace CMZ.ThirdPersonMode
{
    public sealed class ThirdPersonModeMod : ICMZMod
    {
        private const string ModVersion = "1.0.1";
        private const float ShoulderOffset = 0.45f;
        private const float CameraCollisionPadding = 0.18f;
        private const float RangedReticleDistance = 1000.0f;
        private const float VanillaInteractionReach = 5.0f;

        private static IModContext _context;
        private static Settings _settings;
        private static KeyBinding _toggleBinding;

        private static GameScreen _currentScreen;
        private static CameraView _mainView;
        private static CameraView _fpsView;
        private static PerspectiveCamera _thirdCamera;
        private static bool _thirdCameraAttached;
        private static TraceProbe _cameraProbe;
        private static TraceProbe _terrainAimProbe;
        private static ConstructionProbeClass _aimProbe;
        private static bool _thirdPerson;

        // Authoritative third-person ADS state. This deliberately does not trust
        // a shoulder animation left behind by a previously equipped scoped gun.
        private static InventoryItem _trackedActiveItem;
        private static GunInventoryItemClass _trackedAdsGun;
        private static float _thirdPersonAdsAmount;
        private static bool _scopeWasActive;

        private static FieldInfo _mainViewField;
        private static FieldInfo _fpsViewField;
        private static FieldInfo _crosshairTickField;

        public void OnLoad(IModContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            _context = context;
            _settings = Settings.Load(context);
            _toggleBinding = KeyBinding.Parse(_settings.ToggleKey, context);
            _mainViewField = typeof(GameScreen).GetField(
                "mainView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _fpsViewField = typeof(GameScreen).GetField(
                "_fpsView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _crosshairTickField = typeof(InGameHUD).GetField(
                "_crosshairTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_mainViewField == null)
                throw new InvalidOperationException("GameScreen.mainView was not found for Castle Miner Z 1.9.9.8.");
            if (_fpsViewField == null)
                throw new InvalidOperationException("GameScreen._fpsView was not found for Castle Miner Z 1.9.9.8.");

            MethodInfo screenUpdate = RequireMethod(typeof(GameScreen), "Update", 2);
            MethodInfo playerInput = RequireMethod(typeof(InGameHUD), "OnPlayerInput", 4);
            MethodInfo hudDraw = RequireMethod(typeof(InGameHUD), "OnDraw", 3);
            MethodInfo fpsUpdateRotation = RequireMethod(typeof(FPSRig), "UpdateRotation", 2);

            MethodInfo beforeScreenUpdate = PatchMethod("BeforeGameScreenUpdate");
            MethodInfo afterScreenUpdate = PatchMethod("AfterGameScreenUpdate");
            MethodInfo afterPlayerInput = PatchMethod("AfterPlayerInput");
            MethodInfo beforeHudDraw = PatchMethod("BeforeHudDraw");
            MethodInfo afterHudDraw = PatchMethod("AfterHudDraw");
            MethodInfo hudDrawFinalizer = PatchMethod("HudDrawFinalizer");
            MethodInfo beforeFpsUpdateRotation = PatchMethod("BeforeFpsUpdateRotation");

            context.Patches.Prefix(screenUpdate, beforeScreenUpdate);
            context.Patches.Postfix(screenUpdate, afterScreenUpdate);
            context.Patches.Postfix(playerInput, afterPlayerInput);
            context.Patches.Prefix(hudDraw, beforeHudDraw);
            context.Patches.Postfix(hudDraw, afterHudDraw);
            context.Patches.Finalizer(hudDraw, hudDrawFinalizer);
            context.Patches.Prefix(fpsUpdateRotation, beforeFpsUpdateRotation);

            context.Log.Info(
                "Third-Person Mode " + ModVersion +
                " loaded. Toggle=" + _toggleBinding.DisplayName +
                "; start=" + (_settings.StartThirdPerson ? "Third Person" : "First Person") +
                "; side=" + _settings.CameraSide +
                "; distance=" + _settings.CameraDistance.ToString("0.##", CultureInfo.InvariantCulture) +
                "; height=" + _settings.CameraHeight.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        }

        public void OnGameReady()
        {
            if (_context != null)
                _context.Log.Info("Third-Person Mode is ready. Press " + _toggleBinding.DisplayName + " in gameplay to toggle perspective.");
        }

        public void OnShutdown()
        {
            try
            {
                RestoreFirstPerson(_currentScreen);
            }
            catch
            {
            }

            DetachThirdCamera();

            _currentScreen = null;
            _mainView = null;
            _fpsView = null;
            _thirdCamera = null;
            _thirdCameraAttached = false;
            _cameraProbe = null;
            _terrainAimProbe = null;
            _aimProbe = null;
            ResetThirdPersonAimState();
            _context = null;
        }

        private static MethodInfo RequireMethod(Type type, string name, int parameterCount)
        {
            BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            MethodInfo found = null;
            MethodInfo[] methods = type.GetMethods(flags);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                if (!string.Equals(candidate.Name, name, StringComparison.Ordinal) ||
                    candidate.GetParameters().Length != parameterCount)
                    continue;

                if (found != null)
                    throw new InvalidOperationException(
                        "Ambiguous patch target " + type.FullName + "." + name +
                        " with " + parameterCount + " parameter(s).");

                found = candidate;
            }

            if (found == null)
                throw new InvalidOperationException(
                    "Required patch target " + type.FullName + "." + name +
                    " with " + parameterCount + " parameter(s) was not found.");

            return found;
        }

        private static MethodInfo PatchMethod(string name)
        {
            MethodInfo method = typeof(ThirdPersonModeMod).GetMethod(
                name,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (method == null)
                throw new InvalidOperationException("Internal patch method was not found: " + name);

            return method;
        }

        private static void BeforeGameScreenUpdate(GameScreen __instance)
        {
            BindScreen(__instance);
        }

        private static void AfterGameScreenUpdate(GameScreen __instance)
        {
            if (!object.ReferenceEquals(_currentScreen, __instance))
                BindScreen(__instance);

            UpdateThirdPersonAimState(GetLocalPlayer());
            ApplyPerspective();
        }

        private static void BindScreen(GameScreen screen)
        {
            if (screen == null || object.ReferenceEquals(_currentScreen, screen))
                return;

            RestoreFirstPerson(_currentScreen);
            DetachThirdCamera();

            _currentScreen = screen;
            _mainView = null;
            _fpsView = null;
            _thirdCamera = new PerspectiveCamera();
            _thirdCameraAttached = false;
            EnsureThirdCameraAttached(screen);
            _cameraProbe = new TraceProbe();
            _terrainAimProbe = new TraceProbe();
            _aimProbe = new ConstructionProbeClass();
            _thirdPerson = _settings != null && _settings.StartThirdPerson;
            ResetThirdPersonAimState();

            try
            {
                _mainView = _mainViewField.GetValue(screen) as CameraView;
                _fpsView = _fpsViewField.GetValue(screen) as CameraView;
            }
            catch
            {
                _mainView = null;
                _fpsView = null;
            }
        }

        private static Player GetLocalPlayer()
        {
            GameScreen screen = _currentScreen;
            if (screen == null)
                return null;

            try
            {
                InGameHUD hud = screen.HUD;
                return hud == null ? null : hud.LocalPlayer;
            }
            catch
            {
                return null;
            }
        }

        private static CameraView GetMainView()
        {
            if (_mainView != null)
                return _mainView;

            GameScreen screen = _currentScreen;
            if (screen == null || _mainViewField == null)
                return null;

            try
            {
                _mainView = _mainViewField.GetValue(screen) as CameraView;
                _fpsView = _fpsViewField.GetValue(screen) as CameraView;
            }
            catch
            {
                _mainView = null;
                _fpsView = null;
            }

            return _mainView;
        }

        private static CameraView GetFpsView()
        {
            if (_fpsView != null)
                return _fpsView;

            GameScreen screen = _currentScreen;
            if (screen == null || _fpsViewField == null)
                return null;

            try
            {
                _fpsView = _fpsViewField.GetValue(screen) as CameraView;
            }
            catch
            {
                _fpsView = null;
            }

            return _fpsView;
        }

        private static InventoryItem GetActiveInventoryItem()
        {
            GameScreen screen = _currentScreen;
            if (screen == null)
                return null;

            try
            {
                InGameHUD hud = screen.HUD;
                return hud == null ? null : hud.ActiveInventoryItem;
            }
            catch
            {
                return null;
            }
        }

        private static GunInventoryItemClass GetActiveGunClass()
        {
            GameScreen screen = _currentScreen;
            if (screen == null)
                return null;

            try
            {
                InGameHUD hud = screen.HUD;
                if (hud == null || hud.ActiveInventoryItem == null)
                    return null;

                return hud.ActiveInventoryItem.ItemClass as GunInventoryItemClass;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsScopedWeaponAiming(Player player)
        {
            if (player == null || !player.Shouldering)
                return false;

            GunInventoryItemClass gun = GetActiveGunClass();
            return gun != null && gun.Scoped;
        }

        private static bool IsThirdPersonWorldViewActive(Player player)
        {
            return _thirdPerson && !IsScopedWeaponAiming(player);
        }

        private static void ResetThirdPersonAimState()
        {
            _trackedActiveItem = null;
            _trackedAdsGun = null;
            _thirdPersonAdsAmount = 0.0f;
            _scopeWasActive = false;
        }

        private static void RestoreVanillaPublicDefaults(Player player)
        {
            if (player == null)
                return;

            try
            {
                // Do not mutate CMZ's private animation state. The temporary scope
                // belongs to vanilla first person, but once it has ended we can safely
                // restore the public camera/sensitivity values that define normal view.
                player.FPSCamera.FieldOfView = player.DefaultFOV;
                player.GunEyePointCamera.FieldOfView = player.DefaultAvatarFOV;
                if (player.Avatar != null && player.Avatar.EyePointCamera != null)
                    player.Avatar.EyePointCamera.FieldOfView = player.DefaultAvatarFOV;
                player.ControlSensitivity = 1.0f;
            }
            catch (Exception ex)
            {
                LogWarningOnce("vanillaReset", "Could not fully restore vanilla public camera state: " + ex.Message);
            }
        }

        private static float GetFrameSeconds()
        {
            try
            {
                CastleMinerZGame game = CastleMinerZGame.Instance;
                if (game != null && game.CurrentGameTime != null)
                {
                    double seconds = game.CurrentGameTime.ElapsedGameTime.TotalSeconds;
                    if (seconds > 0.0)
                        return (float)Math.Min(0.1, seconds);
                }
            }
            catch
            {
            }

            return 1.0f / 60.0f;
        }

        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (current < target)
                return Math.Min(target, current + maxDelta);
            if (current > target)
                return Math.Max(target, current - maxDelta);
            return target;
        }

        private static void UpdateThirdPersonAimState(Player player)
        {
            if (player == null || !_thirdPerson)
            {
                ResetThirdPersonAimState();
                return;
            }

            InventoryItem activeItem = GetActiveInventoryItem();
            GunInventoryItemClass activeGun = activeItem == null
                ? null
                : activeItem.ItemClass as GunInventoryItemClass;
            bool scopedAim = player.Shouldering && activeGun != null && activeGun.Scoped;
            bool itemChanged = !object.ReferenceEquals(activeItem, _trackedActiveItem);

            if (itemChanged)
            {
                // Item changes clear only mod-owned ADS state. Never force CMZ's
                // private shoulder animation state during a camera/item transition.
                _trackedActiveItem = activeItem;
                _trackedAdsGun = activeGun;
                _thirdPersonAdsAmount = 0.0f;
                _scopeWasActive = scopedAim;
                return;
            }

            if (scopedAim)
            {
                _scopeWasActive = true;
                _trackedAdsGun = activeGun;
                _thirdPersonAdsAmount = 0.0f;
                return;
            }

            if (_scopeWasActive)
            {
                // Scope action is complete. Reset only public vanilla view controls;
                // third-person FOV/reticle are already fully mod-owned and reset here.
                RestoreVanillaPublicDefaults(player);
                _scopeWasActive = false;
                _trackedAdsGun = activeGun;
                _thirdPersonAdsAmount = 0.0f;
                return;
            }

            _trackedAdsGun = activeGun;

            if (activeGun == null || activeGun.Scoped)
            {
                _thirdPersonAdsAmount = 0.0f;
                return;
            }

            // Third-person ADS owns its own smooth transition. It deliberately does
            // not read Avatar.Animations[2], because that slot can retain a previous
            // scoped weapon's shoulder clip after the camera has already returned.
            float target = player.Shouldering ? 1.0f : 0.0f;
            float speedPerSecond = 8.0f;
            _thirdPersonAdsAmount = MoveToward(
                _thirdPersonAdsAmount,
                target,
                speedPerSecond * GetFrameSeconds());
        }

        private static float GetThirdPersonAdsAmount(Player player, GunInventoryItemClass activeGun)
        {
            if (activeGun == null || activeGun.Scoped || !object.ReferenceEquals(activeGun, _trackedAdsGun))
                return 0.0f;
            return Math.Max(0.0f, Math.Min(1.0f, _thirdPersonAdsAmount));
        }

        private static void BeforeFpsUpdateRotation(FPSRig __instance)
        {
            Player player = __instance as Player;
            if (player == null || !IsThirdPersonWorldViewActive(player))
                return;

            GunInventoryItemClass activeGun = GetActiveGunClass();

            // Sensitivity follows the current input action only. It never follows an
            // old shoulder animation, so a completed scope cannot leave third person
            // slow.
            bool nonScopedAds =
                activeGun != null &&
                !activeGun.Scoped &&
                player.Shouldering;
            player.ControlSensitivity = nonScopedAds ? 0.25f : 1.0f;
        }

        private static void ApplyPerspective()
        {
            Player player = GetLocalPlayer();
            CameraView view = GetMainView();

            if (player == null || view == null)
                return;

            try
            {
                bool useThirdPersonWorldView = IsThirdPersonWorldViewActive(player);

                if (useThirdPersonWorldView)
                {
                    EnsureThirdCameraAttached(_currentScreen);
                    RefreshThirdCamera(player);

                    CameraView fpsView = GetFpsView();
                    if (fpsView != null)
                        fpsView.Enabled = false;

                    if (player.FPSMode)
                        player.FPSMode = false;

                    if (_thirdCamera != null && !object.ReferenceEquals(view.Camera, _thirdCamera))
                        view.Camera = _thirdCamera;
                }
                else
                {
                    if (!object.ReferenceEquals(view.Camera, player.FPSCamera))
                        view.Camera = player.FPSCamera;

                    if (!player.FPSMode)
                        player.FPSMode = true;

                    CameraView fpsView = GetFpsView();
                    if (fpsView != null)
                        fpsView.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWarningOnce("apply", "Third-person camera application failed: " + ex.Message);
            }
        }

        private static void EnsureThirdCameraAttached(GameScreen screen)
        {
            if (screen == null || _thirdCamera == null || _thirdCameraAttached)
                return;

            try
            {
                if (screen.mainScene == null)
                    return;

                screen.mainScene.Children.Add(_thirdCamera);
                _thirdCameraAttached = true;
            }
            catch (Exception ex)
            {
                LogWarningOnce("cameraScene", "Could not attach third-person camera to the gameplay scene: " + ex.Message);
            }
        }

        private static void DetachThirdCamera()
        {
            if (_thirdCamera == null)
            {
                _thirdCameraAttached = false;
                return;
            }

            try
            {
                MethodInfo remove = _thirdCamera.GetType().GetMethod(
                    "RemoveFromParent",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (remove != null)
                    remove.Invoke(_thirdCamera, null);
            }
            catch
            {
            }

            _thirdCameraAttached = false;
        }

        private static void RestoreFirstPerson(GameScreen screen)
        {
            if (screen == null)
                return;

            try
            {
                InGameHUD hud = screen.HUD;
                Player player = hud == null ? null : hud.LocalPlayer;
                CameraView view = _mainView;

                if (view == null && _mainViewField != null)
                    view = _mainViewField.GetValue(screen) as CameraView;

                if (player != null)
                {
                    RestoreVanillaPublicDefaults(player);
                    if (view != null)
                        view.Camera = player.FPSCamera;
                    player.FPSMode = true;

                    CameraView fpsView = _fpsView;
                    if (fpsView == null && _fpsViewField != null)
                        fpsView = _fpsViewField.GetValue(screen) as CameraView;
                    if (fpsView != null)
                        fpsView.Enabled = true;
                }
            }
            catch
            {
            }
        }

        private static void RefreshThirdCamera(Player player)
        {
            if (player == null || _thirdCamera == null)
                return;

            PerspectiveCamera source = player.FPSCamera;
            Matrix eye = source.LocalToWorld;
            Vector3 eyePosition = eye.Translation;

            float side = 0.0f;
            if (_settings != null)
            {
                if (string.Equals(_settings.CameraSide, "Right Shoulder", StringComparison.OrdinalIgnoreCase))
                    side = ShoulderOffset;
                else if (string.Equals(_settings.CameraSide, "Left Shoulder", StringComparison.OrdinalIgnoreCase))
                    side = -ShoulderOffset;
            }

            float distance = _settings == null ? 3.25f : _settings.CameraDistance;
            float height = _settings == null ? 0.25f : _settings.CameraHeight;

            Vector3 forward = eye.Forward;
            if (forward.LengthSquared() <= 0.000001f)
                forward = Vector3.Forward;
            else
                forward.Normalize();

            Vector3 stableUp = Vector3.Up;
            if (Math.Abs(Vector3.Dot(forward, stableUp)) > 0.995f)
                stableUp = eye.Up;

            // Build a fresh no-roll basis from the real aim direction.
            Matrix stableBasis = Matrix.CreateWorld(Vector3.Zero, forward, stableUp);
            Vector3 desired =
                eyePosition - (forward * distance) +
                (stableBasis.Right * side) +
                (stableBasis.Up * height);

            desired = ResolveCameraCollision(eyePosition, desired);

            Matrix cameraWorld = stableBasis;
            cameraWorld.Translation = desired;
            _thirdCamera.LocalToParent = cameraWorld;

            // Third-person FOV must be deterministic and must never inherit
            // FPSCamera.FieldOfView. The FPS camera can retain optical scope
            // magnification while CMZ finishes its shoulder-state transition, and
            // copying that value lets the last-used scope contaminate later tools,
            // blocks, melee items, or guns.
            //
            // In third person:
            //   * no gun / scoped gun -> normal DefaultFOV
            //   * non-scoped gun -> interpolate from DefaultFOV to that gun's own
            //     ShoulderMagnification using the mod-owned third-person ADS amount.
            GunInventoryItemClass activeGun = GetActiveGunClass();
            _thirdCamera.FieldOfView = GetThirdPersonFieldOfView(player, activeGun);

            _thirdCamera.NearPlane = source.NearPlane;
            _thirdCamera.FarPlane = source.FarPlane;
        }


        private static DNA.Angle GetThirdPersonFieldOfView(Player player, GunInventoryItemClass activeGun)
        {
            DNA.Angle normalFov = player.DefaultFOV;

            // Scoped weapons only magnify while the temporary vanilla first-person
            // scope is active. Their third-person representation is always normal FOV.
            if (activeGun == null || activeGun.Scoped)
                return normalFov;

            float magnification = activeGun.ShoulderMagnification;
            if (magnification <= 0.0001f)
                magnification = 1.0f;

            DNA.Angle aimedFov = normalFov / magnification;
            float shoulderAmount = GetThirdPersonAdsAmount(player, activeGun);
            return DNA.Angle.Lerp(normalFov, aimedFov, shoulderAmount);
        }

        private static Vector3 ResolveCameraCollision(Vector3 eyePosition, Vector3 desired)
        {
            if (_cameraProbe == null)
                return desired;

            try
            {
                BlockTerrain terrain = BlockTerrain.Instance;
                if (terrain == null)
                    return desired;

                _cameraProbe.Init(eyePosition, desired);
                _cameraProbe.SkipEmbedded = true;
                terrain.Trace(_cameraProbe);

                if (!_cameraProbe._collides)
                    return desired;

                Vector3 hit = _cameraProbe.GetIntersection();
                Vector3 ray = desired - eyePosition;
                float total = ray.Length();

                if (total <= 0.0001f)
                    return desired;

                float hitDistance = Vector3.Distance(eyePosition, hit);
                float safeDistance = Math.Max(0.08f, hitDistance - CameraCollisionPadding);
                safeDistance = Math.Min(total, safeDistance);
                ray.Normalize();
                return eyePosition + (ray * safeDistance);
            }
            catch
            {
                return desired;
            }
        }

        private static void AfterPlayerInput(InGameHUD __instance, KeyboardInput __2)
        {
            if (__instance == null || __2 == null || _toggleBinding == null)
                return;

            if (__instance.IsChatting)
                return;

            if (!_toggleBinding.WasPressed(__2))
                return;

            _thirdPerson = !_thirdPerson;
            ResetThirdPersonAimState();
            ApplyPerspective();

            if (_context != null)
                _context.Log.Info("Camera perspective changed to " + (_thirdPerson ? "Third Person" : "First Person") + ".");
        }

        private sealed class HudDrawState
        {
            public Player Player;
            public bool OriginalShouldering;
            public GunInventoryItemClass GunClass;
            public bool OriginalScoped;
            public bool ChangedShouldering;
            public bool ChangedScoped;
            public bool DrawRangedReticle;
            public bool RangedAdsReticle;
            public float RangedReticleProgress;
            public Sprite RangedCrosshairTick;
            public float VanillaCrosshairSpread;
            public float VanillaCrosshairScale;
            public Color VanillaCrosshairColor;
            public bool DrawInteractionReticle;
            public InventoryItem InteractionItem;
            public Sprite InteractionCrosshairTick;
        }

        private static void BeforeHudDraw(InGameHUD __instance, out HudDrawState __state)
        {
            __state = null;

            if (__instance == null)
                return;

            Player player;
            try
            {
                player = __instance.LocalPlayer;
            }
            catch
            {
                return;
            }

            if (!IsThirdPersonWorldViewActive(player))
                return;

            HudDrawState state = new HudDrawState();
            state.Player = player;
            state.OriginalShouldering = player.Shouldering;
            state.GunClass = GetActiveGunClass();
            state.OriginalScoped = state.GunClass != null && state.GunClass.Scoped;

            InventoryItem activeItem = GetActiveInventoryItem();
            bool isRangedItem =
                state.GunClass != null ||
                activeItem is GrenadeItem;

            CaptureVanillaCrosshairVisualState(
                __instance,
                player,
                state.GunClass,
                out state.VanillaCrosshairSpread,
                out state.VanillaCrosshairScale,
                out state.VanillaCrosshairColor);

            // Ranged attacks keep CMZ's real player aim direction. The mod no longer
            // rewrites outgoing shot/projectile matrices toward the third-person camera.
            // Instead, move the HUD reticle to where the real FPS aim ray projects into
            // the third-person view. This keeps steep up/down aim and close-range parallax
            // honest: the reticle follows the shot rather than forcing the shot to center.
            if (!player.Dead && activeItem != null && isRangedItem)
            {
                state.DrawRangedReticle = true;

                if (_crosshairTickField != null)
                {
                    try
                    {
                        state.RangedCrosshairTick = _crosshairTickField.GetValue(__instance) as Sprite;
                    }
                    catch
                    {
                        state.RangedCrosshairTick = null;
                    }
                }

                if (state.GunClass != null && !state.GunClass.Scoped)
                {
                    float progress = GetThirdPersonAdsAmount(player, state.GunClass);
                    state.RangedReticleProgress = progress;
                    state.RangedAdsReticle = player.Shouldering || progress > 0.001f;
                }

                // Suppress CMZ's centered crosshair for this draw only. The projected
                // ranged reticle is redrawn after vanilla HUD rendering.
                if (!player.Shouldering)
                {
                    player.Shouldering = true;
                    state.ChangedShouldering = true;
                }
            }
            else if (!player.Dead && activeItem != null)
            {
                // World interactions stay 100% vanilla. Hide the centered HUD crosshair
                // and redraw CMZ's real CrossHairTick reticle where the FPSCamera construction/
                // melee ray projects into third person. This is HUD-only.
                state.DrawInteractionReticle = true;
                state.InteractionItem = activeItem;

                if (_crosshairTickField != null)
                {
                    try
                    {
                        state.InteractionCrosshairTick = _crosshairTickField.GetValue(__instance) as Sprite;
                    }
                    catch
                    {
                        state.InteractionCrosshairTick = null;
                    }
                }

                if (!player.Shouldering)
                {
                    player.Shouldering = true;
                    state.ChangedShouldering = true;
                }
            }

            // Prevent a one-frame stale scoped overlay when a scoped weapon is
            // leaving ADS and the animation state has not yet reached UnShouldering.
            if (state.GunClass != null && state.GunClass.Scoped)
            {
                state.GunClass.Scoped = false;
                state.ChangedScoped = true;
            }

            __state = state;
        }

        private static void CaptureVanillaCrosshairVisualState(
            InGameHUD hud,
            Player player,
            GunInventoryItemClass gunClass,
            out float spreadPixels,
            out float uiScale,
            out Color color)
        {
            spreadPixels = 0.0f;
            uiScale = 1.0f;
            color = Color.White;

            if (hud == null || player == null)
                return;

            try
            {
                // This mirrors InGameHUD.OnDraw in Castle Miner Z 1.9.9.8.
                // Vanilla uses CrossHairTick four times; the unused CrossHair sprite
                // is not the normal gameplay crosshair. Non-gun items use 0.5 degrees.
                DNA.Angle spreadAngle = DNA.Angle.FromDegrees(0.5f);
                if (gunClass != null)
                {
                    spreadAngle = gunClass.MinInnaccuracy +
                        (hud.InnaccuracyMultiplier *
                        (gunClass.MaxInnaccuracy - gunClass.MinInnaccuracy));
                }

                DNA.Angle fov = player.FPSCamera.FieldOfView;
                float fovRadians = fov.Radians;
                if (Math.Abs(fovRadians) > 0.000001f)
                {
                    spreadPixels =
                        (spreadAngle.Radians / fovRadians) *
                        DNA.Drawing.UI.Screen.Adjuster.ScreenRect.Width;
                }

                uiScale = Math.Max(1.0f, DNA.Drawing.UI.Screen.Adjuster.ScaleFactor.Y);

                float brightness =
                    ((1.0f - hud.InnaccuracyMultiplier) / 2.0f) + 0.5f;
                color = new Color(brightness, brightness, brightness, brightness);
            }
            catch (Exception ex)
            {
                LogWarningOnce(
                    "vanillaCrosshairState",
                    "Could not mirror vanilla crosshair state: " + ex.Message);

                uiScale = 1.0f;
                color = Color.White;
            }
        }

        private static void DrawVanillaCrosshairTicks(
            SpriteBatch spriteBatch,
            Sprite crosshairTick,
            Vector2 center,
            float spreadPixels,
            float uiScale,
            Color color)
        {
            if (spriteBatch == null || crosshairTick == null)
                return;

            // Exact CMZ 1.9.9.8 CrossHairTick placement constants from
            // InGameHUD.OnDraw, translated from screen-center to projected center.
            Vector2 right = new Vector2(
                center.X + spreadPixels,
                center.Y - (1.0f * uiScale));
            Vector2 left = new Vector2(
                center.X - (9.0f * uiScale) - spreadPixels,
                center.Y - (1.0f * uiScale));
            Vector2 top = new Vector2(
                center.X + (1.0f * uiScale),
                center.Y - (8.0f * uiScale) - spreadPixels);
            Vector2 bottom = new Vector2(
                center.X + (1.0f * uiScale),
                center.Y + spreadPixels + (1.0f * uiScale));

            spriteBatch.Begin();
            spriteBatch.Draw(
                crosshairTick.Texture,
                right,
                crosshairTick.SourceRectangle,
                color,
                0.0f,
                Vector2.Zero,
                uiScale,
                SpriteEffects.None,
                0.0f);
            spriteBatch.Draw(
                crosshairTick.Texture,
                left,
                crosshairTick.SourceRectangle,
                color,
                0.0f,
                Vector2.Zero,
                uiScale,
                SpriteEffects.None,
                0.0f);
            spriteBatch.Draw(
                crosshairTick.Texture,
                top,
                crosshairTick.SourceRectangle,
                color,
                MathHelper.PiOver2,
                Vector2.Zero,
                uiScale,
                SpriteEffects.None,
                0.0f);
            spriteBatch.Draw(
                crosshairTick.Texture,
                bottom,
                crosshairTick.SourceRectangle,
                color,
                MathHelper.PiOver2,
                Vector2.Zero,
                uiScale,
                SpriteEffects.None,
                0.0f);
            spriteBatch.End();
        }

        private static void DrawThirdPersonAimReticle(
            GraphicsDevice device,
            SpriteBatch spriteBatch,
            float progress,
            Vector2 reticleCenter)
        {
            if (device == null || spriteBatch == null)
                return;

            CastleMinerZGame game = CastleMinerZGame.Instance;
            if (game == null || game.DummyTexture == null)
                return;

            progress = Math.Max(0.0f, Math.Min(1.0f, progress));
            // SmoothStep keeps both ends of the contraction from snapping.
            progress = progress * progress * (3.0f - (2.0f * progress));

            int centerX = (int)Math.Round(reticleCenter.X);
            int centerY = (int)Math.Round(reticleCenter.Y);
            float uiScale = Math.Max(0.75f, (float)device.Viewport.Height / 720.0f);

            int thickness = Math.Max(1, (int)Math.Round(2.0f * uiScale));
            int armLength = Math.Max(5, (int)Math.Round(7.0f * uiScale));
            int hipGap = Math.Max(4, (int)Math.Round(8.0f * uiScale));
            int gap = (int)Math.Round((float)hipGap * (1.0f - progress));
            int halfThickness = thickness / 2;
            int outline = Math.Max(1, (int)Math.Round(uiScale));

            Rectangle left = new Rectangle(centerX - gap - armLength, centerY - halfThickness, armLength, thickness);
            Rectangle right = new Rectangle(centerX + gap, centerY - halfThickness, armLength, thickness);
            Rectangle top = new Rectangle(centerX - halfThickness, centerY - gap - armLength, thickness, armLength);
            Rectangle bottom = new Rectangle(centerX - halfThickness, centerY + gap, thickness, armLength);

            spriteBatch.Begin();
            DrawReticleBar(spriteBatch, game.DummyTexture, left, outline);
            DrawReticleBar(spriteBatch, game.DummyTexture, right, outline);
            DrawReticleBar(spriteBatch, game.DummyTexture, top, outline);
            DrawReticleBar(spriteBatch, game.DummyTexture, bottom, outline);

            // At full ADS ensure the four arms visually join into one exact '+'.
            if (gap == 0)
            {
                Rectangle center = new Rectangle(
                    centerX - halfThickness,
                    centerY - halfThickness,
                    thickness,
                    thickness);
                spriteBatch.Draw(game.DummyTexture, Expand(center, outline), Color.Black);
                spriteBatch.Draw(game.DummyTexture, center, Color.White);
            }
            spriteBatch.End();
        }

        private static void DrawReticleBar(
            SpriteBatch spriteBatch,
            Texture2D texture,
            Rectangle rect,
            int outline)
        {
            spriteBatch.Draw(texture, Expand(rect, outline), Color.Black);
            spriteBatch.Draw(texture, rect, Color.White);
        }

        private static Rectangle Expand(Rectangle rect, int amount)
        {
            return new Rectangle(
                rect.X - amount,
                rect.Y - amount,
                rect.Width + (amount * 2),
                rect.Height + (amount * 2));
        }

        private static bool TryGetRangedReticlePosition(
            GraphicsDevice device,
            out Vector2 screenPosition)
        {
            screenPosition = Vector2.Zero;

            Player player = GetLocalPlayer();
            if (device == null || player == null || _thirdCamera == null)
                return false;

            try
            {
                Matrix eye = player.FPSCamera.LocalToWorld;
                Vector3 start = eye.Translation;
                Vector3 forward = eye.Forward;
                if (forward.LengthSquared() <= 0.000001f)
                    return false;
                forward.Normalize();

                Vector3 end = start + (forward * RangedReticleDistance);
                Vector3 target = end;
                float bestDistanceSquared = RangedReticleDistance * RangedReticleDistance;

                // Enemy/player candidate along the real player aim ray.
                if (_aimProbe != null)
                {
                    try
                    {
                        _aimProbe.Init(start, end, true);
                        _aimProbe.SkipEmbedded = true;
                        _aimProbe.Trace();
                        if (_aimProbe._collides)
                        {
                            Vector3 hit = _aimProbe.GetIntersection();
                            float d2 = Vector3.DistanceSquared(start, hit);
                            if (d2 < bestDistanceSquared)
                            {
                                bestDistanceSquared = d2;
                                target = hit;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                // Terrain candidate along the same real player aim ray.
                if (_terrainAimProbe != null && BlockTerrain.Instance != null)
                {
                    try
                    {
                        _terrainAimProbe.Init(start, end);
                        _terrainAimProbe.SkipEmbedded = true;
                        BlockTerrain.Instance.Trace(_terrainAimProbe);
                        if (_terrainAimProbe._collides)
                        {
                            Vector3 hit = _terrainAimProbe.GetIntersection();
                            float d2 = Vector3.DistanceSquared(start, hit);
                            if (d2 < bestDistanceSquared)
                            {
                                bestDistanceSquared = d2;
                                target = hit;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                RefreshThirdCamera(player);
                Vector3 projected = device.Viewport.Project(
                    target,
                    _thirdCamera.GetProjection(device),
                    _thirdCamera.View,
                    Matrix.Identity);

                if (float.IsNaN(projected.X) || float.IsNaN(projected.Y) ||
                    float.IsInfinity(projected.X) || float.IsInfinity(projected.Y) ||
                    projected.Z < 0.0f || projected.Z > 1.0f)
                    return false;

                screenPosition = new Vector2(projected.X, projected.Y);
                return true;
            }
            catch (Exception ex)
            {
                LogWarningOnce("rangedReticle", "Third-person ranged reticle projection failed: " + ex.Message);
                return false;
            }
        }

        private static void DrawThirdPersonRangedReticle(
            GraphicsDevice device,
            SpriteBatch spriteBatch,
            Sprite crosshairTick,
            bool adsReticle,
            float adsProgress,
            float vanillaSpread,
            float vanillaScale,
            Color vanillaColor)
        {
            if (device == null || spriteBatch == null)
                return;

            Vector2 center;
            if (!TryGetRangedReticlePosition(device, out center))
                return;

            if (adsReticle)
            {
                // The compact '+' is intentionally exclusive to non-scoped gun ADS.
                DrawThirdPersonAimReticle(device, spriteBatch, adsProgress, center);
                return;
            }

            DrawVanillaCrosshairTicks(
                spriteBatch,
                crosshairTick,
                center,
                vanillaSpread,
                vanillaScale,
                vanillaColor);
        }

        private static bool TryGetVanillaInteractionReticlePosition(
            GraphicsDevice device,
            InventoryItem activeItem,
            out Vector2 screenPosition)
        {
            screenPosition = Vector2.Zero;

            Player player = GetLocalPlayer();
            if (device == null || activeItem == null || player == null || _thirdCamera == null || _aimProbe == null)
                return false;

            try
            {
                Matrix eye = player.FPSCamera.LocalToWorld;
                Vector3 start = eye.Translation;
                Vector3 forward = eye.Forward;
                if (forward.LengthSquared() <= 0.000001f)
                    return false;
                forward.Normalize();

                Vector3 end = start + (forward * VanillaInteractionReach);
                bool checkEnemies =
                    activeItem.ItemClass != null &&
                    activeItem.ItemClass.IsMeleeWeapon;

                // This exactly mirrors InGameHUD.DoConstructionModeUpdate's probe ray:
                // real FPS eye, five-unit reach, and melee enemy checks when applicable.
                _aimProbe.Init(start, end, checkEnemies);
                _aimProbe.SkipEmbedded = true;
                _aimProbe.Trace();

                Vector3 target = _aimProbe._collides
                    ? _aimProbe.GetIntersection()
                    : end;

                RefreshThirdCamera(player);
                Vector3 projected = device.Viewport.Project(
                    target,
                    _thirdCamera.GetProjection(device),
                    _thirdCamera.View,
                    Matrix.Identity);

                if (float.IsNaN(projected.X) || float.IsNaN(projected.Y) ||
                    float.IsInfinity(projected.X) || float.IsInfinity(projected.Y) ||
                    projected.Z < 0.0f || projected.Z > 1.0f)
                    return false;

                screenPosition = new Vector2(projected.X, projected.Y);
                return true;
            }
            catch (Exception ex)
            {
                LogWarningOnce("interactionReticle", "Third-person interaction reticle projection failed: " + ex.Message);
                return false;
            }
        }

        private static void DrawThirdPersonInteractionReticle(
            GraphicsDevice device,
            SpriteBatch spriteBatch,
            InventoryItem activeItem,
            Sprite crosshairTick,
            float vanillaSpread,
            float vanillaScale,
            Color vanillaColor)
        {
            if (device == null || spriteBatch == null || activeItem == null)
                return;

            Vector2 center;
            if (!TryGetVanillaInteractionReticlePosition(device, activeItem, out center))
                return;

            DrawVanillaCrosshairTicks(
                spriteBatch,
                crosshairTick,
                center,
                vanillaSpread,
                vanillaScale,
                vanillaColor);
        }

        private static void RestoreHudDrawState(HudDrawState state)
        {
            if (state == null)
                return;

            try
            {
                if (state.ChangedShouldering && state.Player != null)
                    state.Player.Shouldering = state.OriginalShouldering;

                if (state.ChangedScoped && state.GunClass != null)
                    state.GunClass.Scoped = state.OriginalScoped;
            }
            catch
            {
            }
        }

        private static void AfterHudDraw(GraphicsDevice __0, SpriteBatch __1, HudDrawState __state)
        {
            RestoreHudDrawState(__state);

            if (__state == null)
                return;

            if (__state.DrawRangedReticle)
            {
                try
                {
                    DrawThirdPersonRangedReticle(
                        __0,
                        __1,
                        __state.RangedCrosshairTick,
                        __state.RangedAdsReticle,
                        __state.RangedReticleProgress,
                        __state.VanillaCrosshairSpread,
                        __state.VanillaCrosshairScale,
                        __state.VanillaCrosshairColor);
                }
                catch (Exception ex)
                {
                    LogWarningOnce("rangedReticleDraw", "Third-person ranged reticle drawing failed: " + ex.Message);
                }
            }
            else if (__state.DrawInteractionReticle)
            {
                try
                {
                    DrawThirdPersonInteractionReticle(
                        __0,
                        __1,
                        __state.InteractionItem,
                        __state.InteractionCrosshairTick,
                        __state.VanillaCrosshairSpread,
                        __state.VanillaCrosshairScale,
                        __state.VanillaCrosshairColor);
                }
                catch (Exception ex)
                {
                    LogWarningOnce("interactionReticleDraw", "Third-person interaction reticle drawing failed: " + ex.Message);
                }
            }
        }

        private static Exception HudDrawFinalizer(Exception __exception, HudDrawState __state)
        {
            RestoreHudDrawState(__state);
            return __exception;
        }

        private static readonly HashSet<string> _warningKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static void LogWarningOnce(string key, string message)
        {
            if (_context == null)
                return;

            lock (_warningKeys)
            {
                if (!_warningKeys.Add(key))
                    return;
            }

            _context.Log.Warning(message);
        }

        private sealed class Settings
        {
            public string ToggleKey;
            public bool StartThirdPerson;
            public string CameraSide;
            public float CameraDistance;
            public float CameraHeight;

            public static Settings Load(IModContext context)
            {
                Settings result = new Settings();
                result.ToggleKey = "C";
                result.StartThirdPerson = false;
                result.CameraSide = "Right Shoulder";
                result.CameraDistance = 3.25f;
                result.CameraHeight = 0.25f;

                string path = Path.Combine(context.ConfigPath, "settings.json");
                if (!File.Exists(path))
                    return result;

                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    SettingsDocument document = serializer.Deserialize<SettingsDocument>(File.ReadAllText(path));
                    if (document == null || document.values == null)
                        return result;

                    object raw;
                    if (document.values.TryGetValue("toggleKey", out raw))
                        result.ToggleKey = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "C";

                    if (document.values.TryGetValue("startingPerspective", out raw))
                    {
                        string value = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "First Person";
                        result.StartThirdPerson = string.Equals(value, "Third Person", StringComparison.OrdinalIgnoreCase);
                    }

                    if (document.values.TryGetValue("cameraSide", out raw))
                    {
                        string value = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "Right Shoulder";
                        if (string.Equals(value, "Left Shoulder", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(value, "Centered", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(value, "Right Shoulder", StringComparison.OrdinalIgnoreCase))
                            result.CameraSide = value;
                    }

                    if (document.values.TryGetValue("cameraDistance", out raw))
                        result.CameraDistance = Clamp(ToSingle(raw, result.CameraDistance), 1.5f, 6.0f);

                    if (document.values.TryGetValue("cameraHeight", out raw))
                        result.CameraHeight = Clamp(ToSingle(raw, result.CameraHeight), -0.5f, 1.0f);
                }
                catch (Exception ex)
                {
                    context.Log.Warning("Could not read Third-Person Mode settings; package defaults will be used. " + ex.Message);
                }

                return result;
            }

            private static float ToSingle(object value, float fallback)
            {
                if (value == null)
                    return fallback;

                float parsed;
                if (float.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed))
                    return parsed;

                return fallback;
            }

            private static float Clamp(float value, float min, float max)
            {
                return Math.Max(min, Math.Min(max, value));
            }
        }

        private sealed class SettingsDocument
        {
            public Dictionary<string, object> values { get; set; }
        }

        private sealed class KeyBinding
        {
            public Keys Key;
            public bool Ctrl;
            public bool Alt;
            public bool Shift;
            public string DisplayName;

            public bool WasPressed(KeyboardInput keyboard)
            {
                if (!keyboard.WasKeyPressed(Key))
                    return false;

                KeyboardState state = keyboard.CurrentState;
                bool ctrlDown = state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl);
                bool altDown = state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
                bool shiftDown = state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift);

                return ctrlDown == Ctrl && altDown == Alt && shiftDown == Shift;
            }

            public static KeyBinding Parse(string text, IModContext context)
            {
                KeyBinding fallback = new KeyBinding {
                    Key = Keys.C,
                    Ctrl = false,
                    Alt = false,
                    Shift = false,
                    DisplayName = "C"
                };

                if (string.IsNullOrWhiteSpace(text))
                    return fallback;

                string[] parts = text.Split(new [] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                bool ctrl = false;
                bool alt = false;
                bool shift = false;
                Keys mainKey = Keys.None;

                for (int i = 0; i < parts.Length; i++)
                {
                    string token = parts[i].Trim();
                    if (token.Length == 0)
                        continue;

                    if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
                    {
                        ctrl = true;
                        continue;
                    }

                    if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
                    {
                        alt = true;
                        continue;
                    }

                    if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
                    {
                        shift = true;
                        continue;
                    }

                    if (mainKey != Keys.None)
                        return Invalid(text, fallback, context);

                    if (string.Equals(token, "Esc", StringComparison.OrdinalIgnoreCase))
                        token = "Escape";

                    Keys parsed;
                    if (!Enum.TryParse<Keys>(token, true, out parsed) || IsModifier(parsed))
                        return Invalid(text, fallback, context);

                    mainKey = parsed;
                }

                if (mainKey == Keys.None)
                    return Invalid(text, fallback, context);

                KeyBinding binding = new KeyBinding {
                    Key = mainKey,
                    Ctrl = ctrl,
                    Alt = alt,
                    Shift = shift
                };

                binding.DisplayName =
                    (ctrl ? "Ctrl+" : "") +
                    (alt ? "Alt+" : "") +
                    (shift ? "Shift+" : "") +
                    mainKey.ToString();

                return binding;
            }

            private static bool IsModifier(Keys key)
            {
                return key == Keys.LeftControl || key == Keys.RightControl ||
                    key == Keys.LeftAlt || key == Keys.RightAlt ||
                    key == Keys.LeftShift || key == Keys.RightShift;
            }

            private static KeyBinding Invalid(string text, KeyBinding fallback, IModContext context)
            {
                if (context != null)
                    context.Log.Warning("Invalid Toggle Camera Hotkey '" + text + "'. Falling back to C.");
                return fallback;
            }
        }
    }
}
