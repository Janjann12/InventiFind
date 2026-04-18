using MySqlConnector;
using System.Collections.ObjectModel;

namespace InventiFind;

public partial class TeacherDashboard : ContentPage
{
    private DashboardViewModel _viewModel = new DashboardViewModel();

    public TeacherDashboard()
    {
        InitializeComponent();
        BindingContext = _viewModel;
        _ = LoadAllDataAsync();
    }

    private async Task LoadAllDataAsync()
    {
        await LoadDashboardStatsAsync();
        await LoadReportsAsync();
    }
    private async Task LoadDashboardStatsAsync()
    {
        try
        {
            using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // Get total stats
            string statsQuery = @"
            SELECT 
                COUNT(*) AS total_reports,
                IFNULL(SUM(r_type = 'lost'), 0) AS lost_items,
                IFNULL(SUM(r_type = 'found'), 0) AS found_items
            FROM items";

            using var statsCmd = new MySqlCommand(statsQuery, conn);
            using var statsReader = await statsCmd.ExecuteReaderAsync();

            if (await statsReader.ReadAsync())
            {
                _viewModel.TotalReports = statsReader.GetInt32("total_reports");
                _viewModel.LostItems = statsReader.GetInt32("lost_items");
                _viewModel.FoundItems = statsReader.GetInt32("found_items");
            }
            await statsReader.CloseAsync();

            // Get category counts - adjust column name based on your schema
            // Option 1: If you have a 'category' column
            string categoryQuery = @"
            SELECT 
                IFNULL(SUM(category = 'phone' OR category = 'Phone'), 0) AS phone_count,
                IFNULL(SUM(category = 'wallet' OR category = 'Wallet'), 0) AS wallet_count,
                IFNULL(SUM(category = 'id' OR category = 'ID'), 0) AS id_count,
                IFNULL(SUM(category = 'watch' OR category = 'Watch'), 0) AS watch_count,
                IFNULL(SUM(category NOT IN ('phone', 'Phone', 'wallet', 'Wallet', 'id', 'ID', 'watch', 'Watch') OR category IS NULL), 0) AS others_count
            FROM items";

          

            using var catCmd = new MySqlCommand(categoryQuery, conn);
            using var catReader = await catCmd.ExecuteReaderAsync();

            if (await catReader.ReadAsync())
            {
                _viewModel.PhoneCount = catReader.GetInt32("phone_count");
                _viewModel.WalletCount = catReader.GetInt32("wallet_count");
                _viewModel.IdCount = catReader.GetInt32("id_count");
                _viewModel.WatchCount = catReader.GetInt32("watch_count");
                _viewModel.OthersCount = catReader.GetInt32("others_count");

                // Calculate max for progress bars
                int max = Math.Max(_viewModel.PhoneCount,
                          Math.Max(_viewModel.WalletCount,
                          Math.Max(_viewModel.IdCount,
                          Math.Max(_viewModel.WatchCount, _viewModel.OthersCount))));

                // If all are 0, set max to 1 to avoid division by zero
                _viewModel.MaxCategoryCount = max > 0 ? max : 1;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load stats: {ex.Message}", "OK");
        }
    }
    private async Task LoadReportsAsync()
    {
        try
        {
            using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            string query = "SELECT name, r_type, description, date, image FROM items ORDER BY date DESC";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            _viewModel.Reports.Clear();

            while (await reader.ReadAsync())
            {
                byte[] imageBytes = null;

                // Safely read BLOB data
                if (!reader.IsDBNull(reader.GetOrdinal("image")))
                {
                    imageBytes = (byte[])reader["image"];
                }

                _viewModel.Reports.Add(new ReportItem
                {
                    Title = reader["name"].ToString() ?? string.Empty,
                    Status = reader["r_type"].ToString() ?? string.Empty,
                    Description = reader["description"].ToString() ?? string.Empty,
                    CreatedAt = Convert.ToDateTime(reader["date"]),
                    ImageData = imageBytes  // This triggers the ImageSource conversion
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load reports: {ex.Message}", "OK");
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnDashboardTapped(object sender, TappedEventArgs e)
    {
        // Already on dashboard — optionally refresh
        _ = LoadAllDataAsync();
    }

    private async void OnReportsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ReportsModule());
    }

    private async void OnReturnTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ReturnModule());
    }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Log Out", "Are you sure you want to log out?", "Yes", "No");
        if (confirm)
        {
            await Navigation.PopToRootAsync();
        }
    }

    // ── Models ────────────────────────────────────────────────────────────────

    public class DashboardViewModel : BindableObject
    {
        private int _totalReports;
        private int _lostItems;
        private int _foundItems;

        private int _phoneCount;
        private int _walletCount;
        private int _idCount;
        private int _watchCount;
        private int _othersCount;
        private int _maxCategoryCount;

        public int TotalReports
        {
            get => _totalReports;
            set { _totalReports = value; OnPropertyChanged(); }
        }

        public int LostItems
        {
            get => _lostItems;
            set { _lostItems = value; OnPropertyChanged(); }
        }

        public int FoundItems
        {
            get => _foundItems;
            set { _foundItems = value; OnPropertyChanged(); }
        }

        public int PhoneCount
        {
            get => _phoneCount;
            set
            {
                _phoneCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PhoneProgress));
            }
        }
        public double PhoneProgress => MaxCategoryCount > 0 ? (double)PhoneCount / MaxCategoryCount : 0;

        // Wallet
        public int WalletCount
        {
            get => _walletCount;
            set
            {
                _walletCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WalletProgress));
            }
        }
        public double WalletProgress => MaxCategoryCount > 0 ? (double)WalletCount / MaxCategoryCount : 0;

        // ID
        public int IdCount
        {
            get => _idCount;
            set
            {
                _idCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IdProgress));
            }
        }
        public double IdProgress => MaxCategoryCount > 0 ? (double)IdCount / MaxCategoryCount : 0;

        // Watch
        public int WatchCount
        {
            get => _watchCount;
            set
            {
                _watchCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WatchProgress));
            }
        }
        public double WatchProgress => MaxCategoryCount > 0 ? (double)WatchCount / MaxCategoryCount : 0;

        // Others
        public int OthersCount
        {
            get => _othersCount;
            set
            {
                _othersCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OthersProgress));
            }
        }
        public double OthersProgress => MaxCategoryCount > 0 ? (double)OthersCount / MaxCategoryCount : 0;

        // Helper to calculate max for progress bars
        public int MaxCategoryCount
        {
            get => _maxCategoryCount;
            set
            {
                _maxCategoryCount = value;
                // Recalculate all progress values
                OnPropertyChanged(nameof(PhoneProgress));
                OnPropertyChanged(nameof(WalletProgress));
                OnPropertyChanged(nameof(IdProgress));
                OnPropertyChanged(nameof(WatchProgress));
                OnPropertyChanged(nameof(OthersProgress));
            }
        }



        public ObservableCollection<ReportItem> Reports { get; set; } = new();
    }

    public class ReportItem : BindableObject
    {
        private byte[] _imageData;
        private ImageSource _reportImage;

        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public byte[] ImageData
        {
            get => _imageData;
            set
            {
                _imageData = value;
                // Convert byte array to ImageSource when data is set
                ReportImage = value != null && value.Length > 0
                    ? ImageSource.FromStream(() => new MemoryStream(value))
                    : null;
                OnPropertyChanged(nameof(ImageData));
                OnPropertyChanged(nameof(ReportImage));
            }
        }

        public ImageSource ReportImage
        {
            get => _reportImage;
            set
            {
                _reportImage = value;
                OnPropertyChanged(nameof(ReportImage));
            }
        }
    }
}