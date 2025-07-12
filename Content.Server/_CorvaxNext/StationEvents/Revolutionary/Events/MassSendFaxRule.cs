using Content.Server._CorvaxNext.StationEvents.Revolutionary.Components;
using Content.Server.Fax;
using Content.Server.StationEvents.Events;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;

namespace Content.Server._CorvaxNext.StationEvents.Revolutionary.Events;

public sealed class MassSendFaxRule : StationEventSystem<MassSendFaxRuleComponent>
{
    [Dependency] private readonly FaxSystem _fax = default!;
    protected override void Started(EntityUid uid, MassSendFaxRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        var printout = new FaxPrintout(
                Loc.GetString(component.DocumentText),
                Loc.GetString(component.DocumentName),
                null,
                null,
                component.StampState,
                [new() { StampedName = component.StampText, StampedColor = component.StampedColor }]
            );

        var query = EntityQueryEnumerator<FaxMachineComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var faxComp, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
                continue;

            if (component.SendOnlyToReceiveNukeCodes && !faxComp.ReceiveNukeCodes)
                continue;

            _fax.Receive(ent, printout);
        }
    }
}

