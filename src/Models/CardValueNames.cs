namespace ShoujoKagekiJunna.Models;

/// <summary>Semantic names resolve through generated metadata; saved field identities remain stable.</summary>
internal static class CardValueNames
{
    internal static bool TryResolve(CardDefinition card, string name, out string field)
    {
        var value = card.Values.FirstOrDefault(value => value.Name == name || value.Aliases.Contains(name));
        field = value?.Name ?? name;
        return value != null;
    }
    internal static string Resolve(string id, string name) => TryResolve(CardCatalog.Get(id), name, out var field)
        ? field : throw new InvalidDataException($"Card {id} has no required field {name}.");
    internal static void Validate(CardDefinition card)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in card.Values)
            foreach (var name in value.Aliases.Prepend(value.Name))
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                    throw new InvalidDataException($"Card {card.Id} has an ambiguous value name {name}.");
    }
}
