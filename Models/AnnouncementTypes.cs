using System.Text.Json.Serialization;
namespace MuralInstitucional.Models;
[JsonConverter(typeof(JsonStringEnumConverter<AnnouncementType>))]
public enum AnnouncementType { Comunicado, Demanda, Oferta }
public sealed record AnnouncementTypeOption(AnnouncementType? Value, string Name);
public static class AnnouncementTypes
{
    public static IReadOnlyList<AnnouncementTypeOption> Choices { get; } = Array.AsReadOnly(new[] {
        new AnnouncementTypeOption(AnnouncementType.Demanda, "Demanda"),
        new AnnouncementTypeOption(AnnouncementType.Oferta, "Oferta"),
        new AnnouncementTypeOption(AnnouncementType.Comunicado, "Comunicado") });
    public static IReadOnlyList<AnnouncementTypeOption> Filters { get; } = Array.AsReadOnly(new[] {
        new AnnouncementTypeOption(null, "Todos"),
        new AnnouncementTypeOption(AnnouncementType.Demanda, "Demandas"),
        new AnnouncementTypeOption(AnnouncementType.Oferta, "Ofertas"),
        new AnnouncementTypeOption(AnnouncementType.Comunicado, "Comunicados") });
    public static string Name(AnnouncementType type) => Choices.Single(x => x.Value == type).Name;
    public static string Color(AnnouncementType type) => type switch {
        AnnouncementType.Demanda => "#FFF4D8", AnnouncementType.Oferta => "#DDF3E6",
        AnnouncementType.Comunicado => "#E2EDFF", _ => throw new ArgumentOutOfRangeException(nameof(type)) };
}
