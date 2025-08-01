using Content.Server.Atmos.Rotting;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Medical.Surgery.Conditions;
using Content.Shared.Medical.Surgery.Effects.Step;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared._CorvaxNext.Surgery;
using Content.Shared._CorvaxNext.Surgery.Effects.Step;
using Content.Shared._CorvaxNext.Surgery.Tools;
using Content.Shared.Bed.Sleep;
using Content.Shared.Medical.Surgery;
using Content.Shared.Verbs;

namespace Content.Server._CorvaxNext.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly RottingSystem _rot = default!;
    [Dependency] private readonly BlindableSystem _blindableSystem = default!;

    private readonly List<EntProtoId> _surgeries = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryToolComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryStepDamageEvent>(OnSurgeryStepDamage);
        // You might be wondering "why aren't we using StepEvent for these two?" reason being that StepEvent fires off regardless of success on the previous functions
        // so this would heal entities even if you had a used or incorrect organ.
        SubscribeLocalEvent<SurgerySpecialDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnSurgerySpecialDamageChange);
        SubscribeLocalEvent<SurgeryDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnSurgeryDamageChange);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepScreamComplete);
        SubscribeLocalEvent<SurgeryStepSpawnEffectComponent, SurgeryStepEvent>(OnStepSpawnComplete);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadPrototypes();
    }

    protected override void RefreshUI(EntityUid body)
    {
        var surgeries = new Dictionary<NetEntity, List<EntProtoId>>();
        foreach (var surgery in _surgeries)
        {
            if (GetSingleton(surgery) is not { } surgeryEnt)
                continue;

            foreach (var part in _body.GetBodyChildren(body))
            {
                var ev = new SurgeryValidEvent(body, part.Id);
                RaiseLocalEvent(surgeryEnt, ref ev);

                if (ev.Cancelled)
                    continue;

                surgeries.GetOrNew(GetNetEntity(part.Id)).Add(surgery);
            }

        }
        //Logger.Debug($"Setting UI state with {surgeries}, {body} and {SurgeryUIKey.Key}");
        _ui.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(surgeries));
        /*
            Reason we do this is because when applying a BUI State, it rolls back the state on the entity temporarily,
            which just so happens to occur right as we're checking for step completion, so we end up with the UI
            not updating at all until you change tools or reopen the window. I love shitcode.
        */
        _ui.ServerSendUiMessage(body, SurgeryUIKey.Key, new SurgeryBuiRefreshMessage());
    }
    private void SetDamage(EntityUid body,
        DamageSpecifier damage,
        float partMultiplier,
        EntityUid user,
        EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return;

        _damageable.TryChangeDamage(body,
            damage,
            true,
            origin: user,
            canSever: false,
            partMultiplier: partMultiplier,
            targetPart: _body.GetTargetBodyPart(partComp));
    }

    private void AttemptStartSurgery(Entity<SurgeryToolComponent> ent, EntityUid user, EntityUid target)
    {
        if (!IsLyingDown(target, user))
            return;

        if (user == target && !_config.GetCVar(Shared._CorvaxNext.NextVars.NextVars.CanOperateOnSelf))

        {
            _popup.PopupEntity(Loc.GetString("surgery-error-self-surgery"), user, user);
            return;
        }

        _ui.OpenUi(target, SurgeryUIKey.Key, user);
        RefreshUI(target);
    }

    private void OnUtilityVerb(Entity<SurgeryToolComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract
            || !args.CanAccess
            || !HasComp<SurgeryTargetComponent>(args.Target))
            return;

        var user = args.User;
        var target = args.Target;

        var verb = new UtilityVerb()
        {
            Act = () => AttemptStartSurgery(ent, user, target),
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Objects/Specific/Medical/Surgery/scalpel.rsi/"), "scalpel"),
            Text = Loc.GetString("surgery-verb-text"),
            Message = Loc.GetString("surgery-verb-message"),
            DoContactInteraction = true
        };

        args.Verbs.Add(verb);
    }

    private void OnSurgeryStepDamage(Entity<SurgeryTargetComponent> ent, ref SurgeryStepDamageEvent args) =>
        SetDamage(args.Body, args.Damage, args.PartMultiplier, args.User, args.Part);

    private void OnSurgeryDamageChange(Entity<SurgeryDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        var damageChange = ent.Comp.Damage;
        if (HasComp<ForcedSleepingStatusEffectComponent>(args.Body))
            damageChange = damageChange * ent.Comp.SleepModifier;

        SetDamage(args.Body, damageChange, 0.5f, args.User, args.Part);
    }

    private void OnSurgerySpecialDamageChange(Entity<SurgerySpecialDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        if (ent.Comp.DamageType == "Rot")
            _rot.ReduceAccumulator(args.Body, TimeSpan.FromSeconds(2147483648)); // BEHOLD, SHITCODE THAT I JUST COPY PASTED. I'll redo it at some point, pinky swear :)
        else if (ent.Comp.DamageType == "Eye"
            && TryComp(ent, out BlindableComponent? blindComp)
            && blindComp.EyeDamage > 0)
            _blindableSystem.AdjustEyeDamage((args.Body, blindComp), -blindComp!.EyeDamage);
    }

    private void OnStepScreamComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (HasComp<ForcedSleepingStatusEffectComponent>(args.Body))
            return;

        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }
    private void OnStepSpawnComplete(Entity<SurgeryStepSpawnEffectComponent> ent, ref SurgeryStepEvent args) =>
        SpawnAtPosition(ent.Comp.Entity, Transform(args.Body).Coordinates);

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        _surgeries.Clear();
        foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
            if (entity.HasComponent<SurgeryComponent>())
                _surgeries.Add(new EntProtoId(entity.ID));
    }
}
