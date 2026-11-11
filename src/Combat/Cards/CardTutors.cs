using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static class CardTutors
{
    public static bool Matches(CardModel card, int category) => category switch
    {
        0 => CardCatalog.IsArrow(card),
        1 => card.Keywords.Contains(JunnaKeywords.Knowledge),
        2 => Shine.IsShineCard(card),
        _ => false
    };

    public static async Task DrawCategories(PlayerChoiceContext context, CardModel source)
    {
        var player = source.Owner;
        var completed = new HashSet<int>();
        var drawn = new HashSet<CardModel>();
        while (completed.Count < 3 && player.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand
            && !CombatManager.Instance.IsOverOrEnding)
        {
            var candidates = player.PlayerCombatState.DrawPile.Cards.Where(card => !drawn.Contains(card)).ToList();
            var randomized = new List<CardModel>();
            var rng = player.RunState.Rng.CombatCardSelection;
            while (candidates.Count > 0)
            {
                var next = rng.NextItem(candidates)!;
                randomized.Add(next);
                candidates.Remove(next);
            }
            var assigned = new Dictionary<int, CardModel>();
            bool Assign(int category, HashSet<CardModel> visited)
            {
                foreach (var candidate in randomized.Where(card => Matches(card, category)))
                {
                    if (!visited.Add(candidate)) continue;
                    var previous = assigned.FirstOrDefault(pair => pair.Value == candidate);
                    if (previous.Value != null && !Assign(previous.Key, visited)) continue;
                    assigned[category] = candidate;
                    return true;
                }
                return false;
            }
            foreach (int category in Enumerable.Range(0, 3).Where(category => !completed.Contains(category)))
                Assign(category, []);
            if (assigned.Count == 0 || !Hook.ShouldDraw(player.Creature.CombatState!, player, false, out _)) break;
            var selected = assigned.OrderBy(pair => pair.Key).First();
            await CardPileCmd.Add(selected.Value, PileType.Draw, CardPilePosition.Top, skipVisuals: true);
            var result = await CardPileCmd.Draw(context, player);
            if (result == null) break;
            drawn.Add(result);
            completed.Add(selected.Key);
        }
    }
}
