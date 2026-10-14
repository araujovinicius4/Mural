using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

internal static class ClassificationChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        // Exercise both legacy stores, including interactions and every persisted field.
        foreach (string kind in new[] { "local-demo", "local-user" })
        {
            string directory = Path.Combine(root, "legacy-" + kind);
            Directory.CreateDirectory(directory);
            var document = AnnouncementRepository.CreateDemo();
            document.DataKind = kind;
            if (kind == "local-user")
                foreach (var item in document.Announcements)
                {
                    item.Id = Guid.NewGuid();
                    foreach (var comment in item.Comments) comment.AnnouncementId = item.Id;
                }
            document.Announcements[0].LikedLocally = true;
            var legacy = JsonSerializer.SerializeToNode(document)!;
            foreach (var item in legacy["Announcements"]!.AsArray()) { item!.AsObject().Remove("Type"); item.AsObject().Remove("CooperationInterest"); }
            string path = Path.Combine(directory, "announcements.json");
            File.WriteAllText(path, legacy.ToJsonString());
            var repo = new AnnouncementRepository(directory, kind);
            var board = kind == "local-demo" ? new BoardViewModel(repo)
                : new BoardViewModel(new AnnouncementRepository(Path.Combine(root, "migration-demos")), repo);
            check(board.Announcements.All(x => x.Type == AnnouncementType.Comunicado), "Migração " + kind + ": categoria inicial Comunicado");
            var migrated = JsonNode.Parse(File.ReadAllText(path))!;
            check(migrated["Announcements"]!.AsArray().All(x => x!["Type"]!.GetValue<string>() == "Comunicado"),
                "Migração " + kind + ": categoria gravada ao abrir");
            foreach (var item in migrated["Announcements"]!.AsArray()) { item!.AsObject().Remove("Type"); item.AsObject().Remove("CooperationInterest"); }
            check(JsonNode.DeepEquals(legacy, migrated), "Migração " + kind + ": todos os dados antigos preservados");
            var reopened = new AnnouncementRepository(directory, kind).Load(out _);
            check(reopened.Announcements[0].LikedLocally && reopened.Announcements[0].Comments.Count == 1,
                "Migração " + kind + ": curtidas e comentários preservados ao reabrir");
        }
        var repository = new AnnouncementRepository(Path.Combine(root, "categories"));
        var model = new BoardViewModel(repository);
        for (int i = 0; i < model.Announcements.Count; i++)
            model.Announcements[i].Type = (AnnouncementType)(i % 3);
        model.Save();
        foreach (var filter in model.TypeFilters)
        {
            model.SelectedTypeFilter = filter;
            var expected = model.Announcements.Where(x => filter.Value is null || x.Type == filter.Value).ToArray();
            check(model.VisibleAnnouncements.Cast<Announcement>().SequenceEqual(expected), "Filtro " + filter.Name + ": seleção e ordem corretas");
            model.Save();
            check(new AnnouncementRepository(Path.Combine(root, "categories")).Load(out _).Announcements.Count == 9,
                "Salvar com filtro " + filter.Name + " preserva todos os registros");
        }
        var loaded = new AnnouncementRepository(Path.Combine(root, "categories")).Load(out _);
        check(loaded.Announcements.Select(x => x.Type).SequenceEqual(model.Announcements.Select(x => x.Type)), "Três categorias persistem após reabrir");
        model.SelectedTypeFilter = model.TypeFilters.Single(x => x.Value == AnnouncementType.Demanda);
        model.Announcements.Clear();
        check(model.VisibleAnnouncements.IsEmpty, "Filtro aceita coleção vazia");
        var added = new Announcement { Id = Guid.NewGuid(), Title = "Oferta", Type = AnnouncementType.Oferta };
        model.Announcements.Add(added);
        check(model.VisibleAnnouncements.IsEmpty, "Novo anúncio fora do filtro permanece oculto");
        var changed = new Announcement { Id = added.Id, Title = added.Title, Type = AnnouncementType.Demanda };
        model.Announcements[0] = changed;
        check(model.VisibleAnnouncements.Cast<Announcement>().Single() == changed, "Edição atualiza filtro pela substituição do anúncio");
        model.Announcements.Remove(changed);
        check(model.VisibleAnnouncements.IsEmpty, "Exclusão atualiza filtro");
        bool Reject(AnnouncementType? type)
        {
            try { AnnouncementManagementService.Validate(new("Teste", null, MediaKind.Image, "", null, type)); }
            catch (InvalidDataException ex) { return ex.Message.Contains("Tipo de anúncio"); }
            return false;
        }
        check(Reject(null) && Reject((AnnouncementType)999), "Cadastro rejeita categoria ausente ou desconhecida antes de importar mídia");
        var invalid = new Announcement { Id = Guid.NewGuid(), Title = "Inválido", Type = (AnnouncementType)999 };
        bool rejected = false;
        try { repository.Save([invalid]); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Repositório rejeita categoria desconhecida sem gravar");
    }
}
