using MySqlConnector;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace InventiFind;

public partial class StudentDashboard : ContentPage
{
    public StudentDashboard()
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

            var reports = new ObservableCollection<ReportItem>();

            // ✅ UPDATED QUERY (new database)
            const string sql = """
            SELECT item_name, description, report_type, date_reported, image
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
                    // ✅ UPDATED COLUMN NAMES
                    Name = reader.GetString("item_name"),
                    Description = reader.GetString("description"),
                    RType = CapitalizeFirst(reader.GetString("report_type")),
                    TimeAgo = FormatTimeAgo(reader.GetDateTime("date_reported")),

                    ImageData = reader.IsDBNull(reader.GetOrdinal("image"))
                        ? null
                        : (byte[])reader["image"]
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

    private async void OnHomeTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new StudentDashboard());
    }

    private async void OnReportTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ReportModule());
    }

    private async void OnReceiveTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ReceiveModule());
    }

    private async void OnViewAllTapped(object sender, TappedEventArgs e)
    {
        // Optional: navigate to full reports page
    }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new NotificationModule());
    }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");
        if (confirm)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

    public class ReportItem : INotifyPropertyChanged
    {
        private byte[] _imageData;
        private ImageSource _reportImage;

        public string Name { get; set; }
        public string Description { get; set; }
        public string RType { get; set; }
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
                {
                    return ImageSource.FromStream(() => new MemoryStream(_imageData));
                }

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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}