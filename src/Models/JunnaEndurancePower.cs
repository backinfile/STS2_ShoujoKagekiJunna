using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_ENDURANCE_POWER")]
public sealed class JunnaEndurancePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => JunnaMod.ResourceRoot + "images/energy.png";
    public override string CustomBigIconPath => CustomPackedIconPath;
}
