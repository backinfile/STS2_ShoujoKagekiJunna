using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShoujoKagekiJunna.Combat;

namespace ShoujoKagekiJunna.Models;

public sealed record CardNumber(string Name, int Base, int Upgraded);
public sealed record ArrowArtFocus(float X = .5f, float Y = .5f);
public sealed record StartingCard(string CardId, int Count);

public sealed class CardDefinition
{
    public string Id { get; init; } = "";
    public string ModelId { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string Name { get; init; } = "";
    public int Cost { get; init; }
    public int UpgradedCost { get; init; }
    public bool CostsX { get; init; }
    public string Type { get; init; } = "";
    public string Target { get; init; } = "";
    public string ArrowSubtype { get; init; } = "";
    public string Rarity { get; init; } = "";
    public bool Pool { get; init; }
    public bool Knowledge { get; init; }
    public bool Exhaust { get; init; }
    public bool UpgradedExhaust { get; init; }
    public bool Retain { get; init; }
    public bool UpgradedRetain { get; init; }
    public bool DamageCapable { get; init; }
    public string Portrait { get; init; } = "";
    public ArrowArtFocus? ArrowFocus { get; init; }
    public string Text { get; init; } = "";
    public string UpgradedText { get; init; } = "";
    public CardNumber[] Values { get; init; } = [];
    public CardType CardType => ToType(Type);
    public CardType InternalType => ToType(ArrowSubtype);
    public TargetType TargetType => Enum.Parse<TargetType>(Target);
    public CardRarity CardRarity => Rarity switch
    {
        "基础" => CardRarity.Basic, "普通" => CardRarity.Common, "罕见" => CardRarity.Uncommon,
        "稀有" => CardRarity.Rare, "远古" => CardRarity.Ancient,
        _ => throw new InvalidOperationException($"Unknown rarity: {Id}/{Rarity}")
    };
    private static CardType ToType(string value) => value switch
    {
        "箭矢" => ArrowPile.ArrowType, "攻击" => CardType.Attack, "能力" => CardType.Power, _ => CardType.Skill
    };
}

public static class CardCatalog
{
    private sealed record Catalog(int Revision, CardDefinition[] Cards, KnowledgeGroup[] Groups, StartingCard[] StartingDeck, CardDefinition[] RetiredCards, int StartingHp = 70, int StartingGold = 99, string[]? StartingRelics = null);
    private static readonly Catalog Data = Load();
    private static readonly Dictionary<string, CardDefinition> ById = Data.Cards.Concat(Data.RetiredCards).ToDictionary(c => c.Id);
    public static int Revision => Data.Revision;
    public static int StartingHp => Data.StartingHp;
    public static int StartingGold => Data.StartingGold;
    public static IReadOnlyList<RelicModel> StartingRelics => (Data.StartingRelics ?? ["JUNNA_GREEN_GEM"])
        .Select(id => ModelDb.GetById<RelicModel>(new ModelId("RELIC", id))).ToArray();
    public static IReadOnlyList<CardDefinition> Cards => Data.Cards;
    public static IReadOnlyList<KnowledgeGroup> Groups => Data.Groups;
    public static IEnumerable<CardModel> StartingDeck => Data.StartingDeck.SelectMany(c => Enumerable.Repeat(Model(c.CardId), c.Count));
    public static CardDefinition Get(string id) => ById[id];
    public static string? DesignId(CardModel card) => (card as JunnaCard)?.DesignId;
    public static bool IsKnowledge(CardModel card) => (card is JunnaCard junna && Get(junna.DesignId).Knowledge) || DynamicKnowledge.IsLinked(card);
    public static bool IsArrow(CardModel card) => HandArrow.IsArrow(card) || card.Type == ArrowPile.ArrowType;
    public static CardModel Model(string id)
    {
        var type = typeof(CardCatalog).Assembly.GetType("ShoujoKagekiJunna.Models." + Get(id).ClassName, true)!;
        return ModelDb.GetById<CardModel>(ModelDb.GetId(type));
    }
    public static CardModel Create(string id, Player owner) => owner.Creature.CombatState!.CreateCard(Model(id), owner);
    public static void RegisterDamageCapabilities()
    {
        foreach (var definition in Cards.Concat(Data.RetiredCards))
        {
            var type = typeof(CardCatalog).Assembly.GetType("ShoujoKagekiJunna.Models." + definition.ClassName, true)!;
            DamageCapability.Register(type, card => card is CatalogCard catalog ? catalog.Definition.DamageCapable : definition.DamageCapable);
        }
    }
    private static Catalog Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Junna.CardPool")!;
        return JsonSerializer.Deserialize<Catalog>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
