using MySqlConnector;
using System.Collections.Generic;
using System.Linq;

namespace InventiFind;

public partial class Settings : ContentPage
{
    private readonly string connString =
        "server=localhost;database=inventifind2;uid=root;pwd=;";

    private int currentUserId;

    // PAGINATION
    private List<UserReport> allReports = new();
    private int currentPage = 1;
    private const int pageSize = 10;
    private int totalPages = 1;

    public Settings()
    {
        InitializeComponent();

        currentUserId = Preferences.Get("UserID", 0);

        if (currentUserId == 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Error", "No logged in user found.", "OK");
                await Shell.Current.GoToAsync("//MainPage");
            });
            return;
        }

        _ = LoadUserDataAsync();
    }

    private async Task LoadUserDataAsync()
    {
        try
        {
            using var conn = new MySqlConnection(connString);
            await conn.OpenAsync();

            string query = @"
                SELECT FirstName, Surname, email, role
                FROM users
                WHERE UserID = @UserID";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@UserID", currentUserId);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                string firstName = reader["FirstName"]?.ToString() ?? "";
                string surname = reader["Surname"]?.ToString() ?? "";
                string email = reader["email"]?.ToString() ?? "";
                string role = reader["role"]?.ToString() ?? "";

                FirstNameEntry.Text = firstName;
                LastNameEntry.Text = surname;
                EmailEntry.Text = email;

                FullNameLabel.Text = $"{firstName} {surname}".Trim();
                RoleLabel.Text = $"{Capitalize(role)} · Active";

                InitialsLabel.Text =
                    $"{firstName.FirstOrDefault()}{surname.FirstOrDefault()}".ToUpper();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Database Error", ex.Message, "OK");
        }
    }

    private async Task LoadReportsAsync()
    {
        try
        {
            using var conn = new MySqlConnection(connString);
            await conn.OpenAsync();

            string query = @"
                SELECT item_name,
                       report_type,
                       date_reported
                FROM item_reports
                WHERE user_id = @UserID
                ORDER BY date_reported DESC";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@UserID", currentUserId);

            using var reader = await cmd.ExecuteReaderAsync();

            allReports.Clear();

            while (await reader.ReadAsync())
            {
                allReports.Add(new UserReport
                {
                    ItemName = reader["item_name"]?.ToString(),
                    ReportType = reader["report_type"]?.ToString(),
                    DateReported = Convert.ToDateTime(reader["date_reported"])
                        .ToString("MMM dd, yyyy")
                });
            }

            totalPages = (int)Math.Ceiling((double)allReports.Count / pageSize);

            if (totalPages == 0)
                totalPages = 1;

            currentPage = 1;

            LoadCurrentPage();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void LoadCurrentPage()
    {
        var pagedReports = allReports
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ReportsCollection.ItemsSource = pagedReports;

        PaginationLabel.Text = BuildPaginationText();
    }

    private string BuildPaginationText()
    {
        if (totalPages <= 1)
            return "1";

        if (totalPages <= 5)
        {
            return string.Join(" ",
                Enumerable.Range(1, totalPages)
                .Select(i => i == currentPage ? $"[{i}]" : i.ToString()));
        }

        return $"{currentPage} ... {totalPages}";
    }

    private void OnPreviousPageClicked(object sender, EventArgs e)
    {
        if (currentPage > 1)
        {
            currentPage--;
            LoadCurrentPage();
        }
    }

    private void OnNextPageClicked(object sender, EventArgs e)
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            LoadCurrentPage();
        }
    }

    private async void OnReportsTapped(object sender, TappedEventArgs e)
    {
        ProfileTabContent.IsVisible = false;
        ReportsTabContent.IsVisible = true;

        await LoadReportsAsync();
    }

    private void OnProfileTapped(object sender, TappedEventArgs e)
    {
        ReportsTabContent.IsVisible = false;
        ProfileTabContent.IsVisible = true;
    }

    private async void OnSaveProfileClicked(object sender, EventArgs e)
    {
        try
        {
            using var conn = new MySqlConnection(connString);
            await conn.OpenAsync();

            string query = @"
                UPDATE users
                SET FirstName = @FirstName,
                    Surname = @Surname,
                    email = @Email
                WHERE UserID = @UserID";

            using var cmd = new MySqlCommand(query, conn);

            cmd.Parameters.AddWithValue("@FirstName", FirstNameEntry.Text?.Trim());
            cmd.Parameters.AddWithValue("@Surname", LastNameEntry.Text?.Trim());
            cmd.Parameters.AddWithValue("@Email", EmailEntry.Text?.Trim());
            cmd.Parameters.AddWithValue("@UserID", currentUserId);

            await cmd.ExecuteNonQueryAsync();

            await LoadUserDataAsync();

            await DisplayAlert("Success", "Profile updated successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnUpdatePasswordClicked(object sender, EventArgs e)
    {
        if (NewPasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            await DisplayAlert("Error", "Passwords do not match.", "OK");
            return;
        }

        try
        {
            using var conn = new MySqlConnection(connString);
            await conn.OpenAsync();

            string query = @"
                UPDATE users
                SET password = @Password
                WHERE UserID = @UserID";

            using var cmd = new MySqlCommand(query, conn);

            cmd.Parameters.AddWithValue("@Password", NewPasswordEntry.Text);
            cmd.Parameters.AddWithValue("@UserID", currentUserId);

            await cmd.ExecuteNonQueryAsync();

            await DisplayAlert("Success", "Password updated successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure?", "Yes", "No");

        if (!confirm) return;

        Preferences.Remove("UserID");
        Preferences.Remove("UserEmail");

        await Shell.Current.GoToAsync("//MainPage");
    }

    private string Capitalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return char.ToUpper(text[0]) + text.Substring(1).ToLower();
    }
}

public class UserReport
{
    public string ItemName { get; set; }
    public string ReportType { get; set; }
    public string DateReported { get; set; }
}