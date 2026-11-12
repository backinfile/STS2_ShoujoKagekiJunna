using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;

namespace ShoujoKagekiJunna.Combat;

internal static class CardStateCopies
{
    internal static void Initialize() => CoreApi.Get<ICardCloneEvents>().Cloned += Copy;
    private static void Copy(CardModel source, CardModel copy)
    {
        DamageModifiers.Copy(source, copy);
        KnowledgeProgress.Copy(source, copy);
        HandArrow.Copy(source, copy);
        ArrowConversion.Copy(source, copy);
        CostChanges.Copy(source, copy);
    }
}
