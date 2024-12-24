using System.Linq;
using Content.Shared.Access;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using static Content.Shared.Access.Components.AccessOverriderComponent;

namespace Content.Client.Access.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class AccessOverriderWindow : DefaultWindow
    {
        private readonly Dictionary<string, Button> _accessButtons = new();

        public event Action<List<ProtoId<AccessLevelPrototype>>>? OnSubmit;

        public AccessOverriderWindow()
        {
            RobustXamlLoader.Load(this);
        }

        public void SetAccessLevels(IPrototypeManager protoManager, List<ProtoId<AccessLevelPrototype>> accessLevels)
        {
            _accessButtons.Clear();
            AccessLevelGrid.DisposeAllChildren();

            foreach (var access in accessLevels)
            {
                if (!protoManager.TryIndex(access, out var accessLevel))
                {
                    continue;
                }

                var newButton = new Button
                {
                    Text = accessLevel.GetAccessLevelName(),
                    ToggleMode = true,
                };

                AccessLevelGrid.AddChild(newButton);
                _accessButtons.Add(accessLevel.ID, newButton);
                newButton.OnPressed += _ =>
                {
                    OnSubmit?.Invoke(
                        // Iterate over the buttons dictionary, filter by `Pressed`, only get key from the key/value pair
                        _accessButtons.Where(x => x.Value.Pressed).Select(x => new ProtoId<AccessLevelPrototype>(x.Key)).ToList());
                };
            }
        }

        public void UpdateState(IPrototypeManager protoManager, AccessOverriderBoundUserInterfaceState state)
        {
            PrivilegedIdLabel.Text = state.PrivilegedIdName;
            PrivilegedIdButton.Text = state.IsPrivilegedIdPresent
                ? Loc.GetString("access-overrider-window-eject-button")
                : Loc.GetString("access-overrider-window-insert-button");

            TargetNameLabel.Text = state.TargetLabel;
            TargetNameLabel.FontColorOverride = state.TargetLabelColor;

            MissingPrivilegesLabel.Text = "";
            MissingPrivilegesLabel.FontColorOverride = Color.Yellow;

            MissingPrivilegesText.Text = "";
            MissingPrivilegesText.FontColorOverride = Color.Yellow;

            if (state.MissingPrivilegesList != null && state.MissingPrivilegesList.Any())
            {
                var missingPrivileges = new List<string>();
                foreach (string tag in state.MissingPrivilegesList)
                {
                    var privilege = Loc.GetString(protoManager.Index<AccessLevelPrototype>(tag)?.Name ?? "generic-unknown");
                    missingPrivileges.Add(privilege);
                }

                MissingPrivilegesLabel.Text = Loc.GetString("access-overrider-window-missing-privileges");
                MissingPrivilegesText.Text = string.Join(", ", missingPrivileges);
            }

            var interfaceEnabled = state.IsPrivilegedIdPresent && state.IsPrivilegedIdAuthorized;

            foreach (var (accessName, button) in _accessButtons)
            {
                button.Disabled = !interfaceEnabled;
                if (!interfaceEnabled)
                    continue;
                button.Pressed = state.TargetAccessReaderIdAccessList?
                    .Select(protoId => protoId.ToString())
                    .Contains(accessName) ?? false;
                button.Disabled = (!state.AllowedModifyAccessList?
                    .Select(protoId => protoId.ToString())
                    .Contains(accessName)) ?? true;
            }
        }
    }
}
