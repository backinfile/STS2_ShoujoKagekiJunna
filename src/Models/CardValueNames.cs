namespace ShoujoKagekiJunna.Models;

/// <summary>Semantic field names preserve the identities already used in saves.</summary>
internal static class CardValueNames
{
    internal static string Resolve(string id, string name) => CardRules.Get(id).Field(name);
    internal static void Validate(CardDefinition card)
    {
        foreach (var field in CardRules.Get(card.Id).Fields)
            if (!card.Values.Any(value => value.Name == field.Value))
                throw new InvalidDataException($"Card {card.Id} has no field {field.Value} for {field.Key}.");
    }
}
