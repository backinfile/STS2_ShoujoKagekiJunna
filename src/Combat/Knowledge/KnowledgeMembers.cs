using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Directional membership shared by effects, acquisition and presentation.</summary>
internal static class KnowledgeMembers
{
    internal static IEnumerable<(CardModel Model, int Count)>? Definition(CardModel source)
        => DynamicKnowledge.Definition(source) ?? KnowledgeCombat.Group(source)?.Members
            .Select(member => (Model: CardCatalog.Model(member.Id), member.Count));

    internal static HashSet<ModelId> PartnerIds(CardModel source)
        => Definition(source)?.Where(member => member.Model.Id != source.Id || member.Count > 1)
            .Select(member => member.Model.Id).ToHashSet() ?? [];
}
