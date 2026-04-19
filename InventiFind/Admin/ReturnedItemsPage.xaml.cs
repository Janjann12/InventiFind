using MySqlConnector;
using System.Collections.ObjectModel;
using System.Text;

namespace InventiFind;

public class ReturnedItem
{
    // Raw data
    public int MatchId { get; set; }
    public int LostId { get; set; }
    public int SurrenderedId { get; set; }
    public string ItemName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string OwnerId { get; set; } = "";          // student/staff ID number
    public DateTime ResolvedAt { get; set; }
    public string VerifiedByName { get; set; } = "Unknown";
    public int Similarity { get; set; }

    // ?? Computed display properties ????????????????????????????????

    public string OwnerLine => $"{OwnerName} � {OwnerId}";

    public string ResolvedDate => ResolvedAt.ToString("yyyy-MM-dd");

    public string CategoryIcon => Category.ToLower() switch
    {
        "phone" => "??",
        "wallet" => "??",
        "id" => "??",
        "watch" => "?",
        _ => "??"
    };

    // Condition derived from similarity score
    public string ConditionLabel => "Resolved";
    public Color BadgeFg => Color.FromArgb("#0F6E56");
    public Color BadgeBg => Color.FromArgb("#E6F7F0");

    // First item in timeline gets filled dot
    public bool IsFirst { get; set; }
    public Color DotColor => IsFirst ? Color.FromArgb("#1A9E6E") : Colors.White;

    public string Notes => string.IsNullOrWhiteSpace(Description)
        ? $"Returned � {Similarity}% match confirmed."
        : Description;

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}
public partial class ReturnedItemsPage : ContentPage
{
	public ReturnedItemsPage()
	{
		InitializeComponent();
	}
    private async void OnGenerateReportClicked(object sender, EventArgs e)
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            const string sql = """
            SELECT
                i.name AS item_name,
                i.category,
                CONCAT(u.FirstName, ' ', u.Surname) AS owner_name,
                i.status,
                m.created_at AS resolved_date,
                IFNULL(CONCAT(v.FirstName, ' ', v.Surname), 'System') AS verified_by,
                i.description
            FROM matches m
            JOIN items i ON i.L_ID = m.lost_id
            JOIN users u ON u.UserID = i.UserID
            LEFT JOIN users v ON v.UserID = m.verified_by
            WHERE i.status = 'resolved'
            ORDER BY m.created_at DESC
        """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Item Name,Category,Owner,Status,Resolved Date,Verified By,Description");

            while (await reader.ReadAsync())
            {
                string itemName = EscapeCsv(reader["item_name"]?.ToString());
                string category = EscapeCsv(reader["category"]?.ToString());
                string owner = EscapeCsv(reader["owner_name"]?.ToString());
                string status = EscapeCsv(reader["status"]?.ToString());
                string resolvedDate = reader["resolved_date"] is DateTime dt
                    ? dt.ToString("yyyy-MM-dd HH:mm:ss")
                    : "";
                string verifiedBy = EscapeCsv(reader["verified_by"]?.ToString());
                string description = EscapeCsv(reader["description"]?.ToString());

                csv.AppendLine($"{itemName},{category},{owner},{status},{resolvedDate},{verifiedBy},{description}");
            }

            string fileName = $"ReturnedItemsReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            await DisplayAlert("Report Generated",
                $"Report saved successfully:\n{filePath}",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to generate report:\n{ex.Message}", "OK");
        }
    }

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReturnedItemsAsync();
    }

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

            // ── Summary stats ──────────────────────────────────────────────

            int totalResolved = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM matches WHERE status = 'resolved'");

            // "Good condition" = similarity >= 70
            int goodCondition = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM matches WHERE status = 'resolved' AND similarity >= 70");

            // Pending verifications for the badge
            int pendingVerify = await GetScalarAsync(conn,
                "SELECT COUNT(*) FROM matches WHERE status = 'pending'");

            TotalReturnedLabel.Text = totalResolved.ToString();
            ThisMonthLabel.Text = DateTime.Now.ToString("MMM");
            SubtitleLabel.Text = $"{totalResolved} resolved match{(totalResolved == 1 ? "" : "es")}";

            VerifyBadgeLabel.Text = pendingVerify.ToString();
            VerifyBadge.IsVisible = pendingVerify > 0;

            // ── Resolved match rows ────────────────────────────────────────
            // Join matches → lost item → owner → verifier
            const string sql = """
                    SELECT
                        m.match_id,
                        m.lost_id,
                        m.surrendered_id,
                        m.similarity,
                        m.created_at,
                        i.name        AS item_name,
                        i.category,
                        i.description,
                        CONCAT(u.FirstName, ' ', u.Surname)  AS owner_name,
                        u.UserID      AS owner_user_id,
                        IFNULL(CONCAT(v.FirstName, ' ', v.Surname), 'System') AS verified_by
                    FROM matches m
                    JOIN items  i ON i.L_ID    = m.lost_id
                    JOIN users  u ON u.UserID  = i.UserID
                    LEFT JOIN users v ON v.UserID = m.verified_by
                    WHERE i.status = 'resolved' OR
                    m.status = 'resolved'
                    ORDER BY m.created_at DESC
                    LIMIT 50
                """;

            var items = new ObservableCollection<ReturnedItem>();
            bool first = true;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var ri = new ReturnedItem
                {
                    MatchId = reader.GetInt32("match_id"),
                    LostId = reader.GetInt32("lost_id"),
                    SurrenderedId = reader.GetInt32("surrendered_id"),
                    Similarity = reader.GetByte("similarity"),
                    ResolvedAt = reader.GetDateTime("created_at"),
                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),
                    Description = reader.GetString("description"),
                    OwnerName = reader.GetString("owner_name"),
                    OwnerId = reader.GetInt32("owner_user_id").ToString(),
                    VerifiedByName = reader.GetString("verified_by"),
                    IsFirst = first
                };
                items.Add(ri);
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
            await DisplayAlert("Error", $"Failed to load returned items:\n{ex.Message}", "OK");
            EmptyState.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<int> GetScalarAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
}