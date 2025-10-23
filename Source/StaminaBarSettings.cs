namespace Celeste.Mod.StaminaBar;

public class StaminaBarSettings : EverestModuleSettings {
    public bool Enabled { get; set; } = true;
    public bool AlwaysVisible { get; set; } = false;

    public enum BarPosition {
        TopRight,
        BottomRight,
        TopLeft,
        BottomLeft,
        HUD,
    }
    
    [SettingSubMenu]
    public class PositionSubMenu {
        public BarPosition Alignment { get; set; } = BarPosition.TopRight;
        [SettingRange(min: -1920, max: 1920, largeRange: true)]
        public int OffsetX { get; set; } = 0;
        [SettingRange(min: -1080, max: 1080, largeRange: true)]
        public int OffsetY { get; set; } = 0;
    }
    
    [SettingSubHeader("modoptions_staminabar_display")]
    public PositionSubMenu Position { get; set; } = new();

    [SettingSubMenu]
    public class SizeSubMenu {
        [SettingRange(min: 0, max: 128)]
        public int PieOuterRadius { get; set; } = 26;
        [SettingRange(min: 0, max: 128)]
        public int PieInnerRadius { get; set; } = 8;
        [SettingRange(min: 4, max: 64)]
        public int PieResolution { get; set; } = 30;
        [SettingRange(min: 0, max: 128)]
        public int PieOutlineThickness { get; set; } = 3;
        [SettingRange(min: 0, max: 128)]
        public int OuterPieThickness { get; set; } = 7;
        [SettingRange(min: 0, max: 128)]
        public int OuterPieOutlineThickness { get; set; } = 2;
    }
    
    public SizeSubMenu Size { get; set; } = new();

    [SettingSubMenu]
    public class ColorSubMenu {
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        public string Back { get; set; } = "030303";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        [SettingSubText("modoptions_staminabar_overlaycolor_desc")]
        public string Overlay { get; set; } = "030303";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        public string Stamina { get; set; } = "69ff47";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        [SettingSubText("modoptions_staminabar_staminaflashcolor_desc")]
        public string StaminaFlash { get; set; } = "ffffff";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        public string StaminaRefund { get; set; } = "ffffff";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        public string StaminaLoss { get; set; } = "ff3030";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        [SettingSubText("modoptions_staminabar_staminadangercolor_desc")]
        public string StaminaDanger { get; set; } = "ff3030";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        [SettingSubText("modoptions_staminabar_staminadangerrefundcolor_desc")]
        public string StaminaDangerRefund { get; set; } = "ff7c73";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        public string StaminaOverdraft { get; set; } = "818181";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        public string StaminaDangerBar { get; set; } = "555555";
        [SettingMinLength(6)]
        [SettingMaxLength(6)]
        public string StaminaOverclock { get; set; } = "63e5ff";
    }

    public ColorSubMenu Color { get; set; } = new();

    [SettingSubMenu]
    public class SquishSubMenu {
        [SettingRange(min: 0, max: 20)]
        [SettingSubText("modoptions_staminabar_squish_desc")]
        public int SquishFactor { get; set; } = 4;
        [SettingSubText("modoptions_staminabar_squishoncrouch_desc")]
        public bool SquishOnCrouch { get; set; } = true;   
    }

    public SquishSubMenu Squish { get; set; } = new();
    
    [SettingSubText("modoptions_staminabar_showdanger_desc")]
    public bool ShowDanger { get; set; } = true;
    [SettingSubText("modoptions_staminabar_showlosses_desc")]
    public bool ShowLosses { get; set; } = true;
    [SettingSubText("modoptions_staminabar_showrefunds_desc")]
    public bool ShowRefunds { get; set; } = true;
    [SettingSubText("modoptions_staminabar_showoverdraft_desc")]
    public bool ShowOverdraft { get; set; } = true;
    [SettingSubText("modoptions_staminabar_showoverclock_desc")]
    public bool ShowOverclock { get; set; } = true;
    [SettingSubText("modoptions_staminabar_upscalefactor_desc")]
    public int UpscaleFactor { get; set; } = 2;
    
    [SettingSubHeader("modoptions_staminabar_vanity")]
    public bool Bunny { get; set; } = false;
    public bool Kitty { get; set; } = false;
    public bool Fairy { get; set; } = false;
    public bool Moth { get; set; } = false;
}