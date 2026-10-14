using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using MuralInstitucional.Models;
using MuralInstitucional.Services;

namespace MuralInstitucional.ViewModels;

public sealed class BoardViewModel : INotifyPropertyChanged
{
    private readonly IAnnouncementRepository repository;
    private readonly IAnnouncementRepository? users;
    private readonly ManagedMediaService? media;
    private bool usersLoaded;
    public bool CanManageUsers => usersLoaded && users is not null && media is not null;
    public string UserDataPath => users?.DataPath ?? "Cadastro não disponível";
    public string MediaDirectory => media?.DirectoryPath ?? "";
    public ObservableCollection<Announcement> Announcements { get; } = [];
    public ICollectionView VisibleAnnouncements { get; }
    public IReadOnlyList<AnnouncementTypeOption> TypeFilters => AnnouncementTypes.Filters;
    private AnnouncementTypeOption selectedTypeFilter = AnnouncementTypes.Filters[0];
    public AnnouncementTypeOption SelectedTypeFilter
    {
        get => selectedTypeFilter;
        set
        {
            if (value is null || !TypeFilters.Contains(value)) return;
            selectedTypeFilter = value;
            VisibleAnnouncements.Refresh();
            PropertyChanged?.Invoke(this, new(nameof(SelectedTypeFilter)));
        }
    }
    private string notice = "";
    public string Notice { get => notice; private set { notice = value; PropertyChanged?.Invoke(this, new(nameof(Notice))); } }
    public string DataPath => repository.DataPath;
    private bool canPersist = true;
    private bool saveFailed;
    public BoardViewModel(IAnnouncementRepository repository, IAnnouncementRepository? users = null, ManagedMediaService? media = null)
    {
        VisibleAnnouncements = new ListCollectionView(Announcements);
        VisibleAnnouncements.Filter = item => selectedTypeFilter.Value is null || ((Announcement)item).Type == selectedTypeFilter.Value;
        this.repository = repository;
        this.users = users;
        this.media = media;
        try
        {
            var document = repository.Load(out var message);
            foreach (var item in document.Announcements) Announcements.Add(item);
            Notice = message;
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Do not overwrite an unreadable file. The UI remains usable with a clear warning.
            canPersist = false;
            if (Announcements.Count == 0)
                foreach (var item in AnnouncementRepository.CreateDemo().Announcements) Announcements.Add(item);
            Notice = $"Não foi possível carregar os dados locais: {ex.Message}. Gravação desativada nesta execução.";
        }
        if (users is not null)
        {
            try
            {
                var document = users.Load(out _);
                if (document.Announcements.Any(item => Announcements.Any(x => x.Id == item.Id)))
                    throw new InvalidDataException("Identificadores repetidos entre exemplos e anúncios cadastrados.");
                int insertion = 0;
                foreach (var item in document.Announcements) { item.IsExample = false; Announcements.Insert(insertion++, item); }
                users.Save(document.Announcements);
                usersLoaded = true;
                if (Notice.StartsWith("Interações demonstrativas", StringComparison.Ordinal))
                    Notice = "Cadastro neste perfil local • exemplos identificados nos cartões • sem autenticação ou publicação multiusuário.";
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException)
            { Notice = "Cadastro bloqueado para preservar os dados existentes: " + ex.Message; }
        }
    }
    public bool ToggleCooperationInterest(Announcement item)
    {
        if (!Announcements.Contains(item) || !item.CanCooperate) return false;
        return item.HasCooperationInterest ? CancelCooperationInterest(item) : RegisterCooperationInterest(item);
    }
    public bool RegisterCooperationInterest(Announcement item)
    {
        if (!Announcements.Contains(item) || !CooperationInterestService.Register(item)) return false;
        Save();
        return true;
    }
    public bool CancelCooperationInterest(Announcement item)
    {
        if (!Announcements.Contains(item) || !CooperationInterestService.Cancel(item)) return false;
        Save();
        return true;
    }
    public void ToggleLike(Announcement item)
    {
        item.LikedLocally = !item.LikedLocally;
        Save();
    }
    public bool AddComment(Announcement item, string text)
    {
        text = text.Trim();
        if (text.Length is < 1 or > 1000 || item.Comments.Count >= 500) return false;
        item.Comments.Add(new DemoComment { AnnouncementId = item.Id, Text = text });
        item.CommentsChanged();
        Save();
        return true;
    }
    public void SetVideo(Announcement item, string path)
    {
        if (media is not null)
        {
            SaveAnnouncement(item, new AnnouncementDraft(item.Title, item.Description, MediaKind.Video, path, item.ExternalUrl, item.Type));
            return;
        }
        item.MediaPath = path;
        Save();
    }
    public void Notify(string message) => Notice = message;
    public void Save()
    {
        try
        {
            if (canPersist) repository.Save(Announcements.Where(x => x.IsExample));
            if (usersLoaded) users?.Save(Announcements.Where(x => !x.IsExample));
            if (saveFailed) Notice = "Interações demonstrativas locais salvas. Não representam usuários autenticados.";
            saveFailed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.SecurityException)
        { saveFailed = true; Notice = $"Interações mantidas na sessão, mas não foram salvas: {ex.Message}"; }
    }
    public Announcement SaveAnnouncement(Announcement? original, AnnouncementDraft draft)
    {
        if (original is not null && !Announcements.Contains(original)) throw new InvalidOperationException("Anúncio não pertence à lista atual.");
        if (media is null || original?.IsExample != true && !CanManageUsers || original?.IsExample == true && !canPersist)
            throw new InvalidOperationException("Cadastro indisponível; verifique os dados em Configurações.");
        var service = new AnnouncementManagementService(original?.IsExample == true ? repository : users!, media, original?.IsExample == true);
        var saved = service.Save(Announcements, original, draft);
        if (original is not null) Announcements[Announcements.IndexOf(original)] = saved;
        else Announcements.Insert(0, saved);
        Notify(service.CleanupNotice ?? "Anúncio salvo neste perfil local.");
        return saved;
    }
    public void DeleteAnnouncement(Announcement item)
    {
        if (!Announcements.Contains(item)) throw new InvalidOperationException("Anúncio não pertence à lista atual.");
        if (media is null || item.IsExample && !canPersist || !item.IsExample && !CanManageUsers) throw new InvalidOperationException("Exclusão indisponível.");
        var service = new AnnouncementManagementService(item.IsExample ? repository : users!, media, item.IsExample);
        service.Delete(Announcements, item);
        Announcements.Remove(item);
        Notify(service.CleanupNotice ?? "Anúncio excluído deste perfil local.");
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
