using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Procedural;
using Content.Server.Warps;
using Content.Shared._VS;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Parallax;
using Content.Shared.Procedural;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

#pragma warning disable CS0162 // Unreachable code detected

#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).

namespace Content.Server._VS;

public sealed class DungeonSelector : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly DungeonSystem _dungeonSystem = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("dungeonSelector");

    /// <summary>
    /// For temporary extra effects like dynamic lighting based on depth.
    /// <br/><br/>
    /// NOT REFLECTIVE OF HOW ACTUAL DUNGEON LEVEL HANDLING IS DONE.
    /// </summary>
    private int tempDungeonLevel = 0;

    /// <summary>
    /// It's duly important to know that Map ID's and Levels start at 1.
    /// </summary>
    private const int DifferenceBetweenStaticDungeons = 1;

    /// <summary>
    /// The number of hand-made dungeons available in the CCVariables and manually made addition here.
    /// <br/><br/>
    /// [UPDATE] Update this and the handling for generating dungeons if you wish to add more dungeons.
    /// There may be a better implementation in the future.
    /// </summary>
    private const int NumberOfStaticDungeons = 1;

    /// <summary>
    /// Formerly _proceduralGenerator.GetTemplate(lvl) from Mining Station 14,
    /// the handling for procedural dungeons is half-fold here. Depending on level
    /// and level's passed, static hand-made dungeons may be used instead of procedural.
    /// </summary>
    /// <param name="level"></param>
    /// <param name="currentMapEntityUid"></param>
    /// <param name="originalWarperComponent"></param>
    /// <returns></returns>
    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
    public bool HandleGenerateDungeon(int level, EntityUid currentMapEntityUid, WarperComponent originalWarperComponent)
    {
        tempDungeonLevel = level;
        MapId? mapId;

        // If Level lands on a static dungeon
        if (NumberOfStaticDungeons != 0 && (level + 1) % DifferenceBetweenStaticDungeons == 0)
        {
            // If Level is on one of the limited number of hand-made levels.
            if (level / DifferenceBetweenStaticDungeons <= NumberOfStaticDungeons)
            {
                (_, mapId) = (level / DifferenceBetweenStaticDungeons) switch // [WIP] might not work right.
                {
                    // Special handling for specific levels
                    1 => // DUNGEON #1 :: Subsurface Dungeon
                        CreateDungeonFromStaticCVar(CCVars.VaultFloor1Map),
                    2 => // DUNGEON #2
                        CreateDungeonFromStaticCVar(CCVars.VaultFloor2Map),
                };

                // Temporary
                return mapId != null;
            }
            else
            {
                // This area occurs when a static dungeon that *should* be here, isn't here because of
                // a lack of static dungeons available. This section was left here such that difficulty
                // tweaks could be done for dungeons every X amount.

                DetermineProceduralDungeon(level);
            }
        }
        else
        {
            DetermineProceduralDungeon(level);
        }

        return true;
    }

    /// <summary>
    /// Creates a procedural dungeon.
    /// </summary>
    private void DetermineProceduralDungeon(int level)
    {
        // ChunkDebris
        // ClusterAsteroid
        // Experiment
        // Haunted
        // LavaBrig
        // Mineshaft
        // PlanetBase
        // SnowyLabs
        // SpindlyAsteroid
        // SwissCheeseAsteroid
        // VGRoid
        // VGRoidBlob
        // VGRoidExterior
        // VGRoidExteriorDungeons

        string selectedPreset;
        switch (level)
        {
            case <= DifferenceBetweenStaticDungeons * 1:
                selectedPreset = "Mineshaft";
                break;
            case <= DifferenceBetweenStaticDungeons * 2:
                selectedPreset = "Haunted";
                break;
            case <= DifferenceBetweenStaticDungeons * 3:
                selectedPreset = "Experiment";
                break;
            case > DifferenceBetweenStaticDungeons * 5:
                selectedPreset = "LavaBrig";
                break;
            default:
                selectedPreset = "MineShaft";
                break;
        }

        if (!GenerateProceduralDungeon(dungeonPreset: selectedPreset, level))
        {
            _sawmill.Error(
                $"GenerateProceduralDungeon({selectedPreset},{level}) :: Failed to generate procedural dungeon.");
        }
    }

    /// <summary>
    /// Just makes the Map entity, the map ID, and the metadata for the map.
    /// </summary>
    (EntityUid, MapId, MetaDataComponent?) CreateMapProto(string mapName)
    {
        var mapUid = _mapSystem.CreateMap(out var mapId, runMapInit: false);
        _metaData.SetEntityName(mapUid, mapName);
        MetaDataComponent? metadata = _entityManager.EnsureComponent<MetaDataComponent>(mapUid);
        return (mapUid, mapId, metadata);
    }

    /// <summary>
    /// Creates a dungeon that's been hand made, lovingly crafted, and inserted into the system.
    /// </summary>
    /// <param name="cVariableDefinition"></param>
    /// <returns></returns>
    private (bool, MapId?) CreateDungeonFromStaticCVar(CVarDef<string> cVariableDefinition)
    {
        // Metadata
        var path = _cfgManager.GetCVar(cVariableDefinition);
        var (_, mapId, metadata) =
            CreateMapProto(Loc.GetString($"{cVariableDefinition.Name}"));

        if (!_map.TryLoad(mapId, path, out var maps))
        {
            _sawmill.Error(
                $"CreateDungeonFromStaticCVar({cVariableDefinition}) :: Could not load map {path} for dungeon level {tempDungeonLevel}.");
            return (false, null);
        }

        var map = maps[0];
        if (!ApplyDungeonComponents(map, metadata))
        {
            _sawmill.Error(
                $"ApplyDungeonComponents({map},{metadata}) :: Failed to apply dungeon components for level {tempDungeonLevel}.");
            return (false, null);
        }

        _mapManager.DoMapInitialize(mapId);
        _mapManager.SetMapPaused(mapId, false);

        var flag = AssignEntrances(tempDungeonLevel, mapId);
        return (flag, mapId);
    }

    /// <summary>
    /// Adds the necessary components to a dungeon.
    /// </summary>
    /// <param name="map"></param>
    /// <param name="metadata"></param>
    bool ApplyDungeonComponents(EntityUid map, MetaDataComponent? metadata)
    {
        // Gravity
        var gravity = _entityManager.EnsureComponent<GravityComponent>(map);
        gravity.Enabled = true;
        _entityManager.Dirty(map, gravity, metadata);

        // Atmospherics
        var atmos = _entityManager.EnsureComponent<MapAtmosphereComponent>(map);
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        var gasMixture = new GasMixture(moles, 293.15f);
        gasMixture.AdjustMoles(Gas.Oxygen, 21);
        gasMixture.AdjustMoles(Gas.Nitrogen, 79);
        _entityManager.System<AtmosphereSystem>().SetMapAtmosphere(map, false, gasMixture);
        _entityManager.System<AtmosphereSystem>()
            .SetMapGasMixture(map, new GasMixture(moles.ToArray(), 293.15f), atmos); // Temperature is tolerable by default.

        // Ambient Lighting Color
        const float paceOfChange = 0.001f; // How fast the dungeon gets dark.
        const float initialIntensity = 0;
        var intensity = (initialIntensity) + tempDungeonLevel * paceOfChange;
        var colorValue = Math.Clamp(intensity, 0, 1);
        Color ambientColour = tempDungeonLevel switch
        {
            // Makes colors vary depending on some series' of levels in the dungeon.
            <= 10 => new Color(0, colorValue, 0),
            <= 20 => new Color(0, 0, colorValue),
            <= 30 => new Color(colorValue, 0, 0),
            _ => new Color(colorValue, colorValue, colorValue)
        };
        var lighting = _entityManager.EnsureComponent<MapLightComponent>(map);
        lighting.AmbientLightColor = ambientColour;
        _entityManager.Dirty(map, lighting);

        // Parallax
        var parallax = _entityManager.EnsureComponent<ParallaxComponent>(map);
        parallax.Parallax = "Dirt";
        return true;
    }

    /// <summary>
    /// Note that [dungeon_presets.yml] is where the presets are.
    /// They are strings, and not defined in code.
    /// </summary>
    /// <param name="dungeonPreset"></param>
    /// <param name="dungeonLevel"></param>
    bool GenerateProceduralDungeon(string dungeonPreset, int dungeonLevel)
    {
        var (mapUid, mapId, metadata) =
            CreateMapProto($"Dungeon Layer {tempDungeonLevel}");

        // [NOTE] This was the best way I could handle it at the moment.
        // Despite all of my searching, this was the first and only thing to come u pthat seems like it would work.
        var completionResult = CompletionResult.FromHintOptions(
            CompletionHelper.PrototypeIDs<DungeonConfigPrototype>(proto: _prototypeManager),
            "");
        var dungeonPrototype = completionResult.Options.FirstOrDefault(o => o.Value == dungeonPreset).Value;
        if (string.IsNullOrEmpty(dungeonPrototype))
        {
            _sawmill.Error($"GenerateProceduralDungeon dungeon preset {dungeonPreset} is invalid.");
            return false;
        }

        if (!_prototypeManager.TryIndex<DungeonConfigPrototype>(dungeonPrototype, out var dungeon))
        {
            _sawmill.Error($"GenerateProceduralDungeon({dungeonPreset},{dungeonLevel}) :: " +
                           $"Failed to select a working dungeon preset : \"{dungeonPreset}\" for level {dungeonLevel}.");
            return false;
        }

        var position = new Vector2i(0, 0); // Spawn at the center of the map (0,0)

        if (!TryComp<MapGridComponent>(mapUid, out var dungeonGrid))
        {
            dungeonGrid = EntityManager.AddComponent<MapGridComponent>(mapUid);
            // If we created a grid (e.g. space dungen) then offset it so we don't double-apply positions
            position = Vector2i.Zero;
        }

        // Seed is based on Dungeon Level.
        var seed = dungeonLevel <= 0 ? new Random().Next(dungeonLevel) : new Random().Next();

        _dungeonSystem.GenerateDungeon(dungeon, mapUid, dungeonGrid, position, seed);
        if (!ApplyDungeonComponents(mapUid, metadata))
        {
            _sawmill.Error($"ApplyDungeonComponents({mapUid},{metadata}) :: " +
                           $"Failed to apply dungeon components for level {tempDungeonLevel}.");
            return false;
        }

        _mapManager.DoMapInitialize(mapId);
        _mapManager.SetMapPaused(mapId, false);

        if (!GenerateEntrances(tempDungeonLevel, mapId))
        {
            return false;
        }

        return true;
    }

    private bool GenerateEntrances(int level, MapId mapId)
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
        return true;
    }

    private bool AssignEntrances(int dungeonLevel, MapId toMapEntityUid)
    {
        // Find "Go Back Up" stairs in new map
        WarperComponent? warperBackUp = FindStairs(toMapEntityUid, goesUp: true);
        if (warperBackUp == null)
        {
            _sawmill.Error($"warper :: " +
                           $"Could not find stairs going back up.");
            return false;
        }

        // Link "Go Back Up" stairs to the "Go Down" stairs a level above.
        warperBackUp.Id = $"dlvl{dungeonLevel}up";
        warperBackUp.DestinationId = $"dlvl{dungeonLevel - 1}down";

        // Assigning warp point name fpr Ghost's sake.
        if (!TryComp<WarpPointComponent>(warperBackUp.Owner, out var warpPoint))
            return false;
        warpPoint.Location = $"Vault Layer {dungeonLevel:00}";

        // Find "Go Down" stairs in new map
        WarperComponent? warperDown = FindStairs(toMapEntityUid, goesUp: false);
        if (warperDown == null)
        {
            _sawmill.Error($"warper :: Could not find stairs going down.");
            return false;
        }

        // Link "Go Down" stairs to the "Go Up" stairs a level below.
        warperDown.Id = $"dlvl{dungeonLevel}down";
        warperDown.DestinationId = $"dlvl{dungeonLevel + 1}up";
        warperDown.GeneratesDungeon = true; // Enabled so that static dungeons will generate dungeon layers below them.

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
