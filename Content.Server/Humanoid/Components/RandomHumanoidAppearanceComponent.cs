namespace Content.Server.Humanoid.Components;

[RegisterComponent]
public sealed partial class RandomHumanoidAppearanceComponent : Component
{
    [DataField("randomizeName")] public bool RandomizeName = true;
    /// <summary>
    /// After randomizing, sets the hair style to this, if possible
    /// </summary>
    [DataField] public string? Hair = null;
}
