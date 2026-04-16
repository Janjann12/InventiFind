
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
    private async void OnLostTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new LostitemsPage());
    }





    private async Task LoadDashboardDataAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            // ── Recent reports (latest 10) ─────────────────────────────────
            var reports = new ObservableCollection<ReportItem>();

            // Changed from image_data to image
            const string sql = """
            SELECT name, description, r_type, date, image
            FROM items
            ORDER BY date DESC, L_ID DESC
            LIMIT 10
        """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var item = new ReportItem
                {
                    Name = reader.GetString("name"),
                    Description = reader.GetString("description"),
                    RType = CapitalizeFirst(reader.GetString("r_type")),
                    TimeAgo = FormatTimeAgo(reader.GetDateTime("date")),

                    // Changed from image_data to image
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





    // View All tapped
    private async void OnViewAllTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("View All", "Show all items", "OK");
    }

    // Bottom navigation taps
    private async void OnReportTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ReportModule());
    }

    private async void OnReceiveTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ReceiveModule());

    }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new NotificationModule());

    }

    private async void OnLogoutClicked(object sender, EventArgs e)  // For Button.Clicked
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
        if (confirm)
        {
            await Navigation.PopToRootAsync();
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

        // Stores the raw byte array from database
        public byte[] ImageData
        {
            get => _imageData;
            set
            {
                _imageData = value;
                // Convert byte array to ImageSource when data is set
                ReportImage = value != null
                    ? ImageSource.FromStream(() => new MemoryStream(value))
                    : null;
                OnPropertyChanged(nameof(ImageData));
            }
        }

        // This is what the XAML binds to for the Image Source
        public ImageSource ReportImage
        {
            get
            {
                // Return actual image if data exists
                if (_imageData != null && _imageData.Length > 0)
                {
                    return ImageSource.FromStream(() => new MemoryStream(_imageData));
                }

                // Return embedded placeholder image
                return ImageSource.FromFile("noimg.png");
            }
            private set
            {
                _reportImage = value;
                OnPropertyChanged(nameof(ReportImage));
            }
        }

        // Add this property for visibility binding
        public bool HasImage => _imageData != null && _imageData.Length > 0;

        // Badge color based on report type
        public Color BadgeColor => RType?.ToLower() switch
        {
            "lost" => Color.FromArgb("#EF4444"),      // Red
            "found" => Color.FromArgb("#22C55E"),     // Green
            "claimed" => Color.FromArgb("#3B82F6"),   // Blue
            _ => Color.FromArgb("#6B7280")             // Gray default
        };

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}