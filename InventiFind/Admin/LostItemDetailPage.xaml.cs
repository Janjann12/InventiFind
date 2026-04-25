using MySqlConnector;
using System.IO;

namespace InventiFind;

public class ProofItem
{
    public int ProofId { get; set; }
    public int MatchId { get; set; }
    public int LostReportId { get; set; }
    public int FoundReportId { get; set; }
    public int ClaimantUserId { get; set; }

    public string ItemName { get; set; } = "";
    public string Category { get; set; } = "";
    public string ItemDescription { get; set; } = "";
    public string LostAt { get; set; } = "";
    public string DateLost { get; set; } = "";
    public string SubmittedAt { get; set; } = "";
    public string ClaimantName { get; set; } = "";
    public int SimilarityScore { get; set; }

    public byte[]? Photo { get; set; }
}

public partial class LostItemDetailPage : ContentPage
{
    public LostItemDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProofsAsync();
    }

    // 🔁 LOAD PROOFS
    private async Task LoadProofsAsync()
    {
        try
        {
            MatchesStack.Children.Clear();

            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            const string sql = """
                SELECT
                    p.id AS proof_id,   -- ✅ FIXED
                    p.match_id,
                    p.user_id,
                    p.item_description,
                    p.lost_at,
                    p.date_lost,
                    p.photo,
                    p.submitted_at,

                    m.lost_report_id,
                    m.found_report_id,
                    m.similarity_score,

                    L.item_name,
                    L.category,

                    CONCAT(U.FirstName, ' ', U.Surname) AS claimant_name
                FROM proofs p
                JOIN matches m ON m.match_id = p.match_id
                JOIN item_reports L ON L.report_id = m.lost_report_id
                LEFT JOIN users U ON U.UserID = p.user_id
                WHERE p.status = 'pending'
                ORDER BY p.submitted_at DESC
            """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var proof = new ProofItem
                {
                    ProofId = reader.GetInt32("proof_id"),
                    MatchId = reader.GetInt32("match_id"),
                    LostReportId = reader.GetInt32("lost_report_id"),
                    FoundReportId = reader.GetInt32("found_report_id"),
                    ClaimantUserId = reader.GetInt32("user_id"),

                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),
                    ItemDescription = reader.IsDBNull(reader.GetOrdinal("item_description")) ? "" : reader.GetString("item_description"),
                    LostAt = reader.IsDBNull(reader.GetOrdinal("lost_at")) ? "" : reader.GetString("lost_at"),
                    DateLost = reader.IsDBNull(reader.GetOrdinal("date_lost"))
                        ? ""
                        : reader.GetDateTime("date_lost").ToString("yyyy-MM-dd"),
                    SubmittedAt = reader.IsDBNull(reader.GetOrdinal("submitted_at"))
                        ? ""
                        : reader.GetDateTime("submitted_at").ToString("yyyy-MM-dd hh:mm tt"),
                    ClaimantName = reader.IsDBNull(reader.GetOrdinal("claimant_name"))
                        ? "Unknown"
                        : reader.GetString("claimant_name"),
                    SimilarityScore = reader.GetInt32("similarity_score"),
                    Photo = reader.IsDBNull(reader.GetOrdinal("photo"))
                        ? null
                        : (byte[])reader["photo"]
                };

                MatchesStack.Children.Add(BuildProofCard(proof));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error loading proofs:\n{ex.Message}", "OK");
        }
    }

    // 🎨 CARD
    private View BuildProofCard(ProofItem proof)
    {
        var photo = new Image
        {
            HeightRequest = 160,
            Aspect = Aspect.AspectFill,
            BackgroundColor = Color.FromArgb("#DDD")
        };

        if (proof.Photo != null && proof.Photo.Length > 0)
        {
            photo.Source = ImageSource.FromStream(() => new MemoryStream(proof.Photo));
        }

        var approveBtn = new Button
        {
            Text = "Approve",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White
        };

        var rejectBtn = new Button
        {
            Text = "Reject",
            BackgroundColor = Color.FromArgb("#FF6B6B"),
            TextColor = Colors.White
        };

        approveBtn.Clicked += async (s, e) =>
        {
            await UpdateProofStatus(proof, "approved");
            await LoadProofsAsync();
        };

        rejectBtn.Clicked += async (s, e) =>
        {
            await UpdateProofStatus(proof, "rejected");
            await LoadProofsAsync();
        };

        return new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 12,
            Margin = new Thickness(0, 0, 0, 10),
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = proof.ItemName, FontSize = 18, FontAttributes = FontAttributes.Bold },
                    new Label { Text = $"Claimant: {proof.ClaimantName}" },
                    new Label { Text = $"Similarity: {proof.SimilarityScore}%" },
                    new Label { Text = $"Description: {proof.ItemDescription}" },
                    new Label { Text = $"Lost At: {proof.LostAt}" },
                    new Label { Text = $"Date Lost: {proof.DateLost}" },
                    photo,
                    new HorizontalStackLayout { Children = { approveBtn, rejectBtn } }
                }
            }
        };
    }

    // 🔥 VERIFY LOGIC
    private async Task UpdateProofStatus(ProofItem proof, string status)
    {
        await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
        await conn.OpenAsync();

        int adminId = Preferences.Get("UserID", 0);

        // 1. Update proof
        await new MySqlCommand(
            "UPDATE proofs SET status=@s WHERE id=@id", conn) // ✅ FIXED
        {
            Parameters =
            {
                new("@s", status),
                new("@id", proof.ProofId)
            }
        }.ExecuteNonQueryAsync();

        if (status != "approved") return;

        // 2. Confirm match
        await new MySqlCommand(
            "UPDATE matches SET match_status='confirmed', verified_by=@a WHERE match_id=@id", conn)
        {
            Parameters =
            {
                new("@a", adminId),
                new("@id", proof.MatchId)
            }
        }.ExecuteNonQueryAsync();

        // 3. Mark reports claimed
        await new MySqlCommand(
            "UPDATE item_reports SET status='claimed' WHERE report_id IN (@l,@f)", conn)
        {
            Parameters =
            {
                new("@l", proof.LostReportId),
                new("@f", proof.FoundReportId)
            }
        }.ExecuteNonQueryAsync();

        // 4. Insert return
        await new MySqlCommand("""
            INSERT INTO returns (match_id, returned_to, released_by, return_date, notes)
            VALUES (@m,@to,@by,NOW(),@n)
        """, conn)
        {
            Parameters =
            {
                new("@m", proof.MatchId),
                new("@to", proof.ClaimantUserId), // ✅ FIXED
                new("@by", adminId),
                new("@n", "Approved ownership proof")
            }
        }.ExecuteNonQueryAsync();

        // 5. Reject other proofs
        await new MySqlCommand("""
            UPDATE proofs
            SET status='rejected'
            WHERE match_id=@m AND id<>@id AND status='pending'
        """, conn) // ✅ FIXED
        {
            Parameters =
            {
                new("@m", proof.MatchId),
                new("@id", proof.ProofId)
            }
        }.ExecuteNonQueryAsync();
    }
}