using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._VS;

[CVarDefs]
public sealed partial class CCVars : CVars
{
    public static readonly CVarDef<string> VaultFloor1Map =
        CVarDef.Create("dungeon-entry-1", "/Maps/Vaults/WarperTest.yml", CVar.SERVERONLY);

    public static readonly CVarDef<string> VaultFloor2Map =
        CVarDef.Create("dungeon-entry-2", "/Maps/Vaults/WarperTest.yml", CVar.SERVERONLY);
}
