using MySqlConnector;
using System.Collections.ObjectModel;

namespace InventiFind;

public partial class NotificationModule : ContentPage
{
    // ── All records fetched from DB ──────────────────────────────────────────
    private List<ReportItem> _allReports = new();

    // ── Pagination ───────────────────────────────────────────────────────────
    private int _currentPage = 1;
    private const int PageSize = 5;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_allReports.Count / (double)PageSize));

    // ── Displayed slice bound to the CollectionView ──────────────────────────
    public ObservableCollection<ReportItem> Reports { get; set; } = new();

    public NotificationModule()
    {
        InitializeComponent();
        ReportCollection.ItemsSource = Reports;
        LoadReports();
    }

    // ── Load all reports from DB, then render page 1 ─────────────────────────
    private async Task LoadReports()
    {
        try
        {
            int currentUserId = Preferences.Get("UserID", 0);

            using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            const string query = @"
SELECT 
    ir.item_name,
    ir.description,
    ir.date_reported,
    ir.report_type,
    u.FirstName,
    u.Surname
FROM item_reports ir
JOIN users u ON ir.user_id = u.UserID
WHERE ir.user_id != @currentUser
ORDER BY ir.date_reported DESC";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@currentUser", currentUserId);

            using var reader = await cmd.ExecuteReaderAsync();
            _allReports.Clear();

            while (await reader.ReadAsync())
            {
                _allReports.Add(new ReportItem
                {
                    Title = reader["item_name"].ToString(),
                    Description = reader["description"]?.ToString(),
                    Date = Convert.ToDateTime(reader["date_reported"]).ToString("yyyy-MM-dd"),
                    Author = $"{reader["FirstName"]} {reader["Surname"]}",
                    RType = reader["report_type"].ToString()
                });
            }

            _currentPage = 1;
            RenderPage();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ── Render current page slice + rebuild page buttons ─────────────────────
    private void RenderPage()
    {
        // Slice for current page
        var slice = _allReports
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        Reports.Clear();
        foreach (var item in slice)
            Reports.Add(item);

        BuildPageButtons();
    }

    // ── Build numbered buttons with ellipsis logic ────────────────────────────
    //    Example outputs:
    //      [1] 2 3               (total ≤ 3, near start)
    //      1 … 4 [5] 6 … 10     (middle)
    //      1 … 8 9 [10]          (near end)
    private void BuildPageButtons()
    {
        PageButtonsLayout.Children.Clear();

        if (TotalPages <= 1) return;

        var pages = GetPageNumbers();
        int? prev = null;

        foreach (int page in pages)
        {
            if (prev.HasValue && page > prev.Value + 1)
                PageButtonsLayout.Children.Add(EllipsisLabel());

            PageButtonsLayout.Children.Add(MakePageButton(page));
            prev = page;
        }
    }

    /// <summary>Returns sorted page numbers to show: always first, last, and ±1 window around current.</summary>
    private IEnumerable<int> GetPageNumbers()
    {
        var set = new SortedSet<int> { 1, TotalPages };
        for (int i = Math.Max(1, _currentPage - 1); i <= Math.Min(TotalPages, _currentPage + 1); i++)
            set.Add(i);
        return set;
    }

    // ── Button factories ──────────────────────────────────────────────────────
    private Frame MakePageButton(int page)
    {
        bool active = page == _currentPage;

        var frame = new Frame
        {
            BackgroundColor = active ? Color.FromArgb("#B71C1C") : Colors.White,
            BorderColor = Color.FromArgb("#B71C1C"),
            CornerRadius = 8,
            Padding = new Thickness(10, 4),
            HasShadow = false,
            Content = new Label
            {
                Text = page.ToString(),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = active ? Colors.White : Color.FromArgb("#B71C1C"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        int captured = page;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => GoToPage(captured);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private static Label EllipsisLabel() => new()
    {
        Text = "…",
        FontSize = 12,
        TextColor = Color.FromArgb("#B71C1C"),
        VerticalOptions = LayoutOptions.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    // ── Navigation ────────────────────────────────────────────────────────────
    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages || page == _currentPage) return;
        _currentPage = page;
        RenderPage();
    }

    // ── Bottom nav handlers ───────────────────────────────────────────────────
    private async void OnHomeTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new StudentDashboard());

    private async void OnReportTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new ReportModule());

    private async void OnReceiveTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new ReceiveModule());

    private void OnNewsTapped(object sender, TappedEventArgs e) { /* already here */ }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");
        if (confirm)
            await Shell.Current.GoToAsync("//MainPage");
    }

    // ── Model ─────────────────────────────────────────────────────────────────
    public class ReportItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string Author { get; set; }
        public string RType { get; set; }
    }
}