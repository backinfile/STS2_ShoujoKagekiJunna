using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace ShoujoKagekiJunna.Models;

public static class JunnaKeywords
{
    // Card text already places this keyword, including below the arrow countdown.
    [CustomEnum]
    public static CardKeyword Knowledge;

    [CustomEnum]
    public static CardKeyword Arrow;

    [CustomEnum("DAMAGE_BONUS")]
    public static CardKeyword DamageBonus;
}
