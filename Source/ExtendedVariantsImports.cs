using System;
using MonoMod.ModInterop;

namespace Celeste.Mod.StaminaBar;

[ModImportName("ExtendedVariantMode")]
public class ExtendedVariantsImports {
    public static Func<string, object> GetCurrentVariantValue;
}