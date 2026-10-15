using System.Text.Json.Serialization;

namespace MuralInstitucional.Models;

[JsonConverter(typeof(JsonStringEnumConverter<DistributionKind>))]
public enum DistributionKind { General, Sectors, TopicsOrSkills, Unit }
public sealed record AnnouncementDistribution
{
    public DistributionKind Kind { get; init; }
    public List<string> Criteria { get; init; } = [];
    [JsonIgnore] public string Summary => Kind == DistributionKind.General ? "Distribuição geral" :
        $"{Label(Kind)}: {string.Join(", ", Criteria)}";
    public static string Label(DistributionKind kind) => kind switch {
        DistributionKind.General => "Distribuição geral", DistributionKind.Sectors => "Setores selecionados",
        DistributionKind.TopicsOrSkills => "Assuntos ou habilidades", DistributionKind.Unit => "Unidade", _ => "Inválida" };
    public static bool IsValid(AnnouncementDistribution? value) => value is not null && Enum.IsDefined(value.Kind)
        && value.Criteria is not null && value.Criteria.Count <= 30
        && value.Criteria.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100 && x == x.Trim())
        && value.Criteria.Distinct(StringComparer.OrdinalIgnoreCase).Count() == value.Criteria.Count
        && (value.Kind == DistributionKind.General ? value.Criteria.Count == 0 :
            value.Kind == DistributionKind.Unit ? value.Criteria.Count == 1 : value.Criteria.Count > 0);
}
