using Content.Shared.Charges.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Charges.Components;

/// <summary>
/// Specifies the attached action has discrete charges, separate to a cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedChargesSystem))]
public sealed partial class LimitedChargesComponent : Component
{
    [DataField, AutoNetworkedField]
    public int LastCharges;

    /// <summary>
    ///     The max charges this action has.
    /// </summary>
<<<<<<< HEAD
    [DataField("maxCharges"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public FixedPoint2 MaxCharges = 3;
=======
    [DataField, AutoNetworkedField, Access(Other = AccessPermissions.Read)]
    public int MaxCharges = 3;
>>>>>>> upstream-next/master

    /// <summary>
    /// Last time charges was changed. Used to derive current charges.
    /// </summary>
<<<<<<< HEAD
    [DataField("charges"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public FixedPoint2 Charges = 3;
=======
    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastUpdate;
>>>>>>> upstream-next/master
}
