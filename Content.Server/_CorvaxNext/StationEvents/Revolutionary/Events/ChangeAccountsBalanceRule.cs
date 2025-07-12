using Content.Server._CorvaxNext.StationEvents.Revolutionary.Components;
using Content.Server.Antag.Components;
using Content.Server.Cargo.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server._CorvaxNext.StationEvents.Revolutionary.Events;

public sealed class ChangeAccountsBalanceRule : StationEventSystem<ChangeAccountsBalanceRuleComponent>
{
    [Dependency] private readonly CargoSystem _cargoSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    protected override void Added(EntityUid uid, ChangeAccountsBalanceRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        if (!TryComp<StationBankAccountComponent>(chosenStation, out var bank))
            return;

        var changesDisplay = "";

        foreach (var (account, percent) in component.AccountsBalanceChanges)
        {
            if (!_prototypeManager.TryIndex<CargoAccountPrototype>(account.Id, out var accountProto))
                return;

            var currentBalance = (double)_cargoSystem.GetBalanceFromAccount((chosenStation.Value, bank), account);
            var balanceToAdd = (int)(currentBalance * percent * -1);

            _cargoSystem.UpdateBankAccount((chosenStation.Value, bank), balanceToAdd, account);


            changesDisplay += "\n" + Loc.GetString("change-account-balance-rule-display-formatting", ("departmentName", Loc.GetString(accountProto.Name))
                , ("added", balanceToAdd < 0 ? balanceToAdd : "+" + balanceToAdd));
        }

        if (stationEvent.StartAnnouncement is not null)
            stationEvent.StartAnnouncement = Loc.GetString(stationEvent.StartAnnouncement, ("display", changesDisplay));
        else
            stationEvent.StartAnnouncement = Loc.GetString("base-change-account-balance-rule", ("display", changesDisplay));

        base.Added(uid, component, gameRule, args);
    }
}
