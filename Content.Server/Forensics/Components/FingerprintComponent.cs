namespace Content.Server.Forensics.Components
{
    /// <summary>
    /// This component is for mobs that leave fingerprints.
    /// </summary>
    [RegisterComponent]
    public sealed partial class FingerprintComponent : Component
    {
        [DataField("fingerprint"), ViewVariables(VVAccess.ReadWrite)]
        public string? Fingerprint;
    }
}
