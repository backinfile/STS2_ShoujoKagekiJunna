using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

/// <summary>Directional membership shared by effects, acquisition and presentation.</summary>
internal static class KnowledgeMembers
{
    internal static IEnumerable<(CardModel Model, int Count)>? Definition(CardModel source, bool forCombatGeneration = false)
    {
        var members = DynamicKnowledge.Definition(source, forCombatGeneration) ?? KnowledgeCombat.Group(source)?.Members
            .Select(member => (Model: CardCatalog.Model(member.Id), member.Count));
        return forCombatGeneration ? members?.Where(member => CardCatalog.CanGenerateAsKnowledgeMember(member.Model)) : members;
    }

    internal static HashSet<ModelId> PartnerIds(CardModel source)
        => Definition(source)?.Where(member => member.Model.Id != source.Id || member.Count > 1)
            .Select(member => member.Model.Id).ToHashSet() ?? [];
}
