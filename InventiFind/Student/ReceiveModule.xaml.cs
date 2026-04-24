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
                    CONCAT(U.FirstName, ' ', U.Surname) AS reporter_name
                FROM matches m
                JOIN item_reports L ON L.report_id = m.lost_report_id
                LEFT JOIN users U ON U.UserID = L.user_id
                ORDER BY m.created_at DESC
                LIMIT 20
            """;

            MatchesStack.Children.Clear();

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            bool hasRows = false;

            while (await reader.ReadAsync())
            {
                hasRows = true;

                var pair = new MatchPair
                {
                    LostId = reader.GetInt32("lost_report_id"),
                    SurrenderedId = reader.GetInt32("found_report_id"),
                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),
                    ReporterName = reader.IsDBNull(reader.GetOrdinal("reporter_name"))
                                        ? "Unknown"
                                        : reader.GetString("reporter_name"),
                    SubmittedDate = reader.GetDateTime("date_reported").ToString("yyyy-MM-dd"),
                    OwnershipProof = reader.IsDBNull(reader.GetOrdinal("ownership_proof"))
                                        ? ""
                                        : reader.GetString("ownership_proof"),
                    SimilarityScore = reader.GetInt32("score"),
                    LostReportNo = reader.GetInt32("lost_report_id").ToString("D10"),
                    SurrenderedNo = reader.GetInt32("found_report_id").ToString("D10"),
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
                    TextColor = Color.FromArgb("#999"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 60, 0, 0)
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load matches:\n{ex.Message}", "OK");
        }
    }

    private static string CapFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    // ── Card builder ──────────────────────────────────────────────────────
    // Copied verbatim from LostItemDetailPage — no changes needed.

    private View BuildMatchCard(MatchPair pair)
    {
        var badge = new Frame
        {
            BackgroundColor = pair.StatusBadgeColor,
            CornerRadius = 20,
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
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto })
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
        nameBlock.Add(new Label { Text = pair.ItemName, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1A1A1A") });
        nameBlock.Add(new Label { Text = pair.ReporterName, FontSize = 13, TextColor = Color.FromArgb("#555") });
        nameBlock.Add(new Label { Text = $"Submitted: {pair.SubmittedDate}", FontSize = 12, TextColor = Color.FromArgb("#888") });

        var scoreRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }),
            Margin = new Thickness(0, 10, 0, 0)
        };
        scoreRow.Add(scoreBox, 0, 0);
        scoreRow.Add(nameBlock, 1, 0);

        Frame MakeReportBox(string header, string value)
        {
            var f = new Frame
            {
                BackgroundColor = Color.FromArgb("#EEEEEE"),
                CornerRadius = 12,
                Padding = new Thickness(12, 10),
                HasShadow = false
            };
            f.Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = header, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#555") },
                    new Label { Text = value,  FontSize = 13, TextColor = Color.FromArgb("#1A1A1A") }
                }
            };
            return f;
        }

        var reportsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }),
            Margin = new Thickness(0, 10, 0, 0)
        };
        reportsRow.Add(MakeReportBox("Lost report", pair.LostReportNo), 0, 0);
        reportsRow.Add(new Label { Text = "⇌", FontSize = 20, TextColor = Color.FromArgb("#888"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }, 1, 0);
        reportsRow.Add(MakeReportBox("Found report", pair.SurrenderedNo), 2, 0);

        var proofBox = new Frame
        {
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            CornerRadius = 12,
            Padding = new Thickness(14, 12),
            HasShadow = false,
            Margin = new Thickness(0, 10, 0, 0)
        };
        proofBox.Content = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "Ownership proof", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#555") },
                new Label { Text = pair.OwnershipProof, FontSize = 13, TextColor = Color.FromArgb("#333") }
            }
        };

        var verifyBtn = new Frame
        {
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            CornerRadius = 12,
            Padding = new Thickness(0, 18),
            HasShadow = false,
            Margin = new Thickness(0, 10, 0, 0)
        };
        verifyBtn.Content = new Label
        {
            Text = "Verify Ownership",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1A1A1A"),
            HorizontalOptions = LayoutOptions.Center
        };
        var tapPair = pair;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            var detailPage = new VerifyDetailPage(tapPair);
            await Navigation.PushModalAsync(detailPage, animated: true);
            await LoadMatchesAsync();          // refresh after verify
        };
        verifyBtn.GestureRecognizers.Add(tap);

        return new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 16,
            Padding = new Thickness(16),
            HasShadow = false,
            Margin = new Thickness(0, 0, 0, 12),
            Content = new VerticalStackLayout
            {
                Children = { vidRow, scoreRow, reportsRow, proofBox, verifyBtn }
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
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");
        if (confirm)
            await Shell.Current.GoToAsync("//MainPage");
    }
}