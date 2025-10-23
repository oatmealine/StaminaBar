using System;
using System.Numerics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Utils;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Celeste.Mod.StaminaBar;

public class StaminaBarEntity : Entity {
    public const float MaxStamina = Player.ClimbMaxStamina;
    public const float DangerStamina = 20f; // hardcoded by Player.cs

    public const float FadeDurationBase = .5f;
    public const float FadeDurationVariance = .3f;
    public const float AlphaFadeDuration = .05f;
    public const float FlashDuration = .9f;

    public const float PieOuterRadius = 26f;
    public const float PieInnerRadius = 8f;
    public const int PieResolution = 30;
    public const float PieOutlineThickness = 3f;
    public const float OuterPieThickness = 7f;
    public const float OuterPieOutlineThickness = 2f;

    public Color BackColor;
    public Color OverlayColor;
    public Color StaminaColor;
    public Color StaminaFlashColor;
    public Color StaminaRefundColor;
    public Color StaminaLossColor;
    public Color StaminaDangerColor;
    public Color StaminaDangerRefundColor;
    public Color StaminaOverdraftColor;
    public Color StaminaDangerBarColor;
    public Color StaminaOverclockColor;
    
    public Player Player;
    private float stamina = MaxStamina;

    private float refillStamina = 0f;
    private float minStamina = MaxStamina;
    
    private float fadeTimer = FadeDurationBase;
    private float fadeDuration = FadeDurationBase;
    private float alpha = 0f;
    private float flash = 0f;
    private Vector2 squish = new(0f);

    private float lastStamina = MaxStamina;
    
    private ArcPolygon backPolygon = new(PieResolution);
    private ArcPolygon fillPolygon = new(PieResolution);
    private ArcPolygon refillPolygon = new(PieResolution);
    private ArcPolygon dangerBarPolygon = new(3);
    private ArcPolygon outerPolygon = new(PieResolution);
    private ArcPolygon outerBackPolygon = new(PieResolution);

    public const int BufferUpscaleFactor = 2;
    public const int BufferSize = (int)(PieOuterRadius + PieOutlineThickness * 2 + OuterPieOutlineThickness * 2 + OuterPieThickness) * 2 * BufferUpscaleFactor;
    private VirtualRenderTarget buffer;
    
    public StaminaBarEntity(Player player) {
        Player = player;

        Tag = TagsExt.SubHUD;
        Visible = false;

        backPolygon.OuterRadius = PieOuterRadius + PieOutlineThickness;
        backPolygon.InnerRadius = PieInnerRadius - PieOutlineThickness;

        fillPolygon.OuterRadius = PieOuterRadius;
        fillPolygon.InnerRadius = PieInnerRadius;
        
        refillPolygon.OuterRadius = PieOuterRadius;
        refillPolygon.InnerRadius = PieInnerRadius;

        dangerBarPolygon.OuterRadius = PieOuterRadius;
        dangerBarPolygon.InnerRadius = PieInnerRadius;

        outerPolygon.InnerRadius = PieOuterRadius + PieOutlineThickness * 2 + OuterPieOutlineThickness;
        outerPolygon.OuterRadius = outerPolygon.InnerRadius + OuterPieThickness;

        outerBackPolygon.InnerRadius = outerPolygon.InnerRadius - OuterPieOutlineThickness;
        outerBackPolygon.OuterRadius = outerPolygon.OuterRadius + OuterPieOutlineThickness;
        
        Add(new BeforeRenderHook(RenderBuffer));
    }

    // the intended purpose for this is to prevent the little "stutter" when transitioning between rooms with "always
    // on" enabled, but i couldn't figure out why this doesn't do the job
    /*
    public override void Awake(Scene scene) {
        base.Awake(scene);
        
        if (StaminaBar.Settings.AlwaysVisible && Player.StateMachine.State != Player.StIntroRespawn) {
            alpha = 1f;
            Visible = true;
        }
    }
    */

    private void UpdateConfig() {
        BackColor = Calc.HexToColor(StaminaBar.Settings.Color.Back);
        OverlayColor = Calc.HexToColor(StaminaBar.Settings.Color.Overlay);
        StaminaColor = Calc.HexToColor(StaminaBar.Settings.Color.Stamina);
        StaminaFlashColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaFlash);
        StaminaRefundColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaRefund);
        StaminaLossColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaLoss);
        StaminaDangerColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaDanger);
        StaminaDangerRefundColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaDangerRefund);
        StaminaOverdraftColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaOverdraft);
        StaminaDangerBarColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaDangerBar) * 0.2f;
        StaminaOverclockColor = Calc.HexToColor(StaminaBar.Settings.Color.StaminaOverclock);
    }

    private void UpdateAnimations() {
        var deadOrRespawning = Player.Dead || Player.StateMachine.State == Player.StIntroRespawn;
        
        // indicate BIG stamina losses/gains
        
        if (
            Math.Abs(stamina - lastStamina) > .03f &&
            stamina < MaxStamina &&
            (stamina - lastStamina > 0 ? StaminaBar.Settings.ShowRefunds : StaminaBar.Settings.ShowLosses) &&
            (stamina >= 0f || StaminaBar.Settings.ShowOverdraft) 
        ) {
            var addRefill = (stamina - lastStamina) / MaxStamina;
            if (Math.Sign(refillStamina) == Math.Sign(addRefill)) {
                refillStamina += addRefill;
            }
            else {
                refillStamina = addRefill;
            }
        }
        
        refillStamina *= float.Pow(32f, -Engine.DeltaTime);
        if (Math.Abs(refillStamina) < .01f) refillStamina = 0f;
        
        // flash the bar when stamina is regained
        
        var fullStamina = stamina == MaxStamina;
        var wasFullStamina = lastStamina == MaxStamina;
        if (fullStamina && !wasFullStamina) {
            flash = 1f;
            refillStamina = 0f;
            fadeDuration = Math.Max(fadeDuration, FadeDurationBase + FadeDurationVariance * (1f - minStamina / MaxStamina)); 
            minStamina = MaxStamina;
        }
        
        flash = Calc.Approach(flash, 0f, Engine.DeltaTime / FlashDuration);
        
        // squish the bar w/ the player's movements
        
        var targetSquish = Vector2.Lerp(Vector2.One, Player.Sprite.Scale.Abs(), (float)StaminaBar.Settings.Squish.SquishFactor / 10);
        if (Player.Ducking && StaminaBar.Settings.Squish.SquishOnCrouch) {
            targetSquish.X *= 1.1f;
            targetSquish.Y *= 0.8f;
        }
        if (deadOrRespawning) targetSquish = Vector2.One;
        squish = float.Pow(32f, -Engine.DeltaTime * 16f) * (squish - targetSquish) + targetSquish;
        
        // show/hide stamina bar
        
        if ((fullStamina && !StaminaBar.Settings.AlwaysVisible) || deadOrRespawning) {
            if (Player.Dead) fadeDuration = FadeDurationBase;
            fadeTimer += Engine.DeltaTime;
            if (fadeTimer >= (fadeDuration - .1f)) {
                alpha = Calc.Approach(alpha, 0f, Engine.DeltaTime / AlphaFadeDuration);
            }

            if (fadeTimer >= fadeDuration) {
                Visible = false;
                fadeDuration = FadeDurationBase;
            }
        } else {
            fadeTimer = 0f;
            Visible = true;
            alpha = Calc.Approach(alpha, 1f, Engine.DeltaTime / AlphaFadeDuration);
        }
    }

    public void UpdatePolygons() { 
        backPolygon.Color = BackColor;
        backPolygon.Rebuild();
        
        var baseColor = StaminaColor;
        if (stamina <= DangerStamina) baseColor = StaminaDangerColor;
        if (stamina <= 0f        ) baseColor = StaminaOverdraftColor;
        baseColor = Color.Lerp(baseColor, StaminaFlashColor, Ease.SineIn(flash));
        fillPolygon.Color = baseColor;
        fillPolygon.AngleEnd = Math.Clamp(stamina / MaxStamina, -1f, 1f);
        if (!StaminaBar.Settings.ShowOverdraft) fillPolygon.AngleEnd = Math.Max(fillPolygon.AngleEnd, 0f);
        if (refillStamina >= 0f) fillPolygon.AngleEnd -= refillStamina;
        fillPolygon.Rebuild();

        if (refillStamina >= 0f) {
            refillPolygon.Color = (stamina <= DangerStamina) ? StaminaDangerRefundColor : StaminaRefundColor;
        } else {
            refillPolygon.Color = StaminaLossColor;
        }

        refillPolygon.AngleStart = fillPolygon.AngleEnd;
        refillPolygon.AngleEnd = fillPolygon.AngleEnd + Math.Abs(refillStamina);  
        refillPolygon.Rebuild();

        dangerBarPolygon.Color = StaminaDangerBarColor * (1f - flash);
        dangerBarPolygon.AngleStart = 0f;
        dangerBarPolygon.AngleEnd   = DangerStamina / MaxStamina;
        dangerBarPolygon.Rebuild();

        outerPolygon.Color = Color.Lerp(StaminaOverclockColor, StaminaFlashColor, Ease.SineIn(flash));
        outerPolygon.AngleStart = 0f;
        outerPolygon.AngleEnd = Math.Max(0f, stamina / MaxStamina - 1f);
        outerPolygon.Rebuild();

        outerBackPolygon.Color = Color.Black;
        outerBackPolygon.AngleStart = -0.01f;
        outerBackPolygon.AngleEnd = outerPolygon.AngleEnd + 0.01f;
        outerBackPolygon.Rebuild();
    }

    public override void Update() {
        if (!StaminaBar.Settings.Enabled) return;
        
        base.Update();

        if (Player is null) {
            var player = Scene.Tracker.GetEntity<Player>();
            if (player is null) {
                RemoveSelf();
                return;                
            }
            Player = player;
        }

        if (!Player.Dead) stamina = Player.Stamina;
        minStamina = Math.Min(minStamina, stamina);
        
        UpdateAnimations();
        if (Visible && alpha > 0f) {
            UpdateConfig();
            UpdatePolygons();
        }
        
        lastStamina = stamina;
    }

    private static EverestModuleSettings settings;
    private static PropertyInfo modSettingProp;
    private static PropertyInfo cameraSettingProp;
    private static Func<Vector2> getCameraPosDelegate;
    private static bool getCameraPosFailed = false;

    // go my reflection soup
    private static Func<Vector2> GetGetCameraPos() {
        if (!Everest.Loader.TryGetDependency(StaminaBar.MotionSmoothingMod, out var module))
            return null; // not loaded
        
        var asm = module.GetType().Assembly;

        settings = module._Settings;
        modSettingProp = settings.GetType().GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
        var modSettingValue = modSettingProp?.GetValue(settings);
        if (modSettingValue is null) return null;
        cameraSettingProp = settings.GetType().GetProperty("UnlockCamera", BindingFlags.Public | BindingFlags.Instance);
        var cameraSettingValue = cameraSettingProp?.GetValue(settings);
        if (cameraSettingValue is null) return null;
        
        var unlockedCameraSmoother = asm.GetType("Celeste.Mod.MotionSmoothing.Smoothing.Targets.UnlockedCameraSmoother");
        var getCameraPositionMethod = unlockedCameraSmoother?.GetMethod("GetSmoothedCameraPosition", BindingFlags.Public | BindingFlags.Static);
        var getCameraPositionDelegate = getCameraPositionMethod?.CreateDelegate(typeof(Func<Vector2>));
        if (getCameraPositionDelegate is null)
            return null; // prolly changed the internals

        return getCameraPositionDelegate as Func<Vector2>;
    } 
    
    private static Vector2 GetCameraPos(Level level) {
        var camPos = level.Camera.Position.Floor();

        if (getCameraPosFailed) return camPos;
        if (getCameraPosDelegate is null) {
            getCameraPosDelegate = GetGetCameraPos();
            if (getCameraPosDelegate is null) {
                getCameraPosFailed = true;
                return camPos;
            } 
        }

        if (!((bool)modSettingProp.GetValue(settings)! && (bool)cameraSettingProp.GetValue(settings)!)) {
            return camPos; // disabled in settings
        }
        
        var res = getCameraPosDelegate.DynamicInvoke()!;
        return (Vector2)res;
    }

    private static Vector2 WorldToScreen(Vector2 worldPos, Level level) {
        var camPos = GetCameraPos(level);
        var screenPos = worldPos - camPos;
        if (SaveData.Instance != null && SaveData.Instance.Assists.MirrorMode)
            screenPos.X = 320f - screenPos.X;
        screenPos.X *= 6f;
        screenPos.Y *= 6f;

        return screenPos;
    }

    private void RenderBuffer() {
        if (buffer is null || buffer.IsDisposed)
            buffer = VirtualContent.CreateRenderTarget("staminabar-buffer", BufferSize, BufferSize);

        Engine.Graphics.GraphicsDevice.SetRenderTarget(buffer);
        Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

        var pos = new Vector2(BufferSize / 2);
        var scale = (float) BufferUpscaleFactor;
        backPolygon.Draw(pos, scale);
        refillPolygon.Draw(pos, scale);
        fillPolygon.Draw(pos, scale);
        if (stamina > DangerStamina && StaminaBar.Settings.ShowDanger) dangerBarPolygon.Draw(pos, scale);
        if (stamina > MaxStamina && StaminaBar.Settings.ShowOverclock) {
            outerBackPolygon.Draw(pos, scale);
            outerPolygon.Draw(pos, scale);
        }
    }

    private static Vector2 GetBarOffset() {
        return StaminaBar.Settings.Position.Alignment switch {
            StaminaBarSettings.BarPosition.TopRight    => new Vector2( 1f, -1f),
            StaminaBarSettings.BarPosition.BottomRight => new Vector2( 1f,  1f),
            StaminaBarSettings.BarPosition.TopLeft     => new Vector2(-1f, -1f),
            StaminaBarSettings.BarPosition.BottomLeft  => new Vector2(-1f,  1f),
            StaminaBarSettings.BarPosition.HUD          => new Vector2( 1f,  1f),
            _ => default
        };
    }
    
    public override void Render() {
        if (!StaminaBar.Settings.Enabled) return;
        if (alpha <= 0f) return;
        
        base.Render();
        
        // putting this here specifically so it updates when the game is pauased
        if (StaminaBar.Settings.Position.Alignment == StaminaBarSettings.BarPosition.HUD) {
            Tag = Tags.HUD;
        } else {
            Tag = TagsExt.SubHUD;
        }

        var level = SceneAs<Level>();
        var playerPos = WorldToScreen(Player.Position, level);
        var offset = GetBarOffset();
        
        var drawPos = Vector2.Zero;
        drawPos += new Vector2(46f);
        drawPos += new Vector2(PieOuterRadius / 2) * squish;
        drawPos *= offset;
        drawPos += new Vector2(0f, -50f);
        if (StaminaBar.Settings.Position.Alignment == StaminaBarSettings.BarPosition.HUD) {
            drawPos += new Vector2(12, 80);
        } else {
            drawPos += playerPos;   
        }
        drawPos += new Vector2(StaminaBar.Settings.Position.OffsetX, StaminaBar.Settings.Position.OffsetY);
        
        var size = Vector2.One;
        size *= (1f - (1f - Ease.SineOut(alpha)) * .5f);
        size *= squish;

        var color = Color.White * Ease.SineOut(alpha);
        var sprColor = OverlayColor * Ease.SineOut(alpha);  
        
        if (StaminaBar.Settings.Bunny)
            GFX.Gui["StaminaBar/bunnymode"].Draw(drawPos, new Vector2(128f), sprColor, size * PieOuterRadius/52f);
        if (StaminaBar.Settings.Kitty)
            GFX.Gui["StaminaBar/kittymode"].Draw(drawPos, new Vector2(128f), sprColor, size * PieOuterRadius/52f);
        if (StaminaBar.Settings.Fairy)
            GFX.Gui["StaminaBar/fairymode"].Draw(drawPos, new Vector2(128f), sprColor, size * PieOuterRadius/52f);
        if (StaminaBar.Settings.Moth)
            GFX.Gui["StaminaBar/mothmode"].Draw(drawPos, new Vector2(128f), sprColor, size * PieOuterRadius/52f);

        Draw.SpriteBatch.Draw(
            buffer, 
            drawPos, null,
            color,
            0f, 
            new Vector2(BufferSize/2), size / BufferUpscaleFactor,
            SpriteEffects.None, 0f
        );
    }
}