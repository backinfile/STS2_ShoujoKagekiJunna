using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

[CustomID("JUNNA_STAGE_GIRL_NOTES")]
public sealed class JunnaStageGirlNotes : CustomEventModel
{
    private static readonly SavedSpireField<Player, string> Outcome = new(() => null, "JunnaStageGirlNotesOutcome");
    private Task? _choice;

    public override string CustomInitialPortraitPath => JunnaMod.ResourceRoot + "images/events/stage_girl_notes.png";
    private static bool CanUpgradeKnowledge(CardModel card) => card.IsUpgradable && CardCatalog.IsKnowledge(card);

    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 1
        && runState.Players.All(player => player.Character is Junna)
        && runState is RunState state && !state.VisitedEventIds.Contains(Id);

    protected override void SetInitialEventState(bool isPreFinished)
    {
        string? outcome = Outcome.Get(Owner!);
        if (outcome != null)
        {
            SetEventFinished(PageDescription(outcome));
            return;
        }
        base.SetInitialEventState(isPreFinished);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, Study, $"{Id.Entry}.pages.INITIAL.options.STUDY",
                HoverTipFactory.FromCard(CardCatalog.Model("JK359"))),
            Owner!.Deck.Cards.Any(CanUpgradeKnowledge)
                ? new EventOption(this, Practice, $"{Id.Entry}.pages.INITIAL.options.PRACTICE")
                : new EventOption(this, null, $"{Id.Entry}.pages.INITIAL.options.PRACTICE_LOCKED"),
            new EventOption(this, Leave, $"{Id.Entry}.pages.INITIAL.options.LEAVE")
        ];
    }

    private Task Study() => _choice ??= Complete("STUDY", ObtainKnowledge);
    private Task Practice() => _choice ??= Complete("PRACTICE", UpgradeKnowledge);
    private Task Leave() => _choice ??= Complete("LEAVE", () => Task.CompletedTask);

    private async Task ObtainKnowledge()
    {
        // Expand the normal direct association list once, including when acquisition sources are restricted.
        using var single = KnowledgeCombat.SingleCard();
        var entrance = Owner!.RunState.CreateCard(CardCatalog.Model("JK359"), Owner);
        var result = await CardPileCmd.Add(entrance, PileType.Deck, CardPilePosition.Bottom);
        if (result.success)
        {
            await KnowledgeCombat.GrantPartners(result.cardAdded, PileType.Deck);
            Owner.Deck.InvokeCardAddFinished();
            CardCmd.PreviewCardPileAdd(result, style: CardPreviewStyle.EventLayout);
        }
    }

    private async Task UpgradeKnowledge()
    {
        var cards = await CardSelectCmd.FromDeckGeneric(Owner!,
            new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1), CanUpgradeKnowledge);
        CardCmd.Upgrade(cards, CardPreviewStyle.EventLayout);
    }

    private async Task Complete(string page, Func<Task> reward)
    {
        if (Outcome.Get(Owner!) != null) return;
        await reward();
        Outcome.Set(Owner!, page);
        Owner!.RunState.Act.RemoveEventFromSet(CanonicalInstance);
        SetEventFinished(PageDescription(page));
        // Native completed-room saves restore this event instead of rolling another unknown room.
        if (Owner.RunState.CurrentRoom is EventRoom room && room.CanonicalEvent.Id == Id
            && RunManager.Instance.EventSynchronizer.Events.All(model => model.IsFinished))
        {
            room.MarkPreFinished();
            await SaveManager.Instance.SaveRun(room);
        }
    }
}
