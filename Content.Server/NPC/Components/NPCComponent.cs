using Content.Shared.NPC;

namespace Content.Server.NPC.Components;

public abstract partial class NPCComponent : SharedNPCComponent
{
    /// <summary>
    /// Contains all the world data for a particular NPC in terms of how it sees the world.
    /// </summary>
    [DataField("blackboard", customTypeSerializer: typeof(NPCBlackboardSerializer))]
    public NPCBlackboard Blackboard = new();
}
