using MySqlConnector;
using System.Collections.ObjectModel;

namespace InventiFind;

public class ReportItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RType { get; set; } = "";
    public string TimeAgo { get; set; } = "";
    public Color BadgeColor => RType?.ToLower() == "lost"
        ? Color.FromArgb("#FF6B6B")
        : Color.FromArgb("#4CAF50");
}

public partial class AdminDashboard : ContentPage
{
    public AdminDashboard()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // ── Total reports ──────────────────────────────────────────────
            int totalReports = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports");

            int totalToday = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE DATE(date_reported) = CURDATE()");

            TotalReportsLabel.Text = totalReports.ToString();
            TotalReportsTodayLabel.Text = $"+{totalToday} today";

            // ── Lost reports ───────────────────────────────────────────────
            int lostTotal = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE report_type = 'lost'");

            int lostToday = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE report_type = 'lost' AND DATE(date_reported) = CURDATE()");

            LostItemsLabel.Text = lostTotal.ToString();
            LostTodayLabel.Text = $"+{lostToday} today";

            // ── Found reports ──────────────────────────────────────────────
            int foundTotal = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE report_type = 'found'");

            int foundToday = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM item_reports WHERE report_type = 'found' AND DATE(date_reported) = CURDATE()");

            FoundItemsLabel.Text = foundTotal.ToString();
            FoundTodayLabel.Text = $"+{foundToday} today";

            // ── Total users ────────────────────────────────────────────────
            int totalUsers = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM users");

            TotalUsersLabel.Text = totalUsers.ToString();
            UsersTodayLabel.Text = "+0 today";

            // ── Recent reports (latest 10) ─────────────────────────────────
            var reports = new ObservableCollection<ReportItem>();

            const string sql = """
                SELECT item_name, description, report_type, date_reported
                FROM item_reports
                ORDER BY date_reported DESC, report_id DESC
                LIMIT 10
            """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var item = new ReportItem
                {
                    Name = reader.GetString("item_name"),
                    Description = reader.IsDBNull(reader.GetOrdinal("description"))
                        ? ""
                        : reader.GetString("description"),
                    RType = CapitalizeFirst(reader.GetString("report_type")),
                    TimeAgo = FormatTimeAgo(reader.GetDateTime("date_reported"))
                };

                reports.Add(item);
            }

            RecentReportsCollection.ItemsSource = reports;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load dashboard data:\n{ex.Message}", "OK");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static async Task<int> GetScalarAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }

    private static string FormatTimeAgo(DateTime date)
    {
        var diff = DateTime.Today - date.Date;
        return diff.TotalDays switch
        {
            0 => "Today",
            1 => "Yesterday",
            <= 7 => $"{(int)diff.TotalDays} days ago",
            <= 30 => $"{(int)(diff.TotalDays / 7)} weeks ago",
            _ => date.ToString("MMM d, yyyy")
        };
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private async void OnReportsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new SurrenderedItemPage());
    }

    private async void OnVerifyTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new LostItemDetailPage());
    }

    private async void OnReturnedTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ReturnedItemsPage());
    }
}