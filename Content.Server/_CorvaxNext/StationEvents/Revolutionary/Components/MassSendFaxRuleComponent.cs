using Content.Server._CorvaxNext.StationEvents.Revolutionary.Events;

namespace Content.Server._CorvaxNext.StationEvents.Revolutionary.Components;

[RegisterComponent, Access(typeof(MassSendFaxRule))]
public sealed partial class MassSendFaxRuleComponent : Component
{
    [DataField("docText", required: true)]
    public string DocumentText;

    [DataField("docName")]
    public string DocumentName = "mass-send-fax-doc-name";

    [DataField("stampText")]
    public string StampText = "stamp-component-stamped-name-centcom";

    [DataField("stampColor")]
    public Color StampedColor = Color.FromHex("#006600");

    [DataField("stampState")]
    public string StampState = "paper_stamp-centcom";

    /// <summary>
    ///     Send to captain's fax only
    /// </summary>
    [DataField("nukeCodesFaxesOnly")]
    public bool SendOnlyToReceiveNukeCodes = false;
}
