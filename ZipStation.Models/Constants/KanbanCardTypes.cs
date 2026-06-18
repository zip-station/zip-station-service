namespace ZipStation.Models.Constants;

/// The built-in story types. These are always available on every board and can never be
/// deleted. Projects may define additional, custom types on their board
/// (<c>KanbanBoard.CustomCardTypes</c>); a card's <c>Type</c> holds either one of these
/// built-in names or a custom type's id.
///
/// Story type used to be the closed enum <c>KanbanCardType</c> serialized to BSON as an int
/// (0=Feature, 1=Bug, 2=Improvement, 3=TechDebt). It's now a string so custom types can exist;
/// the legacy ints are mapped back to these names on read (see
/// <c>LegacyCardTypeStringSerializer</c>) so existing cards keep working.
public static class KanbanCardTypes
{
    public const string Feature = "Feature";
    public const string Bug = "Bug";
    public const string Improvement = "Improvement";
    public const string TechDebt = "TechDebt";

    /// Built-in names in their historical enum order — index == legacy BSON int value.
    public static readonly IReadOnlyList<string> BuiltIns = new[] { Feature, Bug, Improvement, TechDebt };

    public static bool IsBuiltIn(string? type) =>
        type != null && BuiltIns.Contains(type, StringComparer.Ordinal);

    /// Map a legacy BSON int (0–3) to its built-in name. Returns null for out-of-range values.
    public static string? FromLegacyInt(int value) =>
        value >= 0 && value < BuiltIns.Count ? BuiltIns[value] : null;
}
