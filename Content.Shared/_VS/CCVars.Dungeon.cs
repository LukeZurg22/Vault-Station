using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._VS;

[CVarDefs]
public sealed partial class CCVars : CVars
{
    /// <summary>
    ///     The map to use for the first dungeon floor.
    /// </summary>
    public static readonly CVarDef<string> VaultFloor1Map =
        CVarDef.Create("dungeon-entry-1", "/Maps/Vaults/WarperTest.yml", CVar.SERVERONLY);
}
