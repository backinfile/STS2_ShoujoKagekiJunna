using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_DISTANT_STARS_STRENGTH_POWER")]
public sealed class JunnaDistantStarsStrengthPower : TemporaryStrengthPower, ICustomPower
{
    public override AbstractModel OriginModel => ModelDb.Card<JunnaCardJK37C>();
    public string CustomPackedIconPath => JunnaMod.ResourceRoot + "images/powers/JK37C.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomBigIconPath;
}

[CustomID("JUNNA_DISTANT_STARS_DEXTERITY_POWER")]
public sealed class JunnaDistantStarsDexterityPower : TemporaryDexterityPower, ICustomPower
{
    public override AbstractModel OriginModel => ModelDb.Card<JunnaCardJK37C>();
    public string CustomPackedIconPath => JunnaMod.ResourceRoot + "images/powers/JK37C.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomBigIconPath;
}
