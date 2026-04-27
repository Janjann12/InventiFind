using MySqlConnector;

namespace InventiFind;

public partial class ReturnModule : ContentPage
{
    private string _currentFilter = "open";
    private int _currentPage = 1;
    private int _pageSize = 5;
    private int _totalPages = 1;

    private List<MatchPair> _allRecords = new();

    public ReturnModule()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        UpdateFilterButtons();
        await LoadReturnsAsync();
    }

    // ===============================
    // LOAD DATA
    // ===============================
    private async Task LoadReturnsAsync()
    {
        try
        {
            await using var conn =
                new MySqlConnection(DatabaseConfig.ConnectionString);

            await conn.OpenAsync();

            string sql = _currentFilter switch
            {
                "returned" => """
                    SELECT
                        m.lost_report_id,
                        m.found_report_id,
                        L.item_name,
                        L.category,
                        CONCAT(U.FirstName, ' ', U.Surname) AS owner_name,
                        L.date_reported
                    FROM matches m
                    JOIN item_reports L
                        ON L.report_id = m.lost_report_id
                    LEFT JOIN users U
                        ON U.UserID = L.user_id
                    WHERE L.status = 'claimed'
                    ORDER BY m.created_at DESC
                """,

                "matched" => """
                    SELECT
                        m.lost_report_id,
                        m.found_report_id,
                        L.item_name,
                        L.category,
                        CONCAT(U.FirstName, ' ', U.Surname) AS owner_name,
                        L.date_reported
                    FROM matches m
                    JOIN item_reports L
                        ON L.report_id = m.lost_report_id
                    LEFT JOIN users U
                        ON U.UserID = L.user_id
                    WHERE m.match_status = 'confirmed'
                    ORDER BY m.created_at DESC
                """,

                // OPEN FILTER = WAITING APPROVAL ONLY
                _ => """
                    SELECT
                        m.lost_report_id,
                        m.found_report_id,
                        L.item_name,
                        L.category,
                        CONCAT(U.FirstName, ' ', U.Surname) AS owner_name,
                        L.date_reported
                    FROM matches m
                    JOIN item_reports L
                        ON L.report_id = m.lost_report_id
                    LEFT JOIN users U
                        ON U.UserID = L.user_id
                    WHERE m.match_status = 'confirmed'
                      AND L.status = 'wait'
                    ORDER BY m.created_at DESC
                """
            };

            _allRecords.Clear();

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _allRecords.Add(new MatchPair
                {
                    LostId = reader.GetInt32("lost_report_id"),
                    SurrenderedId = reader.GetInt32("found_report_id"),
                    ItemName = reader.GetString("item_name"),
                    Category = reader.GetString("category"),
                    ReporterName = reader.IsDBNull(reader.GetOrdinal("owner_name"))
                        ? "Unknown"
                        : reader.GetString("owner_name"),
                    SubmittedDate = reader.GetDateTime("date_reported")
                        .ToString("yyyy-MM-dd"),
                    LostReportNo = reader.GetInt32("lost_report_id")
                        .ToString("D10")
                });
            }

            _totalPages = (int)Math.Ceiling(
                (double)_allRecords.Count / _pageSize);

            if (_totalPages == 0)
                _totalPages = 1;

            if (_currentPage > _totalPages)
                _currentPage = _totalPages;

            ShowCurrentPage();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ===============================
    // SHOW CURRENT PAGE
    // ===============================
    private void ShowCurrentPage()
    {
        ReturnCardsStack.Children.Clear();

        var pageData = _allRecords
            .Skip((_currentPage - 1) * _pageSize)
            .Take(_pageSize)
            .ToList();

        foreach (var pair in pageData)
        {
            ReturnCardsStack.Children.Add(BuildReturnCard(pair));
        }

        PendingCountLabel.Text = _allRecords.Count.ToString();

        StatusTextLabel.Text = _currentFilter switch
        {
            "returned" => "Returned items",
            "matched" => "Matched items",
            _ => "Items waiting for approval"
        };

        BuildPagination();
    }

    // ===============================
    // BUILD PAGINATION
    // ===============================
    private void BuildPagination()
    {
        PaginationLayout.Children.Clear();

        if (_allRecords.Count <= _pageSize)
        {
            PaginationLayout.IsVisible = false;
            return;
        }

        PaginationLayout.IsVisible = true;

        AddPageButton("1", 1, _currentPage == 1);

        if (_currentPage > 3)
        {
            PaginationLayout.Children.Add(new Label
            {
                Text = ". .",
                VerticalOptions = LayoutOptions.Center,
                FontSize = 14,
                TextColor = Color.FromArgb("#666")
            });
        }

        if (_currentPage != 1 && _currentPage != _totalPages)
        {
            AddPageButton(
                _currentPage.ToString(),
                _currentPage,
                true);
        }

        if (_currentPage < _totalPages - 2)
        {
            PaginationLayout.Children.Add(new Label
            {
                Text = ". .",
                VerticalOptions = LayoutOptions.Center,
                FontSize = 14,
                TextColor = Color.FromArgb("#666")
            });
        }

        if (_totalPages > 1)
        {
            AddPageButton(
                _totalPages.ToString(),
                _totalPages,
                _currentPage == _totalPages);
        }
    }

    private void AddPageButton(string text, int page, bool active = false)
    {
        var btn = new Button
        {
            Text = text,
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 20,
            FontSize = 14,
            BackgroundColor = active
                ? Color.FromArgb("#B71C1C")
                : Color.FromArgb("#E0E0E0"),
            TextColor = active ? Colors.White : Colors.Black
        };

        btn.Clicked += (s, e) =>
        {
            _currentPage = page;
            ShowCurrentPage();
        };

        PaginationLayout.Children.Add(btn);
    }

    // ===============================
    // RELEASE ITEM
    // ===============================
    private async Task ReleaseToOwnerAsync(MatchPair pair)
    {
        bool confirm = await DisplayAlert(
            "Release Item",
            $"Release {pair.ItemName} to owner?",
            "Release",
            "Cancel");

        if (!confirm)
            return;

        try
        {
            await using var conn =
                new MySqlConnection(DatabaseConfig.ConnectionString);

            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(
                @"UPDATE item_reports
                  SET status = 'released'
                  WHERE report_id IN (@lostId, @foundId)", conn);

            cmd.Parameters.AddWithValue("@lostId", pair.LostId);
            cmd.Parameters.AddWithValue("@foundId", pair.SurrenderedId);

            await cmd.ExecuteNonQueryAsync();

            await DisplayAlert(
                "Released",
                $"{pair.ItemName} is now ready for claiming.",
                "OK");

            await LoadReturnsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ===============================
    // CARD UI
    // ===============================
    private View BuildReturnCard(MatchPair pair)
    {
        var releaseButton = new Frame
        {
            BackgroundColor = Color.FromArgb("#C62828"),
            CornerRadius = 20,
            Padding = new Thickness(15, 12),
            HasShadow = false,
            Margin = new Thickness(0, 10, 0, 0),
            Content = new Label
            {
                Text = "Release to Owner",
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold
            }
        };

        releaseButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                await ReleaseToOwnerAsync(pair);
            })
        });

        return new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 15,
            Padding = 20,
            HasShadow = true,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = pair.ItemName,
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    new Label
                    {
                        Text = $"Owner: {pair.ReporterName}",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#555")
                    },
                    new Label
                    {
                        Text = $"Category: {pair.Category}",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#555")
                    },
                    new Label
                    {
                        Text = $"Claim Code: {pair.LostReportNo}",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#B71C1C")
                    },
                    new Label
                    {
                        Text = $"Verified: {pair.SubmittedDate}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#777")
                    },
                    releaseButton
                }
            }
        };
    }

    // ===============================
    // FILTER BUTTONS
    // ===============================
    private async void OnOpenFilterClicked(object sender, EventArgs e)
    {
        _currentFilter = "open";
        _currentPage = 1;

        UpdateFilterButtons();
        await LoadReturnsAsync();
    }

    private async void OnReturnedFilterClicked(object sender, EventArgs e)
    {
        _currentFilter = "returned";
        _currentPage = 1;

        UpdateFilterButtons();
        await LoadReturnsAsync();
    }

    private async void OnMatchedFilterClicked(object sender, EventArgs e)
    {
        _currentFilter = "matched";
        _currentPage = 1;

        UpdateFilterButtons();
        await LoadReturnsAsync();
    }

    private void UpdateFilterButtons()
    {
        OpenFilterBtn.BackgroundColor =
            _currentFilter == "open"
            ? Color.FromArgb("#B71C1C")
            : Color.FromArgb("#E0E0E0");

        ReturnedFilterBtn.BackgroundColor =
            _currentFilter == "returned"
            ? Color.FromArgb("#B71C1C")
            : Color.FromArgb("#E0E0E0");

        MatchedFilterBtn.BackgroundColor =
            _currentFilter == "matched"
            ? Color.FromArgb("#B71C1C")
            : Color.FromArgb("#E0E0E0");

        OpenFilterBtn.TextColor =
            _currentFilter == "open"
            ? Colors.White
            : Colors.Black;

        ReturnedFilterBtn.TextColor =
            _currentFilter == "returned"
            ? Colors.White
            : Colors.Black;

        MatchedFilterBtn.TextColor =
            _currentFilter == "matched"
            ? Colors.White
            : Colors.Black;
    }

    // ===============================
    // NAVIGATION
    // ===============================
    private async void OnHomeTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new TeacherDashboard());
    }

    private async void OnReportsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ReportsModule());
    }

    private async void OnReportItemTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new FileReport());
    }

    private async void OnSettingsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new Settings());
    }
}