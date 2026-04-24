using MySqlConnector;
using System.Windows.Input;

namespace InventiFind;

public partial class ReceiveModule : ContentPage
{
    public ICommand ContactCommand { get; }

    public ReceiveModule()
    {
        InitializeComponent();
        BindingContext = this;

        ContactCommand = new Command<MatchPair>(p =>
            DisplayAlert("Contact", $"Contacting about {p.ItemName}", "OK"));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMatchesAsync();
    }

    // ── Data ──────────────────────────────────────────────────────────────

    private async Task LoadMatchesAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            int currentUserId = Preferences.Get("UserID", 0);
            const string sql = """
    SELECT
        m.match_id,
        m.lost_report_id,
        m.found_report_id,
        m.similarity_score AS score,
        m.match_status,

        L.item_name,
        L.category,
        L.description AS ownership_proof,
        L.date_reported,
        L.user_id AS lost_user_id,

        F.user_id AS found_user_id,

        CONCAT(U.FirstName, ' ', U.Surname) AS reporter_name
    FROM matches m
    JOIN item_reports L ON L.report_id = m.lost_report_id
    JOIN item_reports F ON F.report_id = m.found_report_id
    LEFT JOIN users U ON U.UserID = L.user_id
    WHERE L.user_id = @currentUserId
       OR F.user_id = @currentUserId
    ORDER BY m.created_at DESC
    LIMIT 20
""";

            MatchesStack.Children.Clear();

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@currentUserId", currentUserId);

            await using var reader = await cmd.ExecuteReaderAsync();

            bool hasRows = false;

            while (await reader.ReadAsync())
            {
                hasRows = true;

                var pair = new MatchPair
                {
                    LostId = reader.GetInt32("lost_report_id"),
                    SurrenderedId = reader.GetInt32("found_report_id"),

                    LostUserId = reader.GetInt32("lost_user_id"),
                    FoundUserId = reader.GetInt32("found_user_id"),

                    IsFinder = reader.GetInt32("found_user_id") == currentUserId,

                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),

                    ReporterName = reader.IsDBNull(reader.GetOrdinal("reporter_name"))
                        ? "Unknown"
                        : reader.GetString("reporter_name"),

                    SubmittedDate = reader.GetDateTime("date_reported")
                        .ToString("yyyy-MM-dd"),

                    OwnershipProof = reader.IsDBNull(
                        reader.GetOrdinal("ownership_proof"))
                        ? ""
                        : reader.GetString("ownership_proof"),

                    SimilarityScore = reader.GetInt32("score"),

                    LostReportNo = reader.GetInt32("lost_report_id")
                        .ToString("D10"),

                    SurrenderedNo = reader.GetInt32("found_report_id")
                        .ToString("D10"),

                    Status = CapFirst(reader.GetString("match_status"))
                };

                MatchesStack.Children.Add(BuildMatchCard(pair));
            }

            if (!hasRows)
            {
                MatchesStack.Children.Add(new Label
                {
                    Text = "No matches found",
                    FontSize = 16,
                    TextColor = Color.FromArgb("#546E7A"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 60, 0, 0)
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error",
                $"Could not load matches:\n{ex.Message}", "OK");
        }
    }

    private static string CapFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    // ── Card builder ──────────────────────────────────────────────────────

    private View BuildMatchCard(MatchPair pair)
    {
        var badge = new Frame
        {
            BackgroundColor = pair.StatusBadgeColor,
            CornerRadius = 5,
            Padding = new Thickness(12, 5),
            HasShadow = false
        };

        badge.Content = new Label
        {
            Text = pair.Status,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };

        var vidRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        vidRow.Add(new Label
        {
            Text = "V-ID",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1A1A1A")
        }, 0, 0);

        vidRow.Add(badge, 1, 0);

        var scoreBox = new Frame
        {
            BackgroundColor = pair.ScoreColor,
            CornerRadius = 10,
            Padding = new Thickness(8),
            HasShadow = false,
            WidthRequest = 60,
            HeightRequest = 60
        };

        scoreBox.Content = new Label
        {
            Text = $"{pair.SimilarityScore}%",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var nameBlock = new VerticalStackLayout
        {
            Spacing = 2,
            Margin = new Thickness(10, 0, 0, 0)
        };

        nameBlock.Add(new Label
        {
            Text = pair.ItemName,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1A1A1A")
        });

        nameBlock.Add(new Label
        {
            Text = pair.ReporterName,
            FontSize = 13,
            TextColor = Color.FromArgb("#37474F")
        });

        nameBlock.Add(new Label
        {
            Text = $"Submitted: {pair.SubmittedDate}",
            FontSize = 12,
            TextColor = Color.FromArgb("#546E7A")
        });

        var scoreRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Margin = new Thickness(0, 10, 0, 0)
        };

        scoreRow.Add(scoreBox, 0, 0);
        scoreRow.Add(nameBlock, 1, 0);

        var lostBox = new Frame
        {
            BackgroundColor = Color.FromArgb("#F0F4F8"),
            CornerRadius = 12,
            Padding = new Thickness(12, 10),
            HasShadow = false
        };

        lostBox.Content = new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "Lost report",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#37474F")
                },
                new Label
                {
                    Text = pair.LostReportNo,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#1A1A1A")
                }
            }
        };

        var foundBox = new Frame
        {
            BackgroundColor = Color.FromArgb("#F0F4F8"),
            CornerRadius = 12,
            Padding = new Thickness(12, 10),
            HasShadow = false
        };

        foundBox.Content = new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "Found report",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#37474F")
                },
                new Label
                {
                    Text = pair.SurrenderedNo,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#1A1A1A")
                }
            }
        };

        var reportsRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Margin = new Thickness(0, 10, 0, 0)
        };

        reportsRow.Add(lostBox, 0, 0);

        reportsRow.Add(new Label
        {
            Text = "⇌",
            FontSize = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#546E7A")
        }, 1, 0);

        reportsRow.Add(foundBox, 2, 0);

        var proofBox = new Frame
        {
            BackgroundColor = Color.FromArgb("#F0F4F8"),
            CornerRadius = 12,
            Padding = new Thickness(14, 12),
            HasShadow = false,
            Margin = new Thickness(0, 10, 0, 0)
        };

        proofBox.Content = new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "Ownership proof",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#37474F")
                },
                new Label
                {
                    Text = pair.OwnershipProof,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#1A1A1A")
                }
            }
        };

        var verifyBtn = new Frame
        {
            BackgroundColor = pair.IsFinder
                ? Color.FromArgb("#546E7A")
                : Color.FromArgb("#1565C0"),
            CornerRadius = 12,
            Padding = new Thickness(0, 18),
            HasShadow = false,
            Margin = new Thickness(0, 10, 0, 0)
        };

        verifyBtn.Content = new Label
        {
            Text = pair.IsFinder
                ? "You found someone's item"
                : "Verify Ownership",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        var tap = new TapGestureRecognizer();

        tap.Tapped += async (s, e) =>
        {
            if (pair.IsFinder)
            {
                await DisplayAlert(
                    "Item Found",
                    "You already reported finding this item.",
                    "OK");
                return;
            }

            await Navigation.PushModalAsync(
                new VerifyOwnership(pair), true);
        };

        verifyBtn.GestureRecognizers.Add(tap);

        return new Frame
        {
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            CornerRadius = 16,
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Children =
                {
                    vidRow,
                    scoreRow,
                    reportsRow,
                    proofBox,
                    verifyBtn
                }
            }
        };
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private async void OnHomeTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new StudentDashboard());

    private async void OnReportTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new ReportModule());

    private void OnReceiveTapped(object sender, TappedEventArgs e) { }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new NotificationModule());

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout",
            "Are you sure?", "Yes", "No");

        if (confirm)
            await Shell.Current.GoToAsync("//MainPage");
    }

    // ── INNER CLASS ───────────────────────────────────────────────────────

    public class MatchPair
    {
        public int LostId { get; set; }
        public int SurrenderedId { get; set; }

        public int LostUserId { get; set; }
        public int FoundUserId { get; set; }

        public bool IsFinder { get; set; }

        public string ItemName { get; set; }
        public string Category { get; set; }
        public string ReporterName { get; set; }
        public string SubmittedDate { get; set; }
        public string OwnershipProof { get; set; }

        public int SimilarityScore { get; set; }

        public string LostReportNo { get; set; }
        public string SurrenderedNo { get; set; }

        public string Status { get; set; }

        public Color StatusBadgeColor =>
            Status.ToLower() == "verified"
                ? Color.FromArgb("#2E7D32")
                : Color.FromArgb("#E65100");

        public Color ScoreColor =>
            SimilarityScore >= 80
                ? Color.FromArgb("#2E7D32")
                : SimilarityScore >= 50
                    ? Color.FromArgb("#F57F17")
                    : Color.FromArgb("#C62828");
    }
}