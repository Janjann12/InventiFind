using MySqlConnector;
using System.Collections.ObjectModel;

namespace InventiFind;

public partial class NotificationModule : ContentPage
{
    public ObservableCollection<ReportItem> Reports { get; set; } = new();



    public NotificationModule()
    {
        InitializeComponent();
        ReportCollection.ItemsSource = Reports;
        LoadReports();
    }


    private async Task LoadReports()
    {
        try
        {
            int currentUserId = Preferences.Get("UserID", 0);

            using (var conn = new MySqlConnection(DatabaseConfig.ConnectionString))
            {
                await conn.OpenAsync();
                string query = @"
SELECT 
    r.title,
    i.description,
    i.date,
    i.r_type,
    u.FirstName,
    u.Surname
FROM reports r
JOIN items i ON r.I_id = i.L_ID
JOIN users u ON r.user_id = u.UserID
WHERE r.user_id != @currentUser";



                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@currentUser", currentUserId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        Reports.Clear();

                        while (await reader.ReadAsync())
                        {
                            Reports.Add(new ReportItem
                            {
                                Title = reader["title"].ToString(),
                                Description = reader["description"].ToString(),
                                Date = Convert.ToDateTime(reader["date"]).ToString("yyyy-MM-dd"),
                                Author = $"{reader["FirstName"]} {reader["Surname"]}",
                                RType = reader["r_type"].ToString()
                            });


                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

public class ReportItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string Author { get; set; }

        public string RType { get; set; }
    }


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

    private async void OnNewsTapped(object sender, TappedEventArgs e)
    {
        // Already on News/NotificationModule page
    }

    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");
        if (confirm)
        {
            await Shell.Current.GoToAsync("//MainPage");

        }
    }


}