namespace Content.Shared._VS;

public abstract partial class SharedWarperComponent : Component
{
    /// Warp destination unique identifier.
    /// This is specifically set on a Ladder / Entrance with intent to
    /// -read- this ID to travel-to. It is NOT the ID of the -current- Ladder / Entrance.
    [ViewVariables(VVAccess.ReadWrite), DataField("id")]
    public string? Id { get; set; }

    /// Warp destination unique identifier.
    /// This is specifically set on a Ladder / Entrance with intent to
    /// -read- this ID to travel-to. It is NOT the ID of the -current- Ladder / Entrance.
    [ViewVariables(VVAccess.ReadWrite), DataField("destinationId")]
    public string? DestinationId { get; set; }

    /// Used as a flag to toggle generation of dungeon destinations.
    [DataField("dungeon"), ViewVariables(VVAccess.ReadWrite)]
    public bool GeneratesDungeon { get; set; } = false;

    /// Does the level need to be completed before it can be used?
    [DataField("requiresCompletion"), AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool RequiresCompletion { get; set; } = true;

    /// <summary>
    /// [UNUSED FOR NOW] This is to assist with determining reverse-generation order for dungeons.
    /// IE: Spawning on a lower layer and working one's way up.
    /// </summary>
    [DataField("goesUp"), ViewVariables(VVAccess.ReadWrite)]
    public bool GoesUp { get; set; } = false;
}
