using System;
using MonoMod.ModInterop;

namespace Celeste.Mod.StaminaBar;

public class StaminaBar : EverestModule {
    public static StaminaBar Instance { get; private set; }

    public override Type SettingsType => typeof(StaminaBarSettings);
    public static StaminaBarSettings Settings => (StaminaBarSettings)Instance._Settings;

    public static EverestModuleMetadata MotionSmoothingMod = new() {
        Name = "MotionSmoothing",
        Version = new Version(1, 1 ,1)
    };

    public StaminaBar() {
        Instance = this;
    }

    public override void Load() {
        On.Celeste.Level.LoadLevel += LevelOnLoadLevel;
        typeof(ExtendedVariantsImports).ModInterop();
    }

    public override void Unload() {
        On.Celeste.Level.LoadLevel -= LevelOnLoadLevel;
    }

    private static void LevelOnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
        orig(self, playerIntro, isFromLoader);
        var player = self.Tracker.GetEntity<Player>();
        if (player == null) return;
        self.Add(new StaminaBarEntity(player));
    }
}