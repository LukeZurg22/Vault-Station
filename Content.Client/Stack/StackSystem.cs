using System.Linq;
using Content.Client.Items;
using Content.Client.Storage.Systems;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Stack
{
    // Frontier: add partial to class definition
    [UsedImplicitly]
    public sealed partial class StackSystem : SharedStackSystem
    {
        [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly ItemCounterSystem _counterSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StackComponent, AppearanceChangeEvent>(OnAppearanceChange);
            Subs.ItemStatus<StackComponent>(ent => new StackStatusControl(ent));
        }

        public override void SetCount(EntityUid uid, int amount, StackComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            base.SetCount(uid, amount, component);

            if (component.Lingering &&
                TryComp<SpriteComponent>(uid, out var sprite))
            {
                // tint the stack gray and make it transparent if it's lingering.
                var color = component.Count == 0 && component.Lingering
                    ? Color.DarkGray.WithAlpha(0.65f)
                    : Color.White;

                for (var i = 0; i < sprite.AllLayers.Count(); i++)
                {
                    sprite.LayerSetColor(i, color);
                }
            }

            // TODO PREDICT ENTITY DELETION: This should really just be a normal entity deletion call.
            if (component.Count <= 0 && !component.Lingering)
            {
                Xform.DetachEntity(uid, Transform(uid));
                return;
            }

            component.UiUpdateNeeded = true;
        }

        private void OnAppearanceChange(EntityUid uid, StackComponent comp, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null || comp.LayerStates.Count < 1)
                return;

            StackLayerData data = new(); // Frontier: use structure to store StackLayerData

            // Skip processing if no actual
            if (!_appearanceSystem.TryGetData<int>(uid, StackVisuals.Actual, out data.Actual, args.Component))
                return;

            if (!_appearanceSystem.TryGetData<int>(uid, StackVisuals.MaxCount, out data.MaxCount, args.Component))
                data.MaxCount = comp.LayerStates.Count;

            if (!_appearanceSystem.TryGetData<bool>(uid, StackVisuals.Hide, out data.Hidden, args.Component))
                data.Hidden = false;

            if (comp.LayerFunction != StackLayerFunction.None)  // Frontier: use stack layer function to modify appearance if provided.
                ApplyLayerFunction(uid, comp, ref data);        // Frontier: definition in _NF/Stack/StackSystem.Layers.cs

            if (comp.IsComposite)
                _counterSystem.ProcessCompositeSprite(uid, data.Actual, data.MaxCount, comp.LayerStates, data.Hidden, sprite: args.Sprite);
            else
                _counterSystem.ProcessOpaqueSprite(uid, comp.BaseLayer, data.Actual, data.MaxCount, comp.LayerStates, data.Hidden, sprite: args.Sprite);
        }
    }
}
