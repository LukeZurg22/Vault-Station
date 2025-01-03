using Content.Server.Body.Components;
using Content.Server.DoAfter;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Forensics.Components;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Forensics;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Forensics.Systems
{
    public sealed class ForensicsSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<Components.FingerprintComponent, ContactInteractionEvent>(OnInteract);
            SubscribeLocalEvent<Components.FingerprintComponent, MapInitEvent>(OnFingerprintInit);
            SubscribeLocalEvent<Components.DnaComponent, MapInitEvent>(OnDNAInit);

            SubscribeLocalEvent<Components.ForensicsComponent, BeingGibbedEvent>(OnBeingGibbed);
            SubscribeLocalEvent<Components.ForensicsComponent, MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<Components.ForensicsComponent, GotRehydratedEvent>(OnRehydrated);
            SubscribeLocalEvent<Components.CleansForensicsComponent, AfterInteractEvent>(OnAfterInteract, after: new[] { typeof(AbsorbentSystem) });
            SubscribeLocalEvent<Components.ForensicsComponent, CleanForensicsDoAfterEvent>(OnCleanForensicsDoAfter);
            SubscribeLocalEvent<Components.DnaComponent, TransferDnaEvent>(OnTransferDnaEvent);
            SubscribeLocalEvent<Components.DnaSubstanceTraceComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
            SubscribeLocalEvent<Components.CleansForensicsComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        }

        private void OnSolutionChanged(Entity<Components.DnaSubstanceTraceComponent> ent, ref SolutionContainerChangedEvent ev)
        {
            var soln = GetSolutionsDNA(ev.Solution);
            if (soln.Count > 0)
            {
                var comp = EnsureComp<Components.ForensicsComponent>(ent.Owner);
                foreach (string dna in soln)
                {
                    comp.DNAs.Add(dna);
                }
            }
        }

        private void OnInteract(EntityUid uid, Components.FingerprintComponent component, ContactInteractionEvent args)
        {
            ApplyEvidence(uid, args.Other);
        }

        private void OnFingerprintInit(EntityUid uid, Components.FingerprintComponent component, MapInitEvent args)
        {
            component.Fingerprint = GenerateFingerprint();
        }

        private void OnDNAInit(EntityUid uid, Components.DnaComponent component, MapInitEvent args)
        {
            if (component.DNA == String.Empty)
            {
                component.DNA = GenerateDNA();

                var ev = new GenerateDnaEvent { Owner = uid, DNA = component.DNA };
                RaiseLocalEvent(uid, ref ev);
            }
        }

        private void OnBeingGibbed(EntityUid uid, Components.ForensicsComponent component, BeingGibbedEvent args)
        {
            string dna = Loc.GetString("forensics-dna-unknown");

            if (TryComp(uid, out Components.DnaComponent? dnaComp))
                dna = dnaComp.DNA;

            foreach (EntityUid part in args.GibbedParts)
            {
                var partComp = EnsureComp<Components.ForensicsComponent>(part);
                partComp.DNAs.Add(dna);
                partComp.CanDnaBeCleaned = false;
            }
        }

        private void OnMeleeHit(EntityUid uid, Components.ForensicsComponent component, MeleeHitEvent args)
        {
            if ((args.BaseDamage.DamageDict.TryGetValue("Blunt", out var bluntDamage) && bluntDamage.Value > 0) ||
                (args.BaseDamage.DamageDict.TryGetValue("Slash", out var slashDamage) && slashDamage.Value > 0) ||
                (args.BaseDamage.DamageDict.TryGetValue("Piercing", out var pierceDamage) && pierceDamage.Value > 0))
            {
                foreach (EntityUid hitEntity in args.HitEntities)
                {
                    if (TryComp<Components.DnaComponent>(hitEntity, out var hitEntityComp))
                        component.DNAs.Add(hitEntityComp.DNA);
                }
            }
        }

        private void OnRehydrated(Entity<Components.ForensicsComponent> ent, ref GotRehydratedEvent args)
        {
            CopyForensicsFrom(ent.Comp, args.Target);
        }

        /// <summary>
        /// Copy forensic information from a source entity to a destination.
        /// Existing forensic information on the target is still kept.
        /// </summary>
        public void CopyForensicsFrom(Components.ForensicsComponent src, EntityUid target)
        {
            var dest = EnsureComp<Components.ForensicsComponent>(target);
            foreach (var dna in src.DNAs)
            {
                dest.DNAs.Add(dna);
            }

            foreach (var fiber in src.Fibers)
            {
                dest.Fibers.Add(fiber);
            }

            foreach (var print in src.Fingerprints)
            {
                dest.Fingerprints.Add(print);
            }
        }

        public List<string> GetSolutionsDNA(EntityUid uid)
        {
            List<string> list = new();
            if (TryComp<SolutionContainerManagerComponent>(uid, out var comp))
            {
                foreach (var (_, soln) in _solutionContainerSystem.EnumerateSolutions((uid, comp)))
                {
                    list.AddRange(GetSolutionsDNA(soln.Comp.Solution));
                }
            }
            return list;
        }

        public List<string> GetSolutionsDNA(Solution soln)
        {
            List<string> list = new();
            foreach (var reagent in soln.Contents)
            {
                foreach (var data in reagent.Reagent.EnsureReagentData())
                {
                    if (data is DnaData)
                    {
                        list.Add(((DnaData) data).DNA);
                    }
                }
            }
            return list;
        }
        private void OnAfterInteract(Entity<Components.CleansForensicsComponent> cleanForensicsEntity, ref AfterInteractEvent args)
        {
            if (args.Handled || !args.CanReach || args.Target == null)
                return;

            args.Handled = TryStartCleaning(cleanForensicsEntity, args.User, args.Target.Value);
        }

        private void OnUtilityVerb(Entity<Components.CleansForensicsComponent> entity, ref GetVerbsEvent<UtilityVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            // These need to be set outside for the anonymous method!
            var user = args.User;
            var target = args.Target;

            var verb = new UtilityVerb()
            {
                Act = () => TryStartCleaning(entity, user, target),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
                Text = Loc.GetString(Loc.GetString("forensics-verb-text")),
                Message = Loc.GetString(Loc.GetString("forensics-verb-message")),
                // This is important because if its true using the cleaning device will count as touching the object.
                DoContactInteraction = false
            };

            args.Verbs.Add(verb);
        }

        /// <summary>
        ///     Attempts to clean the given item with the given CleansForensics entity.
        /// </summary>
        /// <param name="cleanForensicsEntity">The entity that is being used to clean the target.</param>
        /// <param name="user">The user that is using the cleanForensicsEntity.</param>
        /// <param name="target">The target of the forensics clean.</param>
        /// <returns>True if the target can be cleaned and has some sort of DNA or fingerprints / fibers and false otherwise.</returns>
        public bool TryStartCleaning(Entity<Components.CleansForensicsComponent> cleanForensicsEntity, EntityUid user, EntityUid target)
        {
            if (!TryComp<Components.ForensicsComponent>(target, out var forensicsComp))
            {
                _popupSystem.PopupEntity(Loc.GetString("forensics-cleaning-cannot-clean", ("target", target)), user, user, PopupType.MediumCaution);
                return false;
            }

            var totalPrintsAndFibers = forensicsComp.Fingerprints.Count + forensicsComp.Fibers.Count;
            var hasRemovableDNA = forensicsComp.DNAs.Count > 0 && forensicsComp.CanDnaBeCleaned;

            if (hasRemovableDNA || totalPrintsAndFibers > 0)
            {
                var cleanDelay = cleanForensicsEntity.Comp.CleanDelay;
                var doAfterArgs = new DoAfterArgs(EntityManager, user, cleanDelay, new CleanForensicsDoAfterEvent(), cleanForensicsEntity, target: target, used: cleanForensicsEntity)
                {
                    NeedHand = true,
                    BreakOnDamage = true,
                    BreakOnMove = true,
                    MovementThreshold = 0.01f,
                    DistanceThreshold = forensicsComp.CleanDistance,
                };

                _doAfterSystem.TryStartDoAfter(doAfterArgs);

                _popupSystem.PopupEntity(Loc.GetString("forensics-cleaning", ("target", target)), user, user);

                return true;
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("forensics-cleaning-cannot-clean", ("target", target)), user, user, PopupType.MediumCaution);
                return false;
            }

        }

        private void OnCleanForensicsDoAfter(EntityUid uid, Components.ForensicsComponent component, CleanForensicsDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Args.Target == null)
                return;

            if (!TryComp<Components.ForensicsComponent>(args.Target, out var targetComp))
                return;

            targetComp.Fibers = new();
            targetComp.Fingerprints = new();

            if (targetComp.CanDnaBeCleaned)
                targetComp.DNAs = new();

            // leave behind evidence it was cleaned
            if (TryComp<Components.FiberComponent>(args.Used, out var fiber))
                targetComp.Fibers.Add(string.IsNullOrEmpty(fiber.FiberColor) ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial)) : Loc.GetString("forensic-fibers-colored", ("color", fiber.FiberColor), ("material", fiber.FiberMaterial)));

            if (TryComp<Components.ResidueComponent>(args.Used, out var residue))
                targetComp.Residues.Add(string.IsNullOrEmpty(residue.ResidueColor) ? Loc.GetString("forensic-residue", ("adjective", residue.ResidueAdjective)) : Loc.GetString("forensic-residue-colored", ("color", residue.ResidueColor), ("adjective", residue.ResidueAdjective)));
        }

        public string GenerateFingerprint()
        {
            var fingerprint = new byte[16];
            _random.NextBytes(fingerprint);
            return Convert.ToHexString(fingerprint);
        }

        public string GenerateDNA()
        {
            var letters = new[] { "A", "C", "G", "T" };
            var DNA = string.Empty;

            for (var i = 0; i < 16; i++)
            {
                DNA += letters[_random.Next(letters.Length)];
            }

            return DNA;
        }

        private void ApplyEvidence(EntityUid user, EntityUid target)
        {
            if (HasComp<IgnoresFingerprintsComponent>(target))
                return;

            var component = EnsureComp<Components.ForensicsComponent>(target);
            if (_inventory.TryGetSlotEntity(user, "gloves", out var gloves))
            {
                if (TryComp<Components.FiberComponent>(gloves, out var fiber) && !string.IsNullOrEmpty(fiber.FiberMaterial))
                    component.Fibers.Add(string.IsNullOrEmpty(fiber.FiberColor) ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial)) : Loc.GetString("forensic-fibers-colored", ("color", fiber.FiberColor), ("material", fiber.FiberMaterial)));

                if (HasComp<Components.FingerprintMaskComponent>(gloves))
                    return;
            }
            if (TryComp<Components.FingerprintComponent>(user, out var fingerprint))
                component.Fingerprints.Add(fingerprint.Fingerprint ?? "");
        }

        private void OnTransferDnaEvent(EntityUid uid, Components.DnaComponent component, ref TransferDnaEvent args)
        {
            var recipientComp = EnsureComp<Components.ForensicsComponent>(args.Recipient);
            recipientComp.DNAs.Add(component.DNA);
            recipientComp.CanDnaBeCleaned = args.CanDnaBeCleaned;
        }

        #region Public API

        /// <summary>
        /// Transfer DNA from one entity onto the forensics of another
        /// </summary>
        /// <param name="recipient">The entity receiving the DNA</param>
        /// <param name="donor">The entity applying its DNA</param>
        /// <param name="canDnaBeCleaned">If this DNA be cleaned off of the recipient. e.g. cleaning a knife vs cleaning a puddle of blood</param>
        public void TransferDna(EntityUid recipient, EntityUid donor, bool canDnaBeCleaned = true)
        {
            if (TryComp<Components.DnaComponent>(donor, out var donorComp))
            {
                EnsureComp<Components.ForensicsComponent>(recipient, out var recipientComp);
                recipientComp.DNAs.Add(donorComp.DNA);
                recipientComp.CanDnaBeCleaned = canDnaBeCleaned;
            }
        }

        #endregion
    }
}
