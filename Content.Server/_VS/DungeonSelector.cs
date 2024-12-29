using System.Linq;
using Content.Server.Warps;
using Content.Shared._VS;
using Content.Shared.Gravity;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Server._VS;

public sealed class DungeonSelector : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// It's duly important to know that Map ID's and Levels start at 1.
    /// </summary>
    public int DifferenceBetweenStaticDungeons { get; set; } = 1;

    public int NumberOfStaticDungeons { get; set; } = 1; // [UPDATE] Change this with new dungeon additions.

    /// <summary>
    /// Formerly _proceduralGenerator.GetTemplate(lvl) from Mining Station 14,
    /// the handling for procedural dungeons is half-fold here. Depending on level
    /// and level's passed, static hand-made dungeons may be used instead of procedural.
    /// </summary>
    /// <param name="level"></param>
    /// <param name="currentMapEntityUid"></param>
    /// <param name="originalWarperComponent"></param>
    /// <returns></returns>
    public (MapId, string) GetDungeon(int level, EntityUid currentMapEntityUid, WarperComponent originalWarperComponent)
    {
        MapId mapId = new();
        string path = string.Empty;

        // Determining if the dungeon level is some "DifferenceBetweenStaticDungeons" units from the last.
        if (NumberOfStaticDungeons != 0 && level % DifferenceBetweenStaticDungeons == 0)
        {
            // Check if this is within the first five static dungeons
            if (level <= NumberOfStaticDungeons * DifferenceBetweenStaticDungeons)
            {
                // [UPDATE] Change these with new dungeon additions.
                // # DUNGEON #1 :: Subsurface Dungeon
                if (level == 1 * DifferenceBetweenStaticDungeons)
                {
                    (mapId, path) = CreateDungeonFromStaticCVar(CCVars.VaultFloor1Map);
                }

                // # DUNGEON #2 :: ?
                /*if (level == 2 * DifferenceBetweenStaticDungeons)
                {

                }

                if (level == 3 * DifferenceBetweenStaticDungeons)
                {

                }

                if (level == 4 * DifferenceBetweenStaticDungeons)
                {

                }*/
            }
            else
            {
                // The dungeon level is procedural
                (mapId, path) = CreateProceduralDungeon();
            }
        }
        else
        {
            // The dungeon level is procedural
            (mapId, path) = CreateProceduralDungeon();
        }

        return (mapId, path);
    }

    private (MapId, string) CreateDungeonFromStaticCVar(CVarDef<string> cVariableDefinition)
    {
        var mapUid = _mapSystem.CreateMap(out var mapId, true);
        _metaData.SetEntityName(mapUid, Loc.GetString($"{cVariableDefinition.Name}"));
        return (mapId, _cfgManager.GetCVar(cVariableDefinition));
    }

    /// [WIP] UNIMPLEMENTED
    private (MapId, string) CreateProceduralDungeon()
    {
        var d = _mapSystem.CreateMap(out var mapId, true);
        return (mapId, "");
    }

    public void AssignEntrances(
        int dungeonLevel,
        EntityUid fromMapEntityUid,
        MapId toMapEntityUid,
        WarperComponent originalWarperComponent)
    {
        // [WIP] possibly problematic generative code.
        // Create destination back up on current map
        /*var upDest = _entityManager.SpawnEntity("WarpPoint", Transform(fromMapEntityUid).Coordinates);
        if (TryComp<SharedWarperComponent>(upDest, out var upWarp))
        {
            upWarp.DestinationId = $"dlvl{dungeonLevel - 1}down";
        }
        else
        {
            Logger.ErrorS("warper", "Could not find WarpPointComponent when setting return destination");
            return;
        }*/

        #region Assign Return Stairs

        // Find "Go Back Up" stairs in new map
        WarperComponent? warperBackUp = FindStairs(toMapEntityUid, goesUp: true);
        if (warperBackUp == null)
        {
            Logger.ErrorS("warper", "Could not find stairs going back up");
            return;
        }

        // Link "Go Back Up" stairs to the "Go Down" stairs a level above.
        warperBackUp.Id = $"dlvl{dungeonLevel}up";
        warperBackUp.DestinationId = $"dlvl{dungeonLevel - 1}down";

        #endregion

        #region Assign Stairs Down

        // Find "Go Down" stairs in new map
        WarperComponent? warperDown = FindStairs(toMapEntityUid, goesUp: false);
        if (warperDown == null)
        {
            Logger.ErrorS("warper", "Could not find stairs going back up");
            return;
        }

        // Link "Go Down" stairs to the "Go Up" stairs a level below.
        warperDown.Id = $"dlvl{dungeonLevel}down";
        warperDown.DestinationId = $"dlvl{dungeonLevel + 1}up";
        # endregion

        /*
        // Create destination downstairs
        var downDest = _entityManager.SpawnEntity("WarpPoint", Transform(warperBackUp.Owner).Coordinates);
        if (TryComp<SharedWarperComponent>(downDest, out var downWarp) &&
            TryComp<WarpPointComponent>(downDest, out var warpPoint))
        {
            downWarp.DestinationId = $"dlvl{dungeonLevel}up";
            warpPoint.Location = $"Dungeon Level {dungeonLevel:00}";
        }
        else
        {
            Logger.ErrorS("warper", "Could not find WarpPointComponent when setting destination downstairs");
            return;
        }
        // [WIP] This is generative. May be issue.
        originalWarperComponent.DestinationId = $"dlvl{dungeonLevel}up";
        */

    }

    /// <summary>
    /// Initializes the dungeon layer.
    /// </summary>
    /// <param name="dungeonLevel"></param>
    /// <param name="path"></param>
    /// <param name="mapId"></param>
    /// <returns></returns>
    public bool LoadDungeon(int dungeonLevel, string path, MapId mapId)
    {
        if (string.IsNullOrEmpty(path))
        {
            Logger.ErrorS("warper", $"Could not load map {path} for dungeon level {dungeonLevel}. Path returned empty.");
            return false;
        }

        // Map generator relies on the global dungeonLevel, so temporarily set it here
        if (!_map.TryLoad(mapId, path, out var maps))
        {
            Logger.ErrorS("warper", $"Could not load map {path} for dungeon level {dungeonLevel}");
            return false;
        }

        // Prepare map
        var map = maps.First();
        var gravity = _entityManager.EnsureComponent<GravityComponent>(map);
        gravity.Enabled = true;
        _entityManager.DirtyEntity(gravity.Owner);
        return true;
    }

    private WarperComponent? FindStairs(MapId id, bool goesUp = false)
    {
        foreach (var warper in EntityManager.EntityQuery<WarperComponent>())
        {
            if (Transform(warper.Owner).MapID == id && warper.GoesUp == goesUp)
            {
                return warper;
            }
        }

        return null;
    }
}
