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

    private async Task LoadProofsAsync()
    {
        try
        {
            MatchesStack.Children.Clear();

            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            const string sql = """
                SELECT
                    p.id AS proof_id,
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

            bool hasData = false;

            while (await reader.ReadAsync())
            {
                hasData = true;

                var proof = new ProofItem
                {
                    ProofId = reader.GetInt32("proof_id"),
                    MatchId = reader.GetInt32("match_id"),
                    LostReportId = reader.GetInt32("lost_report_id"),
                    FoundReportId = reader.GetInt32("found_report_id"),
                    ClaimantUserId = reader.GetInt32("user_id"),

                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),
                    ItemDescription = reader.IsDBNull(reader.GetOrdinal("item_description"))
                        ? ""
                        : reader.GetString("item_description"),
                    LostAt = reader.IsDBNull(reader.GetOrdinal("lost_at"))
                        ? ""
                        : reader.GetString("lost_at"),
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

            if (!hasData)
            {
                MatchesStack.Children.Add(BuildEmptyState());
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error loading proofs:\n{ex.Message}", "OK");
        }
    }

    private View BuildEmptyState()
    {
        return new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 18,
            Padding = 24,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E5E7EB"),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Image
                    {
                        Source = "notif.png",
                        WidthRequest = 52,
                        HeightRequest = 52,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "No pending verification",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#111827"),
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "Proof submissions will appear here.",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#6B7280"),
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            }
        };
    }

    private View BuildProofCard(ProofItem proof)
    {
        var itemNameLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(proof.ItemName) ? "Unnamed Item" : proof.ItemName,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111827"),
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalOptions = LayoutOptions.Center
        };

        var categoryFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#FDECEC"),
            CornerRadius = 10,
            Padding = new Thickness(8, 3),
            HasShadow = false,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = proof.Category,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#C8102E")
            }
        };

        Grid.SetColumn(categoryFrame, 1);

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Children =
            {
                itemNameLabel,
                categoryFrame
            }
        };

        var viewBtn = new Button
        {
            Text = "View Proof",
            BackgroundColor = Color.FromArgb("#C8102E"),
            TextColor = Colors.White,
            CornerRadius = 12,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };

        viewBtn.Clicked += async (s, e) =>
        {
            await OpenProofModal(proof);
        };

        return new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 18,
            Padding = 14,
            Margin = new Thickness(0, 0, 0, 10),
            HasShadow = false,
            BorderColor = Color.FromArgb("#E5E7EB"),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    headerGrid,
                    new Label
                    {
                        Text = $"Claimant: {proof.ClaimantName}",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#4B5563")
                    },
                    new Label
                    {
                        Text = $"Similarity: {proof.SimilarityScore}%",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#6B7280")
                    },
                    new Label
                    {
                        Text = $"Submitted: {proof.SubmittedAt}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#9CA3AF")
                    },
                    viewBtn
                }
            }
        };
    }

    private async Task OpenProofModal(ProofItem proof)
    {
        var photo = new Image
        {
            HeightRequest = 180,
            WidthRequest = 280,
            Aspect = Aspect.AspectFill,
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = Color.FromArgb("#E5E7EB")
        };

        if (proof.Photo != null && proof.Photo.Length > 0)
        {
            photo.Source = ImageSource.FromStream(() => new MemoryStream(proof.Photo));
        }

        var approveBtn = new Button
        {
            Text = "Approve",
            BackgroundColor = Color.FromArgb("#16A34A"),
            TextColor = Colors.White,
            CornerRadius = 12,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };

        var rejectBtn = new Button
        {
            Text = "Reject",
            BackgroundColor = Color.FromArgb("#DC2626"),
            TextColor = Colors.White,
            CornerRadius = 12,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };

        var closeBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E5E7EB"),
            TextColor = Color.FromArgb("#111827"),
            CornerRadius = 12,
            HeightRequest = 44
        };

        var actionGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10,
            Children =
            {
                rejectBtn,
                approveBtn
            }
        };

        Grid.SetColumn(approveBtn, 1);

        var modal = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = new Grid
            {
                Padding = 20,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        CornerRadius = 22,
                        Padding = 18,
                        HasShadow = true,
                        Content = new ScrollView
                        {
                            Content = new VerticalStackLayout
                            {
                                Spacing = 12,
                                Children =
                                {
                                    new Label
                                    {
                                        Text = "Proof Verification",
                                        FontSize = 22,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Color.FromArgb("#111827")
                                    },
                                    new Label
                                    {
                                        Text = proof.ItemName,
                                        FontSize = 16,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Color.FromArgb("#C8102E")
                                    },
                                    new Label { Text = $"Claimant: {proof.ClaimantName}", FontSize = 13, TextColor = Color.FromArgb("#4B5563") },
                                    new Label { Text = $"Category: {proof.Category}", FontSize = 13, TextColor = Color.FromArgb("#4B5563") },
                                    new Label { Text = $"Similarity: {proof.SimilarityScore}%", FontSize = 13, TextColor = Color.FromArgb("#4B5563") },
                                    new Label { Text = $"Lost At: {proof.LostAt}", FontSize = 13, TextColor = Color.FromArgb("#4B5563") },
                                    new Label { Text = $"Date Lost: {proof.DateLost}", FontSize = 13, TextColor = Color.FromArgb("#4B5563") },
                                    new Label { Text = $"Submitted: {proof.SubmittedAt}", FontSize = 13, TextColor = Color.FromArgb("#4B5563") },

                                    new Label
                                    {
                                        Text = "Description",
                                        FontSize = 14,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Color.FromArgb("#111827"),
                                        Margin = new Thickness(0, 8, 0, 0)
                                    },
                                    new Frame
                                    {
                                        BackgroundColor = Color.FromArgb("#F9FAFB"),
                                        BorderColor = Color.FromArgb("#E5E7EB"),
                                        CornerRadius = 14,
                                        Padding = 12,
                                        HasShadow = false,
                                        Content = new Label
                                        {
                                            Text = string.IsNullOrWhiteSpace(proof.ItemDescription)
                                                ? "No description provided."
                                                : proof.ItemDescription,
                                            FontSize = 13,
                                            TextColor = Color.FromArgb("#4B5563")
                                        }
                                    },

                                    new Label
                                    {
                                        Text = "Proof Image",
                                        FontSize = 14,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Color.FromArgb("#111827"),
                                        Margin = new Thickness(0, 8, 0, 0)
                                    },
                                    new Frame
                                    {
                                        BackgroundColor = Color.FromArgb("#F3F4F6"),
                                        CornerRadius = 16,
                                        Padding = 0,
                                        HasShadow = false,
                                        IsClippedToBounds = true,
                                        HorizontalOptions = LayoutOptions.Center,
                                        Content = photo
                                    },

                                    actionGrid,
                                    closeBtn
                                }
                            }
                        }
                    }
                }
            }
        };

        approveBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert("Approve Proof",
                "Are you sure you want to approve this claim?", "Yes", "No");

            if (!confirm) return;

            await UpdateProofStatus(proof, "approved");
            await Navigation.PopModalAsync();
            await LoadProofsAsync();
        };

        rejectBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert("Reject Proof",
                "Are you sure you want to reject this claim?", "Yes", "No");

            if (!confirm) return;

            await UpdateProofStatus(proof, "rejected");
            await Navigation.PopModalAsync();
            await LoadProofsAsync();
        };

        closeBtn.Clicked += async (s, e) =>
        {
            await Navigation.PopModalAsync();
        };

        await Navigation.PushModalAsync(modal);
    }

    private async Task UpdateProofStatus(ProofItem proof, string status)
    {
        await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
        await conn.OpenAsync();

        int adminId = Preferences.Get("UserID", 0);

        await new MySqlCommand(
            "UPDATE proofs SET status=@s WHERE id=@id", conn)
        {
            Parameters =
            {
                new("@s", status),
                new("@id", proof.ProofId)
            }
        }.ExecuteNonQueryAsync();

        if (status != "approved") return;

        await new MySqlCommand(
            "UPDATE matches SET match_status='confirmed', verified_by=@a WHERE match_id=@id", conn)
        {
            Parameters =
            {
                new("@a", adminId),
                new("@id", proof.MatchId)
            }
        }.ExecuteNonQueryAsync();

       

        await new MySqlCommand("""
            INSERT INTO returns (match_id, returned_to, released_by, return_date, notes)
            VALUES (@m,@to,@by,NOW(),@n)
        """, conn)
        {
            Parameters =
            {
                new("@m", proof.MatchId),
                new("@to", proof.ClaimantUserId),
                new("@by", adminId),
                new("@n", "Approved ownership proof")
            }
        }.ExecuteNonQueryAsync();

        await new MySqlCommand("""
            UPDATE proofs
            SET status='rejected'
            WHERE match_id=@m AND id<>@id AND status='pending'
        """, conn)
        {
            Parameters =
            {
                new("@m", proof.MatchId),
                new("@id", proof.ProofId)
            }
        }.ExecuteNonQueryAsync();
    }

    private async void OnDashboardTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new AdminDashboard());
    }

    private async void OnReportsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new SurrenderedItemPage());
    }

    private async void OnReturnedTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ReturnedItemsPage());
    }
    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");

        if (!confirm) return;

        Preferences.Remove("UserID");
        Preferences.Remove("UserEmail");

        await Shell.Current.GoToAsync("//MainPage");
    }
}