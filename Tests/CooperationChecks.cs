using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MuralInstitucional.Controls;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

internal static class CooperationChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "cooperation");
        string demosDirectory = Path.Combine(directory, "demos");
        string usersDirectory = Path.Combine(directory, "users");
        var media = new ManagedMediaService(Path.Combine(directory, "media"));
        BoardViewModel Open() => new(new AnnouncementRepository(demosDirectory), new AnnouncementRepository(usersDirectory, "local-user"), media);
        var board = Open();
        string video = Path.Combine(AppContext.BaseDirectory, "Assets", "demo-mural.avi");
        var demand = board.SaveAnnouncement(null, new("Demanda", "Descrição", MediaKind.Video, video, "https://example.org", AnnouncementType.Demanda));
        var offer = board.SaveAnnouncement(null, new("Oferta", null, MediaKind.Video, video, null, AnnouncementType.Oferta));
        var notice = board.SaveAnnouncement(null, new("Comunicado", null, MediaKind.Video, video, null, AnnouncementType.Comunicado));
        foreach (var item in new[] { demand, offer, notice })
        {
            var card = new AnnouncementCard { DataContext = item };
            Layout(card);
            var actions = (StackPanel)card.FindName("CooperationActions");
            check(actions.Visibility == (item.CanCooperate ? Visibility.Visible : Visibility.Collapsed), "Controles de interesse: visibilidade em " + item.Type);
            var square = (SquareMediaPanel)card.FindName("MediaSquare");
            check(square.ActualWidth == square.ActualHeight && square.ActualWidth > 0, "Interesse preserva mídia quadrada em " + item.Type);
            check(((Button)card.FindName("CooperationInterestButton")).Content.ToString() == "Tenho interesse em cooperar", "Texto inicial do botão de alternância em " + item.Type);
            card.StopMedia();
        }
        check(!board.RegisterCooperationInterest(notice) && notice.CooperationInterest is null, "Comunicado rejeita registro de interesse");
        var foreign = new Announcement { Id = Guid.NewGuid(), Type = AnnouncementType.Demanda };
        check(!board.RegisterCooperationInterest(foreign) && !board.CancelCooperationInterest(foreign), "Anúncio fora da coleção rejeitado");
        board.ToggleLike(demand); board.AddComment(demand, "Comentário independente");
        int likes = demand.LikeCount;
        int comments = demand.Comments.Count;
        check(board.RegisterCooperationInterest(demand) && demand.CooperationInterest!.AnnouncementId == demand.Id
            && demand.CooperationInterest.Scope == LocalCooperationInterest.LocalScope, "Registro associado ao anúncio e escopo local demonstrativo");
        var interest = demand.CooperationInterest;
        check(!board.RegisterCooperationInterest(demand) && demand.CooperationInterest == interest, "Registro repetido é idempotente, sem duplicidade ou nova data");
        check(demand.LikeCount == likes && demand.Comments.Count == comments, "Interesse independente de curtidas e comentários");
        var activeCard = new AnnouncementCard { DataContext = demand };
        Layout(activeCard);
        var toggle = (Button)activeCard.FindName("CooperationInterestButton");
        var activeBackground = toggle.Background;
        check(toggle.Content.ToString() == "✓ Interesse registrado" && toggle.IsEnabled
            && toggle.ToolTip?.ToString() == "Clique para cancelar seu interesse", "Botão ativo habilitado com marca de seleção e dica de cancelamento");
        check(((Button)activeCard.FindName("PlayButton")).IsEnabled && ((Button)activeCard.FindName("RestartButton")).IsEnabled
            && ((MediaElement)activeCard.FindName("Video")).Source is null
            && ((Button)activeCard.FindName("LinkSurface")).Visibility == Visibility.Visible, "Interesse preserva link e controles de vídeo sem reprodução automática");
        var reopened = Open();
        var persisted = reopened.Announcements.Single(x => x.Id == demand.Id);
        check(persisted.CooperationInterest == interest && persisted.LikeCount == likes && persisted.Comments.Count == comments,
            "Reabertura preserva interesse, curtida e comentário");
        check(reopened.RegisterCooperationInterest(reopened.Announcements.Single(x => x.Id == offer.Id)), "Oferta registra interesse independentemente da Demanda");
        foreach (var filter in reopened.TypeFilters)
        {
            reopened.SelectedTypeFilter = filter;
            check(reopened.VisibleAnnouncements.Cast<Announcement>().All(x => filter.Value is null || x.Type == filter.Value), "Interesse compatível com filtro " + filter.Name);
        }
        var edited = reopened.SaveAnnouncement(persisted, new("Editado", persisted.Description, MediaKind.Video, persisted.MediaPath!, persisted.ExternalUrl, AnnouncementType.Oferta));
        check(edited.Id == demand.Id && edited.CooperationInterest == interest && edited.LikeCount == likes
            && edited.Comments.Count == comments && edited.ExternalUrl == demand.ExternalUrl, "Edição preserva interesse e demais interações");
        var comunicado = reopened.SaveAnnouncement(edited, new("Virou comunicado", null, MediaKind.Video, edited.MediaPath!, edited.ExternalUrl, AnnouncementType.Comunicado));
        check(comunicado.CooperationInterest == interest && !comunicado.CanCooperate, "Mudança para Comunicado preserva registro e oculta controles");
        var restored = reopened.SaveAnnouncement(comunicado, new("Demanda novamente", null, MediaKind.Video, comunicado.MediaPath!, comunicado.ExternalUrl, AnnouncementType.Demanda));
        check(restored.HasCooperationInterest && restored.CanCooperate, "Retorno para Demanda restaura acesso ao interesse preservado");
        activeCard.DataContext = restored; Layout(activeCard);
        check(reopened.CancelCooperationInterest(restored) && !reopened.CancelCooperationInterest(restored), "Cancelamento remove registro e repetição não altera estado");
        Layout(activeCard);
        check(toggle.Content.ToString() == "Tenho interesse em cooperar" && toggle.ToolTip is null
            && toggle.Background.ToString() != activeBackground.ToString(), "Cancelamento restaura texto e cor e remove dica de cancelamento");
        check(reopened.ToggleCooperationInterest(restored) && restored.HasCooperationInterest, "Alternância registra interesse");
        Layout(activeCard);
        check(toggle.Content.ToString() == "✓ Interesse registrado", "Alternância atualiza texto pelo binding");
        check(reopened.ToggleCooperationInterest(restored) && !restored.HasCooperationInterest, "Segundo clique cancela interesse");
        check(!reopened.ToggleCooperationInterest(foreign) && !reopened.ToggleCooperationInterest(comunicado), "Alternância rejeita anúncio externo ou Comunicado");
        var afterCancel = Open();
        check(!afterCancel.Announcements.Single(x => x.Id == demand.Id).HasCooperationInterest, "Cancelamento persiste após reabrir");
        var deleting = afterCancel.Announcements.Single(x => x.Id == offer.Id);
        afterCancel.DeleteAnnouncement(deleting);
        check(!Open().Announcements.Any(x => x.Id == offer.Id)
            && !File.ReadAllText(Path.Combine(usersDirectory, "announcements.json")).Contains(offer.Id.ToString()), "Exclusão limpa anúncio e interesse associado no JSON");
        activeCard.StopMedia();
        // Compatibility with both legacy stores and missing files is also covered by existing checks.
        foreach (string kind in new[] { "local-demo", "local-user" })
        {
            string legacyDirectory = Path.Combine(directory, "legacy-" + kind);
            Directory.CreateDirectory(legacyDirectory);
            var doc = new DemoDocument { DataKind = kind, Announcements = [new Announcement { Id = Guid.NewGuid(), Title = "Antigo", Type = AnnouncementType.Demanda }] };
            var json = JsonSerializer.SerializeToNode(doc)!;
            json["Announcements"]![0]!.AsObject().Remove("CooperationInterest");
            string path = Path.Combine(legacyDirectory, "announcements.json");
            File.WriteAllText(path, json.ToJsonString());
            var repo = new AnnouncementRepository(legacyDirectory, kind);
            var loaded = repo.Load(out _);
            check(!loaded.Announcements[0].HasCooperationInterest, "JSON anterior sem interesse compatível: " + kind);
            repo.Save(loaded.Announcements);
            foreach (var bad in new[] {
                new LocalCooperationInterest { AnnouncementId = Guid.NewGuid(), RegisteredAt = DateTimeOffset.Now },
                new LocalCooperationInterest { AnnouncementId = doc.Announcements[0].Id },
                new LocalCooperationInterest { AnnouncementId = doc.Announcements[0].Id, RegisteredAt = DateTimeOffset.Now, Scope = "authenticated" } })
            {
                loaded.Announcements[0].CooperationInterest = bad;
                string before = File.ReadAllText(path);
                bool rejected = false;
                try { repo.Save(loaded.Announcements); } catch (InvalidDataException) { rejected = true; }
                check(rejected && File.ReadAllText(path) == before, "Registro inconsistente rejeitado sem sobrescrever: " + kind);
            }
            loaded.Announcements[0].CooperationInterest = new LocalCooperationInterest { AnnouncementId = Guid.NewGuid(), RegisteredAt = DateTimeOffset.Now };
            string inconsistent = JsonSerializer.Serialize(loaded);
            File.WriteAllText(path, inconsistent);
            if (kind == "local-user")
            {
                bool blocked = false;
                try { new AnnouncementRepository(legacyDirectory, kind).Load(out _); } catch (InvalidDataException) { blocked = true; }
                check(blocked && File.ReadAllText(path) == inconsistent, "Interesse inconsistente bloqueia cadastro e preserva arquivo original");
            }
            else
            {
                var recovered = new AnnouncementRepository(legacyDirectory, kind).Load(out var message);
                check(recovered.Announcements.Count == 9 && message.Contains("original preservado")
                    && Directory.GetFiles(legacyDirectory, "*.invalid-*").Any(x => File.ReadAllText(x) == inconsistent), "Interesse inconsistente em exemplos preservado em backup");
            }
        }
        var failure = new BoardViewModel(new FailingRepository());
        var session = failure.Announcements[0]; session.Type = AnnouncementType.Demanda;
        check(failure.RegisterCooperationInterest(session) && session.HasCooperationInterest && failure.Notice.Contains("não foram salvas"), "Falha de gravação mantém interesse na sessão e informa ausência de persistência");
        check(failure.CancelCooperationInterest(session) && !session.HasCooperationInterest && failure.Notice.Contains("não foram salvas"), "Falha de gravação no cancelamento informa ausência de persistência");
    }
    private static void Layout(AnnouncementCard card)
    {
        card.Measure(new Size(300, double.PositiveInfinity));
        card.Arrange(new Rect(0, 0, 300, card.DesiredSize.Height));
        card.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private sealed class FailingRepository : IAnnouncementRepository
    {
        public string DataPath => "simulado";
        public DemoDocument Load(out string notice) { notice = "Teste"; return AnnouncementRepository.CreateDemo(); }
        public void Save(IEnumerable<Announcement> announcements) => throw new IOException("Falha simulada");
    }
}
