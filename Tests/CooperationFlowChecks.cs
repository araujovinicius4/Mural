using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using MuralInstitucional;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

internal static class CooperationFlowChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        foreach (var kind in new[] { "local-demo", "local-user" })
        {
            var directory = Path.Combine(root, "flow-legacy-" + kind);
            Directory.CreateDirectory(directory);
            var id = Guid.NewGuid();
            var interest = new LocalCooperationInterest { AnnouncementId = id, RegisteredAt = DateTimeOffset.Now.AddDays(-2) };
            var document = new DemoDocument { DataKind = kind, Announcements = [new Announcement {
                Id = id, Title = "Legado", Type = AnnouncementType.Oferta, CooperationInterest = interest, LikedLocally = true,
                Comments = [new DemoComment { AnnouncementId = id, Text = "Preservado" }] }] };
            var json = JsonSerializer.SerializeToNode(document)!;
            json["Announcements"]![0]!.AsObject().Remove("CooperationRequests");
            json["Announcements"]![0]!.AsObject().Remove("Distribution");
            var path = Path.Combine(directory, "announcements.json");
            File.WriteAllText(path, json.ToJsonString());
            var repository = new AnnouncementRepository(directory, kind);
            var loaded = repository.Load(out _).Announcements.Single();
            check(loaded.CooperationInterest == interest && loaded.CooperationRequests.Single().Interest == interest
                && loaded.CooperationRequests[0].State == CooperationState.InterestRegistered
                && !loaded.CooperationRequests[0].IdentificationSimulated, "Migração de interesse preserva dados e não concede consentimento: " + kind);
            check(loaded.Distribution.Kind == DistributionKind.General && loaded.LikedLocally && loaded.Comments.Single().Text == "Preservado",
                "Migração usa distribuição geral e preserva interações: " + kind);
            repository.Save([loaded]);
            var again = new AnnouncementRepository(directory, kind).Load(out _).Announcements.Single();
            check(again.CooperationRequests.Count == 1 && again.CooperationRequests[0].Id == loaded.CooperationRequests[0].Id,
                "Migração idempotente sem duplicação: " + kind);
        }
        var demos = Path.Combine(root, "flow-demos");
        var users = Path.Combine(root, "flow-users");
        var media = new ManagedMediaService(Path.Combine(root, "flow-media"));
        BoardViewModel Open() => new(new AnnouncementRepository(demos), new AnnouncementRepository(users, "local-user"), media);
        var board = Open();
        var video = Path.Combine(AppContext.BaseDirectory, "Assets", "demo-mural.avi");
        foreach (var type in new[] { AnnouncementType.Demanda, AnnouncementType.Oferta })
        {
            var item = board.SaveAnnouncement(null, new("Fluxo " + type, null, MediaKind.Video, video, null, type,
                new() { Kind = DistributionKind.Sectors, Criteria = ["Biblioteca", "Tecnologia"] }));
            check(board.RegisterCooperationInterest(item), "Registro inicial: " + type);
            var request = item.CooperationRequests.Single();
            bool Move(CooperationState state, string? reason = null) => board.TransitionCooperation(item, request.Id, state, reason);
            check(!Move(CooperationState.Accepted) && !Move(CooperationState.InProgress)
                && !board.ConsentCooperation(item, request.Id, true), "Saltos e consentimento antecipado recusados: " + type);
            check(Move(CooperationState.AwaitingAcceptance) && !Move(CooperationState.Accepted, new string('x', 501))
                && Move(CooperationState.Accepted, "Habilidade e disponibilidade compatíveis"), "Análise e justificativa objetiva limitada: " + type);
            check(!item.CooperationRequests[0].IdentificationSimulated && item.Distribution.Kind == DistributionKind.Sectors,
                "Aceite não identifica nem altera distribuição/publicação: " + type);
            check(Move(CooperationState.AwaitingConsent) && !Move(CooperationState.InProgress)
                && board.ConsentCooperation(item, request.Id, true) && !Move(CooperationState.InProgress)
                && !board.ConsentCooperation(item, request.Id, true), "Um consentimento não permite início nem duplicação: " + type);
            var reopened = Open().Announcements.Single(x => x.Id == item.Id);
            check(reopened.CooperationRequests[0].RequesterConsent && !reopened.CooperationRequests[0].ResponsibleConsent
                && reopened.CooperationRequests[0].State == CooperationState.AwaitingConsent,
                "Consentimento parcial persiste sem identificar: " + type);
            check(board.ConsentCooperation(item, request.Id, false) && Move(CooperationState.InProgress)
                && Move(CooperationState.Completed) && !Move(CooperationState.Cancelled), "Ambos consentem, início e conclusão terminal: " + type);
            var history = item.CooperationRequests[0].History;
            var final = Open().Announcements.Single(x => x.Id == item.Id);
            check(final.CooperationRequests[0].History.SequenceEqual(history) && final.CooperationRequests[0].IdentificationSimulated,
                "Histórico completo e consentimentos persistem: " + type);
            var edited = board.SaveAnnouncement(item, new("Editado", null, MediaKind.Video, item.MediaPath!, null, type, item.Distribution));
            board.SetVideo(edited, video);
            edited = board.Announcements.Single(x => x.Id == edited.Id);
            check(edited.CooperationRequests[0].History.SequenceEqual(history) && edited.Distribution.Criteria.Count == 2,
                "Edição e troca de mídia preservam distribuição e fluxo: " + type);
            var notice = board.SaveAnnouncement(edited, new("Comunicado", null, MediaKind.Video, edited.MediaPath!, null, AnnouncementType.Comunicado));
            check(!board.RegisterCooperationInterest(notice) && !board.TransitionCooperation(notice, request.Id, CooperationState.Cancelled)
                && !board.ConsentCooperation(notice, request.Id, true) && !board.CancelCooperationInterest(notice)
                && notice.CooperationRequests[0].History.SequenceEqual(history), "Comunicado preserva histórico sem participar do fluxo: " + type);
            var restored = board.SaveAnnouncement(notice, new("Restaurado", null, MediaKind.Video, notice.MediaPath!, null, type));
            check(board.CancelCooperationInterest(restored) && board.RegisterCooperationInterest(restored)
                && restored.CooperationRequests.Count == 2 && restored.CooperationRequests.Select(x => x.Id).Distinct().Count() == 2
                && restored.CooperationRequests[0].State == CooperationState.Completed, "Nova solicitação tem ID próprio e preserva anterior: " + type);
            var second = restored.CooperationRequests[^1];
            check(board.TransitionCooperation(restored, second.Id, CooperationState.AwaitingAcceptance)
                && board.TransitionCooperation(restored, second.Id, CooperationState.Refused, "Disponibilidade incompatível")
                && !board.TransitionCooperation(restored, second.Id, CooperationState.Accepted), "Recusa objetiva é terminal: " + type);
            usingWindow(restored, board, check);
        }
        foreach (var from in Enum.GetValues<CooperationState>())
        {
            var item = new Announcement { Id = Guid.NewGuid(), Title = "Cancelamento", Type = AnnouncementType.Demanda };
            CooperationInterestService.Register(item);
            var id = item.CooperationRequests[0].Id;
            if (from != CooperationState.InterestRegistered) CooperationInterestService.Transition(item, id, CooperationState.AwaitingAcceptance);
            if (from == CooperationState.Refused) CooperationInterestService.Transition(item, id, from);
            if (from is CooperationState.Accepted or CooperationState.AwaitingConsent or CooperationState.InProgress or CooperationState.Completed)
                CooperationInterestService.Transition(item, id, CooperationState.Accepted);
            if (from is CooperationState.AwaitingConsent or CooperationState.InProgress or CooperationState.Completed)
                CooperationInterestService.Transition(item, id, CooperationState.AwaitingConsent);
            if (from is CooperationState.InProgress or CooperationState.Completed) {
                CooperationInterestService.Consent(item, id, false); CooperationInterestService.Consent(item, id, true);
                CooperationInterestService.Transition(item, id, CooperationState.InProgress);
            }
            if (from is CooperationState.Completed or CooperationState.Cancelled) CooperationInterestService.Transition(item, id, from);
            foreach (var target in Enum.GetValues<CooperationState>())
            {
                var copy = new Announcement { Id = item.Id, Type = item.Type, CooperationInterest = item.CooperationInterest,
                    CooperationRequests = item.CooperationRequests.ToList() };
                var permitted = (from, target) is
                    (CooperationState.InterestRegistered, CooperationState.AwaitingAcceptance)
                    or (CooperationState.AwaitingAcceptance, CooperationState.Accepted)
                    or (CooperationState.AwaitingAcceptance, CooperationState.Refused)
                    or (CooperationState.Accepted, CooperationState.AwaitingConsent)
                    or (CooperationState.InProgress, CooperationState.Completed)
                    || target == CooperationState.Cancelled && from is not (CooperationState.Completed or CooperationState.Cancelled or CooperationState.Refused);
                check(CooperationInterestService.Transition(copy, id, target) == permitted && CooperationInterestService.IsValid(copy),
                    $"Matriz de transições: {from} -> {target}");
            }
            var expected = from is not (CooperationState.Completed or CooperationState.Cancelled or CooperationState.Refused);
            check(CooperationInterestService.Transition(item, id, CooperationState.Cancelled) == expected && CooperationInterestService.IsValid(item),
                "Cancelamento e integridade a partir de " + from);
        }
        foreach (var distribution in new[] {
            new AnnouncementDistribution(), new() { Kind = DistributionKind.Sectors, Criteria = ["Setor A", "Setor B"] },
            new() { Kind = DistributionKind.TopicsOrSkills, Criteria = ["Revisão", "Planilhas"] }, new() { Kind = DistributionKind.Unit, Criteria = ["Unidade A"] } })
        {
            var item = board.SaveAnnouncement(null, new(distribution.Summary, null, MediaKind.Video, video, null, AnnouncementType.Comunicado, distribution));
            var reopened = Open().Announcements.Single(x => x.Id == item.Id);
            check(reopened.Distribution.Kind == distribution.Kind && reopened.Distribution.Criteria.SequenceEqual(distribution.Criteria),
                "Distribuição persiste: " + distribution.Kind);
        }
        foreach (var bad in new[] {
            new AnnouncementDistribution { Kind = (DistributionKind)99 }, new() { Kind = DistributionKind.Sectors },
            new() { Kind = DistributionKind.General, Criteria = ["Extra"] }, new() { Kind = DistributionKind.Unit, Criteria = ["A", "B"] },
            new() { Kind = DistributionKind.TopicsOrSkills, Criteria = ["A", "a"] }, new() { Kind = DistributionKind.Sectors, Criteria = [" "] },
            new() { Kind = DistributionKind.Unit, Criteria = [new string('a', 101)] } })
        {
            var before = board.Announcements.Count;
            bool rejected = false;
            try { board.SaveAnnouncement(null, new("Inválido", null, MediaKind.Video, video, null, AnnouncementType.Oferta, bad)); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && board.Announcements.Count == before, "Distribuição inválida rejeitada antes de cadastrar: " + bad.Kind);
        }
        var integrity = board.Announcements.First(x => x.CanCooperate && x.CooperationRequests.Count > 0);
        var original = integrity.CooperationRequests[0];
        var repo = new AnnouncementRepository(users, "local-user"); repo.Load(out _);
        var stored = File.ReadAllText(repo.DataPath);
        foreach (var bad in new[] {
            original with { State = (CooperationState)999 }, original with { ResponsibleConsent = false },
            original with { History = [] }, original with { Id = Guid.Empty }, original with { Interest = original.Interest with { AnnouncementId = Guid.NewGuid() } } })
        {
            integrity.CooperationRequests[0] = bad;
            bool rejected = false;
            try { repo.Save(board.Announcements.Where(x => !x.IsExample)); } catch (InvalidDataException) { rejected = true; }
            check(rejected && File.ReadAllText(repo.DataPath) == stored, "Solicitação inconsistente rejeitada sem sobrescrever JSON");
        }
        integrity.CooperationRequests[0] = original;
        foreach (var kind in new[] { "local-demo", "local-user" })
        {
            var directory = Path.Combine(root, "invalid-flow-" + kind);
            Directory.CreateDirectory(directory);
            var doc = new DemoDocument { DataKind = kind, Announcements = [integrity] };
            var invalid = JsonSerializer.SerializeToNode(doc)!;
            invalid["Announcements"]![0]!["CooperationRequests"]![0]!["RequesterConsent"] = false;
            var path = Path.Combine(directory, "announcements.json");
            var bytes = invalid.ToJsonString(); File.WriteAllText(path, bytes);
            var repository = new AnnouncementRepository(directory, kind);
            if (kind == "local-user") {
                bool rejected = false;
                try { repository.Load(out _); } catch (InvalidDataException) { rejected = true; }
                check(rejected && File.ReadAllText(path) == bytes, "Leitura de fluxo inconsistente preserva cadastro original");
            }
            else {
                repository.Load(out _);
                check(Directory.GetFiles(directory, "*.invalid-*").Any(x => File.ReadAllText(x) == bytes),
                    "Leitura de fluxo inconsistente preserva demonstração em backup");
            }
        }
        var editor = new AnnouncementEditorWindow(board, integrity);
        check(((ComboBox)editor.FindName("DistributionInput")).SelectedValue is DistributionKind.Sectors
            && ((TextBox)editor.FindName("DistributionCriteria")).Text.Contains("Tecnologia"), "Editor carrega múltiplos critérios existentes");
        ((ComboBox)editor.FindName("DistributionInput")).SelectedValue = DistributionKind.General;
        check(!((TextBox)editor.FindName("DistributionCriteria")).IsEnabled, "Distribuição geral desabilita critérios específicos no editor");
        editor.Close();
        var foreign = new Announcement { Id = Guid.NewGuid(), Type = AnnouncementType.Demanda };
        check(!board.TransitionCooperation(foreign, original.Id, CooperationState.AwaitingAcceptance)
            && !board.ConsentCooperation(foreign, original.Id, true), "Fluxo rejeita anúncio fora da coleção");
    }

    private static void usingWindow(Announcement item, BoardViewModel board, Action<bool, string> check)
    {
        var window = new CooperationDetailsWindow(item, board);
        var input = (ComboBox)window.FindName("RequestInput");
        check(input.Items.Count == 2 && ((TextBlock)window.FindName("StateText")).Text == "Solicitação recusada",
            "Detalhes instanciam e selecionam solicitação local atual");
        input.SelectedIndex = 0;
        check(((TextBlock)window.FindName("StateText")).Text == "Cooperação concluída", "Detalhes permitem inspecionar histórico de outra solicitação");
        window.Close();
    }
}
