using System;
using System.Numerics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Utils;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace Celeste.Mod.StaminaBar;

public class StaminaBarEntity : Entity {
    public const float MaxStamina = Player.ClimbMaxStamina;
    public const float DangerStamina = 20f; // hardcoded by Player.cs

    public const float FadeDurationBase = .5f;
    public const float FadeDurationVariance = .3f;
    public const float AlphaFadeDuration = .05f;
    public const float FlashDuration = .9f;

    public float PieOuterRadius;
    public float PieInnerRadius;
    public int PieResolution = 30;
    public float PieOutlineThickness;
    public float OuterPieThickness;
    public float OuterPieOutlineThickness;

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
    private float currentMaxStamina = MaxStamina;

    private float refillStamina = 0f;
    private float minStamina = MaxStamina;
    
    private float fadeTimer = FadeDurationBase;
    private float fadeDuration = FadeDurationBase;
    private float alpha = 0f;
    private float flash = 0f;
    private Vector2 squish = new(0f);
    private Vector2 lagScale = new(0f);

    private float lastStamina = MaxStamina;
    
    private ArcPolygon backPolygon = new(30);
    private ArcPolygon fillPolygon = new(30);
    private ArcPolygon refillPolygon = new(30);
    private ArcPolygon dangerBarPolygon = new(3);
    private ArcPolygon outerPolygon = new(30);
    private ArcPolygon outerBackPolygon = new(30);

    public int BufferSize =>
        (int)(PieOuterRadius + PieOutlineThickness * 2 + OuterPieOutlineThickness * 2 + OuterPieThickness)
        * 2 * StaminaBar.Settings.UpscaleFactor;

    private VirtualRenderTarget buffer;
    
    public StaminaBarEntity(Player player) {
        Player = player;

        Tag = TagsExt.SubHUD;
        Visible = false;
        
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
        
        PieOuterRadius = StaminaBar.Settings.Size.PieOuterRadius;
        PieInnerRadius = StaminaBar.Settings.Size.PieInnerRadius;
        PieOutlineThickness = StaminaBar.Settings.Size.PieOutlineThickness;
        OuterPieThickness = StaminaBar.Settings.Size.OuterPieThickness;
        OuterPieOutlineThickness = StaminaBar.Settings.Size.OuterPieOutlineThickness;
    
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

        if (PieResolution != StaminaBar.Settings.Size.PieResolution) {
            PieResolution = StaminaBar.Settings.Size.PieResolution;
            
            backPolygon.SetResolution(PieResolution);
            fillPolygon.SetResolution(PieResolution);
            refillPolygon.SetResolution(PieResolution);
            outerPolygon.SetResolution(PieResolution);
            outerBackPolygon.SetResolution(PieResolution);
        }
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
        
        var fullStamina = stamina == currentMaxStamina;
        var wasFullStamina = lastStamina == currentMaxStamina;
        if (fullStamina && !wasFullStamina) {
            flash = 1f;
            refillStamina = 0f;
            fadeDuration = Math.Max(fadeDuration, FadeDurationBase + FadeDurationVariance * (1f - minStamina / MaxStamina)); 
            minStamina = currentMaxStamina;
        }
        
        flash = Calc.Approach(flash, 0f, Engine.DeltaTime / FlashDuration);
        
        // squish the bar w/ the player's movements
        
        var targetSquish = Vector2.One;
        if (Player.Ducking && StaminaBar.Settings.Squish.SquishOnCrouch) {
            targetSquish.X *= 1.1f;
            targetSquish.Y *= 0.75f;
        }
        var targetLagScale = Vector2.Lerp(Vector2.One, Player.Sprite.Scale.Abs(), (float)StaminaBar.Settings.Squish.SquishFactor / 10);
        if (deadOrRespawning) {
            targetSquish = Vector2.One;
            targetLagScale = Vector2.One;
        }
        squish = float.Pow(32f, -Engine.DeltaTime * 16f) * (squish - targetSquish) + targetSquish;
        lagScale = float.Pow(32f, -Engine.DeltaTime * 16f) * (lagScale - targetLagScale) + targetLagScale;
        
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
        currentMaxStamina = (int) (ExtendedVariantsImports.GetCurrentVariantValue?.Invoke("Stamina") ?? Player.ClimbMaxStamina);
        minStamina = Math.Min(minStamina, stamina);
        
        UpdateAnimations();
        if (Visible && alpha > 0f) {
            UpdatePolygons();
        }
        
        lastStamina = stamina;
    }

    private void RenderBuffer() {
        UpdateConfig();
        
        if (buffer != null && buffer.Width != BufferSize)
            buffer.Dispose();
        
        if (buffer is null || buffer.IsDisposed)
            buffer = VirtualContent.CreateRenderTarget("staminabar-buffer", BufferSize, BufferSize);

        Engine.Graphics.GraphicsDevice.SetRenderTarget(buffer);
        Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

        var pos = new Vector2(BufferSize / 2);
        var scale = (float) StaminaBar.Settings.UpscaleFactor;
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

    // not a big fan of XNA
    private static Matrix translate(float xy) => Matrix.CreateTranslation(xy, xy, 0f);
    private static Matrix translate(float x, float y) => Matrix.CreateTranslation(x, y, 0f);
    private static Matrix translate(Vector2 v) => Matrix.CreateTranslation(v.X, v.Y, 0f);
    private static Matrix scale(float xy) => Matrix.CreateScale(xy, xy, 1f);
    private static Matrix scale(float x, float y) => Matrix.CreateScale(x, y, 1f);
    private static Matrix scale(Vector2 v) => Matrix.CreateScale(v.X, v.Y, 1f);

    private static Vector3 getScale(Matrix m) {
        return new Vector3(
            (new Vector3(m.M11, m.M12, m.M13)).Length(),
            (new Vector3(m.M21, m.M22, m.M23)).Length(),
            (new Vector3(m.M31, m.M32, m.M33)).Length()
        );
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
        var playerPos = level.WorldToScreen(Player.Position);
        var offset = GetBarOffset();

        var m = Matrix.Identity;
        
        m *= translate(StaminaBar.Settings.Position.OffsetX, StaminaBar.Settings.Position.OffsetY);
        
        m *= scale(1f - (1f - Ease.SineOut(alpha)) * .5f);
        
        m *= scale(lagScale);
        
        m *= translate(PieOuterRadius / 2);
        m *= translate(46f);
        
        m *= translate(-PieOuterRadius * 2, -PieOuterRadius);
        m *= scale(squish);
        m *= translate(PieOuterRadius * 2, PieOuterRadius);
        
        if (StaminaBar.Settings.Position.Alignment == StaminaBarSettings.BarPosition.HUD) {
            m *= translate(12, 80);
        } else {
            m *= scale(offset);
            m *= translate(0f, -50f); // center it on the player
            m *= scale(level.Zoom);
            m *= translate(playerPos);
        }
        
        var drawPos = new Vector2(m.Translation.X, m.Translation.Y);
        var size3 = getScale(m);
        var size = new Vector2(size3.X, size3.Y);
        //var size = Vector2.One;

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
            new Vector2(BufferSize/2), size / StaminaBar.Settings.UpscaleFactor,
            SpriteEffects.None, 0f
        );
    }
}