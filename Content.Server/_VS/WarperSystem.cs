using System.Linq;
using System.Numerics;
using Content.Server._VS;
using Content.Server.Administration;
using Content.Server.Bible.Components;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared._VS;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Content.Server.Warps;

/// <summary>
/// This is lifted from Mining Station 14 and redesigned to be used for Vault Station's Dungeon Layers.
/// <br/><br/>
/// The dungeon layers are a pseudo static mix of pre-defined and procedurally generated maps.
/// <br/><br/>
/// As such, this system is designed to accomodate this arrangement and manual changes will be
/// necessary in order to accomplish procedural-only levels.
/// -Z
/// </summary>
public sealed class WarperSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly DungeonSelector _dungeonSelector = default!;

    [Dependency] private readonly TagSystem _tags = default!;
    // [Dependency] private readonly ... See DungeonJob.cs [IMPL]
    // See about ExternalWindowDunGen & etc. classes for better random procedural dungeons.

    /// <summary>
    /// The Current Deepest-Depths level explored.
    /// </summary>
    public int DungeonLevel = 0;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarperComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<WarperComponent, MoveFinished>(OnMoveFinished);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    # region Events
    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        DungeonLevel = 0;
    }

    private void RandomAnnounce(EntityUid uid, int dlvl)
    {
        string announcement;
        if (dlvl == 1)
            announcement = Loc.GetString("dungeon-enter-announce");
        else
            return;
        var sender = Loc.GetString("admin-announce-announcer-default");
        _chatSystem.DispatchStationAnnouncement(uid, announcement, sender);
    }
    #endregion

    /// <summary>
    /// Finds a Ladder / Entrance point that has an ID equal to the destination ID.
    /// </summary>
    /// <param name="queryWarpPointId"></param>
    /// <returns></returns>
    private static EntityUid? FindDestinationWarperPoint(string queryWarpPointId)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var foundComponent = entityManager.EntityQuery<WarperComponent>(true)
            .FirstOrDefault(p => p.Id == queryWarpPointId);
        return foundComponent?.Owner;
    }

    private bool GenerateDungeon(EntityUid currentMapEntityUid, WarperComponent originalWarperComponent)
    {
        // Destination is next level
        int lvl = DungeonLevel + 1;
        var (mapId, path) = _dungeonSelector.GetDungeon(lvl, currentMapEntityUid, originalWarperComponent);

        // If the dungeon doesn't load, then
        if (!_dungeonSelector.LoadDungeon(lvl, path, mapId))
        {
            Logger.ErrorS("LoadDungeon", $"Could not load path {path} @ dungeon level {lvl} in mapID {mapId}.");
            return false;
        }
        DungeonLevel = lvl;
        _dungeonSelector.AssignEntrances(lvl, currentMapEntityUid, mapId, originalWarperComponent);

        // Find Ladder Up

        // Find Ladder Down

        /*if (string.IsNullOrEmpty(path))
        {
            Logger.ErrorS("warper", $"Could not load map {path} for dungeon level {lvl}. Path returned empty.");
            return false;
        }


        // Map generator relies on the global dungeonLevel, so temporarily set it here
        DungeonLevel = lvl;
        if (!_map.TryLoad(mapId, path, out var maps))
        {
            Logger.ErrorS("warper", $"Could not load map {path} for dungeon level {lvl}");
            DungeonLevel = lvl - 1;
            return false;
        }

        // Prepare map
        var map = maps.First();
        var gravity = _entityManager.EnsureComponent<GravityComponent>(map);
        gravity.Enabled = true;
        _entityManager.DirtyEntity(gravity.Owner);*/

        // [NOTE] [WIP] Move a HUUUGE chunk of the below into DungeonSelector.cs
        // Dungeon selector will handle the ordaining of dungeon components & etc. This is
        // just a warper system for warping, and shall do nothing more.
        // Also statically-made maps might have some issues with destination ID's.

        /*
        // Create destination back up on current map
        var upDest = _entityManager.SpawnEntity("WarpPoint", Transform(currentMapEntityUid).Coordinates);
        if (TryComp<WarperComponent>(upDest, out var upWarp))
        {
            upWarp.DestinationId = $"dlvl{lvl - 1}down";
        }
        else
        {
            Logger.ErrorS("warper", "Could not find WarpPointComponent when setting return destination");
            return false;
        }

        // find stairs up in new map
        WarperComponent? warperBackUp = FindStairs(mapId);
        if (warperBackUp == null)
        {
            Logger.ErrorS("warper", "Could not find stairs going back up");
            return false;
        }

        // link back upstairs
        warperBackUp.DestinationId = $"dlvl{lvl - 1}down";

        // Create destination downstairs
        var downDest = _entityManager.SpawnEntity("WarpPoint", Transform(warperBackUp.Owner).Coordinates);
        if (TryComp<WarperComponent>(downDest, out var downWarp) &&
            TryComp<WarpPointComponent>(downDest, out var warpPoint))
        {
            downWarp.DestinationId = $"dlvl{lvl}up";
            warpPoint.Location = $"Dungeon Level {lvl:00}";
        }
        else
        {
            Logger.ErrorS("warper", "Could not find WarpPointComponent when setting destination downstairs");
            return false;
        }
        originalWarperComponent.DestinationId = $"dlvl{lvl}up";
        */

        RandomAnnounce(currentMapEntityUid, DungeonLevel);
        return true;
    }

    /// <summary>This means Statically-made stairs must have a set id.</summary>
    /// <param name="id">The map ID of the player activating the stairs.</param>
    /// <param name="goesUp"></param>
    /// <returns>The first WarperComponent found on the Map ID provided.</returns>
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

    private bool CanDescend(EntityUid uid, WarperComponent component)
    {
        if (component.GeneratesDungeon && component.RequiresCompletion)
            return IsDungeonComplete(uid);
        else
            return true;
    }

    /// <summary>
    /// Checks whether a dungeon level is complete if a certain amount of monsters within the dungeon are dead.
    /// This is specific to all monsters containing one or more faction tags.
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    private bool IsDungeonComplete(EntityUid uid)
    {
        // Faction ID's of who should not be present in the current dungeon level.
        var targetFactionIds = new List<string>
        {
            // SS14
            "Dragon", "Xeno", "Zombie", "SimpleHostile", "AllHostile",
            // Vault Station
            "ChaosCultistHuemac", "ChaosCultistOldGod",
        };

        int monsterCount = 0, aliveCount = 0;
        foreach (var entity in _entityManager.EntityQuery<NpcFactionMemberComponent>())
        {
            // NPCs not on the same map - skipped
            if (Transform(entity.Owner).MapID != Transform(uid).MapID)
                continue;
            // NPC not of a hostile faction - skipped
            if (!entity.Factions.Any(faction => targetFactionIds.Contains(faction.ToString())))
                continue;
            // NPC is a pet - skipped
            if (HasComp<FamiliarComponent>(entity.Owner))
                continue;
            // Add to monster count.
            monsterCount++;
            // If it's dead, don't continue.
            if (_mobState.IsDead(entity.Owner))
                continue;
            // Monster is a Boss - dungeon is straight up NOT DONE.
            if (_tags.HasTag(entity.Owner, "Boss"))
                return false;
            // So if it's not a pet, not dead, not a boss,
            aliveCount++;
        }

        // 20% bottom limit for how many hostiles could be alive to be considered complete.
        return aliveCount <= 0.2 * monsterCount;
    }

    private void ForceDescent(EntityUid uid, WarperComponent component, ActivateInWorldEvent args, bool forced = true)
    {
        if (!CanDescend(uid, component) && forced == false)
        {
            var message = Loc.GetString("dungeon-level-not-clear");

            _popupSystem.PopupEntity(message, args.User); // Small PopupType by default.
            return;
        }

        if (component.GeneratesDungeon && GenerateDungeon(uid, component))
        {
            // Flag Dungeon to not continue generating.
            component.GeneratesDungeon = false;
        }

        DoWarp(args.Target, args.User, args.User, component);
    }

    private void OnActivate(EntityUid uid, WarperComponent component, ActivateInWorldEvent args)
    {
        // The Ladder Goes Down
        //if (component.GoesDown)
        //{
        if (!CanDescend(uid, component))
        {
            var message = Loc.GetString("dungeon-level-not-clear");

            _popupSystem.PopupEntity(message, args.User); // Small PopupType by default.
            return;
        }

        if (component.GeneratesDungeon && GenerateDungeon(uid, component))
        {
            // Flag Dungeon False so this entrance doesn't keep generating dungeons.
            component.GeneratesDungeon = false;
        }
        //}

        // [IMPL] Code here for reverse-order dungeoneering.

        DoWarp(args.Target, args.User, args.User, component);
    }

    private void DoWarp(EntityUid uid, EntityUid user, EntityUid victim, WarperComponent component)
    {
        if (component.DestinationId is null)
        {
            Logger.DebugS("warper", "Warper has no destination");
            var message = Loc.GetString("warper-goes-nowhere", ("warper", uid));
            _popupSystem.PopupEntity(message, user);
            return;
        }

        var dest = FindDestinationWarperPoint(component.DestinationId);
        if (dest is null)
        {
            Logger.DebugS("warper", $"Warp destination '{component.DestinationId}' not found");
            var message = Loc.GetString("warper-goes-nowhere", ("warper", uid));
            _popupSystem.PopupEntity(message, user);
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        entityManager.TryGetComponent(dest.Value, out TransformComponent? destXform);
        if (destXform is null)
        {
            Logger.DebugS("warper", $"Warp destination '{component.DestinationId}' has no transform");
            var message = Loc.GetString("warper-goes-nowhere", ("warper", uid));
            _popupSystem.PopupEntity(message, user);
            return;
        }

        // Check that the destination map is initialized and return unless in aghost mode.
        var mapManager = IoCManager.Resolve<IMapManager>();
        var destinationMap = destXform.MapID;
        if (!mapManager.IsMapInitialized(destinationMap) || mapManager.IsMapPaused(destinationMap))
        {
            if (!entityManager.HasComponent<GhostComponent>(user))
            {
                // Normal ghosts cannot interact, so if we're here this is already an admin ghost.
                Logger.DebugS("warper",
                    $"Player tried to warp to '{component.DestinationId}', which is not on a running map");
                var message = Loc.GetString("warper-goes-nowhere", ("warper", uid));
                _popupSystem.PopupEntity(message, user);
                return;
            }
        }

        var transform = entityManager.GetComponent<TransformComponent>(victim);
        transform.Coordinates = destXform.Coordinates;
        transform.AttachToGridOrMap();
        if (entityManager.TryGetComponent(victim, out PhysicsComponent? _))
        {
            _physics.SetLinearVelocity(victim, Vector2.Zero);
        }
    }

    # region Commands

    private sealed class MoveFinished : EntityEventArgs
    {
        public EntityUid VictimUid;
        public EntityUid UserUid;

        public MoveFinished(EntityUid userUid, EntityUid victimUid)
        {
            UserUid = userUid;
            VictimUid = victimUid;
        }
    }

    private void OnMoveFinished(EntityUid uid, WarperComponent component, MoveFinished args)
    {
        DoWarp(component.Owner, args.UserUid, args.VictimUid, component);
    }


    [AdminCommand(AdminFlags.Debug)]
    private sealed class NextLevelCommand : IConsoleCommand
    {
        public string Command => "nextlevel";
        public string Description => "Find the ladder down and force-apply it";
        public string Help => "nextlevel";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player?.AttachedEntity == null /*|| shell.Player.AttachedEntityTransform == null*/)
            {
                shell.WriteLine("You need a player and attached entity to use this command.");
                return;
            }

            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var entityManager = IoCManager.Resolve<EntityManager>();
            var warper = sysMan.GetEntitySystem<WarperSystem>();
            var player = shell.Player.AttachedEntity.Value;
            var mapId = entityManager.GetComponent<TransformComponent>(player).MapID;
            var stairsDown = warper.FindStairs(mapId);
            if (stairsDown == null)
            {
                shell.WriteLine("No stairs found!");
                return;
            }

            if (stairsDown.RequiresCompletion && !warper.IsDungeonComplete(stairsDown.Owner))
            {
                shell.WriteLine("The level is not complete but you force your way down anyway.");
            }

            warper.ForceDescent(
                stairsDown.Owner,
                stairsDown,
                new ActivateInWorldEvent(player, stairsDown.Owner, false)
            );
        }
    }

    [AdminCommand(AdminFlags.Debug)]
    private sealed class CurrentLevel : IConsoleCommand
    {
        public string Command => "currentlevel";
        public string Description => "Returns the current level.";
        public string Help => "currentlevel";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var warper = sysMan.GetEntitySystem<WarperSystem>();

            shell.WriteLine($"Current Dungeon Level: {warper.DungeonLevel}");
        }
    }

    #endregion
}
