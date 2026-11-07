using BaseLib.Patches.Content;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShoujoKagekiJunna.Models;

namespace ShoujoKagekiJunna.Combat;

public static class JunnaRewardTypes
{
    [CustomEnum] public static RewardType KnowledgeCopy;
}

public sealed class KnowledgeCopyReward(Player player, int maximum) : Reward(player)
{
    private bool _resolved;
    public int Maximum { get; } = Math.Max(1, maximum);
    protected override RewardType RewardType => JunnaRewardTypes.KnowledgeCopy;
    public override int RewardsSetIndex => 4;
    public override bool IsPopulated => true;
    protected override string IconPath => ImageHelper.GetImagePath("ui/reward_screen/reward_icon_card.png");
    public override LocString Description
    {
        get
        {
            var text = new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_COPY_REWARD");
            text.Add("Count", Maximum);
            return text;
        }
    }
    public override void Populate() { }
    public override void MarkContentAsSeen() { }
    protected override async Task<bool> OnSelect()
    {
        if (_resolved) return true;
        var candidates = Player.Deck.Cards.Where(CardCatalog.IsKnowledge)
            .Where(c => ShoujoKageki.Core.V1.Shine.GetState(c) is not { Remaining: <= 0 }).ToArray();
        if (candidates.Length == 0) return false;
        var prompt = new LocString("gameplay_ui", "JUNNA.KNOWLEDGE_COPY_SELECT");
        prompt.Add("Count", Maximum);
        var selected = await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), candidates, Player,
            new CardSelectorPrefs(prompt, 0, Math.Min(Maximum, candidates.Length)));
        if (!selected.Any()) return false;
        await KnowledgeProgress.CopySingleCards(Player, selected);
        _resolved = true;
        return true;
    }
    public override SerializableReward ToSerializable() => new() { RewardType = RewardType, OptionCount = Maximum };

    [HarmonyPatch(typeof(Reward), nameof(Reward.FromSerializable))]
    private static class Restore
    {
        private static bool Prefix(SerializableReward save, Player player, ref Reward __result)
        {
            if (save.RewardType != JunnaRewardTypes.KnowledgeCopy) return true;
            __result = new KnowledgeCopyReward(player, save.OptionCount);
            return false;
        }
    }
}
