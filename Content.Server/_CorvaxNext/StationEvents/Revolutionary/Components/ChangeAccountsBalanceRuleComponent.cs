using Content.Server._CorvaxNext.StationEvents.Revolutionary.Events;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxNext.StationEvents.Revolutionary.Components;

[RegisterComponent, Access(typeof(ChangeAccountsBalanceRule))]
public sealed partial class ChangeAccountsBalanceRuleComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<CargoAccountPrototype>, double> AccountsBalanceChanges = new();
}
