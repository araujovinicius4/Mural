using System.Windows;
using MuralInstitucional.Models;
using MuralInstitucional.ViewModels;

namespace MuralInstitucional;

public partial class CommentsWindow : Window
{
    private readonly Announcement announcement;
    private readonly BoardViewModel board;
    public CommentsWindow(Announcement announcement, BoardViewModel board)
    {
        InitializeComponent(); this.announcement = announcement; this.board = board; DataContext = announcement;
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!board.AddComment(announcement, CommentInput.Text))
        { Feedback.Text = "Digite um comentário de 1 a 1.000 caracteres. Limite de 500 comentários por anúncio."; return; }
        CommentInput.Clear(); Feedback.Text = "Comentário adicionado. " + board.Notice; CommentInput.Focus();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
