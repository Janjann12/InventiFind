using MySqlConnector;
using System.Collections.ObjectModel;
using System.Text;

namespace InventiFind;

public class ReturnedItem
{
    public int MatchId { get; set; }
    public int LostId { get; set; }
    public int SurrenderedId { get; set; }
    public string ItemName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public DateTime ResolvedAt { get; set; }
    public string VerifiedByName { get; set; } = "Unknown";
    public int Similarity { get; set; }

    public string OwnerLine => $"{OwnerName} • {OwnerId}";
    public string ResolvedDate => ResolvedAt.ToString("yyyy-MM-dd");

    public string ConditionLabel => "Resolved";
    public Color BadgeFg => Color.FromArgb("#0F6E56");
    public Color BadgeBg => Color.FromArgb("#E6F7F0");

    public bool IsFirst { get; set; }
    public Color DotColor => Colors.White;

    public string Notes => string.IsNullOrWhiteSpace(Description)
        ? $"Returned • {Similarity}% match confirmed."
        : Description;
}

public partial class ReturnedItemsPage : ContentPage
{
    public ReturnedItemsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReturnedItemsAsync();
    }

    // ── GENERATE REPORT (CSV) ─────────────────────────────────────────────

    private async void OnGenerateReportClicked(object sender, EventArgs e)
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            const string sql = """
                SELECT
                    i.item_name,
                    i.category,
                    CONCAT(u.FirstName, ' ', u.Surname) AS owner_name,
                    r.return_date,
                    IFNULL(CONCAT(v.FirstName, ' ', v.Surname), 'System') AS verified_by,
                    i.description
                FROM returns r
                JOIN matches m ON m.match_id = r.match_id
                JOIN item_reports i ON i.report_id = m.lost_report_id
                JOIN users u ON u.UserID = i.user_id
                LEFT JOIN users v ON v.UserID = r.released_by
                ORDER BY r.return_date DESC
            """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Item Name,Category,Owner,Return Date,Verified By,Description");

            while (await reader.ReadAsync())
            {
                string itemName = EscapeCsv(reader["item_name"]?.ToString());
                string category = EscapeCsv(reader["category"]?.ToString());
                string owner = EscapeCsv(reader["owner_name"]?.ToString());
                string returnDate = reader["return_date"] is DateTime dt
                    ? dt.ToString("yyyy-MM-dd HH:mm:ss")
                    : "";
                string verifiedBy = EscapeCsv(reader["verified_by"]?.ToString());
                string description = EscapeCsv(reader["description"]?.ToString());

                csv.AppendLine($"{itemName},{category},{owner},{returnDate},{verifiedBy},{description}");
            }

            string fileName = $"ReturnedItemsReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            await DisplayAlert("Report Generated",
                $"Report saved:\n{filePath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed:\n{ex.Message}", "OK");
        }
    }

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }

    // ── LOAD DATA ─────────────────────────────────────────────────────────

    private async Task LoadReturnedItemsAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ReturnedCollection.IsVisible = false;
        EmptyState.IsVisible = false;

        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // ✔ TOTAL RETURNS
            int totalReturned = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM returns");

            int pendingVerify = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM matches WHERE match_status = 'pending'");

            TotalReturnedLabel.Text = totalReturned.ToString();
            SubtitleLabel.Text = $"{totalReturned} returned item{(totalReturned == 1 ? "" : "s")}";
            VerifyBadgeLabel.Text = pendingVerify.ToString();
            VerifyBadge.IsVisible = pendingVerify > 0;

            // ✔ MAIN DATA
            const string sql = """
                SELECT
                    r.match_id,
                    m.lost_report_id,
                    m.found_report_id,
                    m.similarity_score,
                    r.return_date,
                    i.item_name,
                    i.category,
                    i.description,
                    CONCAT(u.FirstName, ' ', u.Surname) AS owner_name,
                    u.UserID AS owner_user_id,
                    IFNULL(CONCAT(v.FirstName, ' ', v.Surname), 'System') AS verified_by
                FROM returns r
                JOIN matches m ON m.match_id = r.match_id
                JOIN item_reports i ON i.report_id = m.lost_report_id
                JOIN users u ON u.UserID = i.user_id
                LEFT JOIN users v ON v.UserID = r.released_by
                ORDER BY r.return_date DESC
                LIMIT 50
            """;

            var items = new ObservableCollection<ReturnedItem>();
            bool first = true;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                items.Add(new ReturnedItem
                {
                    MatchId = reader.GetInt32("match_id"),
                    LostId = reader.GetInt32("lost_report_id"),
                    SurrenderedId = reader.GetInt32("found_report_id"),
                    Similarity = reader.GetInt32("similarity_score"),
                    ResolvedAt = reader.GetDateTime("return_date"),
                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),
                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                    OwnerName = reader.GetString("owner_name"),
                    OwnerId = reader.GetInt32("owner_user_id").ToString(),
                    VerifiedByName = reader.GetString("verified_by"),
                    IsFirst = first
                });

                first = false;
            }

            if (items.Count == 0)
            {
                EmptyState.IsVisible = true;
            }
            else
            {
                ReturnedCollection.ItemsSource = items;
                ReturnedCollection.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load:\n{ex.Message}", "OK");
            EmptyState.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnDashboardTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new AdminDashboard());
    }

    private async void OnReportsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new SurrenderedItemPage());
    }

    private async void OnVerifyTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new LostItemDetailPage());
    }

    // ── HELPERS ───────────────────────────────────────────────────────────

    private static async Task<int> GetScalarAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
}