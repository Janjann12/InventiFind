using MySqlConnector;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace InventiFind;

public partial class StudentDashboard : ContentPage
{
    // ── State ─────────────────────────────────────────────────────────────
    private List<ReportItem> _allReports = new();
    private string _activeCategory = null;   // null = "All"
    private string _searchQuery = string.Empty;

    private int _currentPage = 1;
    private const int PageSize = 5;

    // Maps category name → (Frame, Label) so we can restyle them easily
    private Dictionary<string, (Frame frame, Label label)> _catControls;

    public StudentDashboard()
    {
        InitializeComponent();

        _catControls = new()
        {
            ["Phone"] = (CatPhoneFrame, CatPhoneLabel),
            ["Wallet"] = (CatWalletFrame, CatWalletLabel),
            ["ID"] = (CatIDFrame, CatIDLabel),
            ["Watch"] = (CatWatchFrame, CatWatchLabel),
            ["Others"] = (CatOthersFrame, CatOthersLabel),
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDashboardDataAsync();
    }

    // ── Data loading ──────────────────────────────────────────────────────

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            const string sql = """
                SELECT item_name, description, report_type, category, date_reported, image
                FROM item_reports
                ORDER BY date_reported DESC, report_id DESC
                LIMIT 100
                """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            _allReports.Clear();

            while (await reader.ReadAsync())
            {
                var item = new ReportItem
                {
                    Name = reader.GetString("item_name"),
                    Description = reader.GetString("description"),
                    RType = CapitalizeFirst(reader.GetString("report_type")),
                    Category = reader.IsDBNull(reader.GetOrdinal("category"))
                                    ? "Others"
                                    : CapitalizeFirst(reader.GetString("category")),
                    TimeAgo = FormatTimeAgo(reader.GetDateTime("date_reported")),
                    ImageData = reader.IsDBNull(reader.GetOrdinal("image"))
                                    ? null
                                    : (byte[])reader["image"]
                };

                _allReports.Add(item);
            }

            _currentPage = 1;
            ApplyFilterAndPage();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load dashboard data:\n{ex.Message}", "OK");
        }
    }

    // ── Search ────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = e.NewTextValue?.Trim() ?? string.Empty;
        _currentPage = 1;
        ApplyFilterAndPage();
    }

    // ── Category tap ──────────────────────────────────────────────────────

    private void OnCategoryTapped(object sender, TappedEventArgs e)
    {
        var tapped = e.Parameter?.ToString();

        // Toggle off if same category tapped again
        if (_activeCategory == tapped)
        {
            _activeCategory = null;
            SetCategoryStyle(tapped, active: false);
        }
        else
        {
            // Deactivate previous
            if (_activeCategory != null)
                SetCategoryStyle(_activeCategory, active: false);

            _activeCategory = tapped;
            SetCategoryStyle(tapped, active: true);
        }

        _currentPage = 1;
        ApplyFilterAndPage();
    }

    private void SetCategoryStyle(string category, bool active)
    {
        if (!_catControls.TryGetValue(category, out var pair)) return;

        if (active)
        {
            pair.frame.BackgroundColor = Color.FromArgb("#C8102E");
            pair.frame.BorderColor = Color.FromArgb("#C8102E");
            pair.label.TextColor = Color.FromArgb("#C8102E");
            pair.label.FontAttributes = FontAttributes.Bold;
        }
        else
        {
            pair.frame.BackgroundColor = Colors.White;
            pair.frame.BorderColor = Color.FromArgb("#E5E7EB");
            pair.label.TextColor = Color.FromArgb("#374151");
            pair.label.FontAttributes = FontAttributes.None;
        }
    }

    // ── Filter + page ─────────────────────────────────────────────────────

    private void ApplyFilterAndPage()
    {
        IEnumerable<ReportItem> filtered = _allReports;

        // Category filter
        if (_activeCategory != null)
            filtered = filtered.Where(r =>
                r.Category.Equals(_activeCategory, StringComparison.OrdinalIgnoreCase));

        // Search filter — matches item name or description
        if (!string.IsNullOrEmpty(_searchQuery))
            filtered = filtered.Where(r =>
                r.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();
        bool isEmpty = list.Count == 0;

        // Show/hide empty state
        EmptyState.IsVisible = isEmpty;
        RecentReportsCollection.IsVisible = !isEmpty;
        PageButtonsLayout.IsVisible = !isEmpty;

        if (isEmpty)
        {
            // Tailor the empty message to context
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                EmptyStateLabel.Text = $"No results for \"{_searchQuery}\"";
                EmptyStateSubLabel.Text = "Try a different keyword or category";
            }
            else
            {
                EmptyStateLabel.Text = "No items found";
                EmptyStateSubLabel.Text = "There are no posts in this category yet";
            }

            BuildPaginationButtons(0);
            return;
        }

        int totalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        _currentPage = Math.Clamp(_currentPage, 1, totalPages);

        var page = list
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        RecentReportsCollection.ItemsSource = new ObservableCollection<ReportItem>(page);
        BuildPaginationButtons(totalPages);
    }

    // ── Pagination ────────────────────────────────────────────────────────

    private void BuildPaginationButtons(int totalPages)
    {
        PageButtonsLayout.Children.Clear();

        if (totalPages <= 1) return;

        // Prev arrow
        PageButtonsLayout.Children.Add(MakePageButton("‹", _currentPage > 1, () =>
        {
            _currentPage--;
            ApplyFilterAndPage();
        }));

        // Page number buttons (show up to 5 around current)
        int start = Math.Max(1, _currentPage - 2);
        int end = Math.Min(totalPages, start + 4);
        start = Math.Max(1, end - 4);

        for (int p = start; p <= end; p++)
        {
            int captured = p;
            bool isCurrent = p == _currentPage;
            PageButtonsLayout.Children.Add(MakePageButton(
                p.ToString(),
                enabled: true,
                onTap: () =>
                {
                    _currentPage = captured;
                    ApplyFilterAndPage();
                },
                isActive: isCurrent));
        }

        // Next arrow
        PageButtonsLayout.Children.Add(MakePageButton("›", _currentPage < totalPages, () =>
        {
            _currentPage++;
            ApplyFilterAndPage();
        }));
    }

    private static Frame MakePageButton(
        string text,
        bool enabled,
        Action onTap,
        bool isActive = false)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 13,
            FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None,
            TextColor = isActive ? Colors.White
                              : enabled ? Color.FromArgb("#374151")
                                          : Color.FromArgb("#9CA3AF"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var frame = new Frame
        {
            BackgroundColor = isActive ? Color.FromArgb("#C8102E")
                            : enabled ? Colors.White
                                                : Color.FromArgb("#F3F4F6"),
            BorderColor = isActive ? Color.FromArgb("#C8102E")
                                                : Color.FromArgb("#E5E7EB"),
            CornerRadius = 8,
            Padding = new Thickness(10, 5),
            HasShadow = false,
            Content = label
        };

        if (enabled)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => onTap();
            frame.GestureRecognizers.Add(tap);
        }

        return frame;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

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

    // ── Navigation ────────────────────────────────────────────────────────

    private async void OnHomeTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new StudentDashboard());

    private async void OnReportTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new ReportModule());

    private async void OnReceiveTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new ReceiveModule());

    private async void OnViewAllTapped(object sender, TappedEventArgs e) { }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
        => await Navigation.PushModalAsync(new NotificationModule());

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");
        if (confirm)
            await Shell.Current.GoToAsync("//MainPage");
    }

    // ── Model ─────────────────────────────────────────────────────────────

    public class ReportItem : INotifyPropertyChanged
    {
        private byte[] _imageData;
        private ImageSource _reportImage;

        public string Name { get; set; }
        public string Description { get; set; }
        public string RType { get; set; }
        public string Category { get; set; }
        public string TimeAgo { get; set; }

        public byte[] ImageData
        {
            get => _imageData;
            set
            {
                _imageData = value;
                ReportImage = value != null
                    ? ImageSource.FromStream(() => new MemoryStream(value))
                    : null;
                OnPropertyChanged(nameof(ImageData));
            }
        }

        public ImageSource ReportImage
        {
            get
            {
                if (_imageData != null && _imageData.Length > 0)
                    return ImageSource.FromStream(() => new MemoryStream(_imageData));
                return ImageSource.FromFile("noimg.png");
            }
            private set
            {
                _reportImage = value;
                OnPropertyChanged(nameof(ReportImage));
            }
        }

        public bool HasImage => _imageData != null && _imageData.Length > 0;

        public Color BadgeColor => RType?.ToLower() switch
        {
            "lost" => Color.FromArgb("#EF4444"),
            "found" => Color.FromArgb("#22C55E"),
            "claimed" => Color.FromArgb("#3B82F6"),
            _ => Color.FromArgb("#6B7280")
        };

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}