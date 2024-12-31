using System.ComponentModel;
using Content.Server.Warps;
using Content.Shared._VS;

namespace Content.Server._VS;

/// <summary>
/// Allows an interactable entity to be used as a "teleporter" to a different map.
/// </summary>
[RegisterComponent, AutoGenerateComponentState(fieldDeltas: false), Access(typeof(WarperSystem))]
public sealed partial class WarperComponent : SharedWarperComponent
{
}
