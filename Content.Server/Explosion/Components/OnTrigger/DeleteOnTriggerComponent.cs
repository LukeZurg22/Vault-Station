using Content.Server.Explosion.EntitySystems;

namespace Content.Server.Explosion.Components.OnTrigger;

/// <summary>
/// Will delete the attached entity upon a <see cref="TriggerEvent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DeleteOnTriggerComponent : Component
{
}
