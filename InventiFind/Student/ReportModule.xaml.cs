using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace InventiFind
{
    public partial class ReportModule : ContentPage
    {
        private string selectedFilePath = null;
        private byte[] selectedFileBytes = null;
        private string reportType = "lost"; // Default to lost

        public ReportModule()
        {
            InitializeComponent();
            DatePicker.Date = DateTime.Now;
            UpdateReportTypeUI();
        }

        // ========== REPORT TYPE TOGGLE ==========
        private void OnLostSomethingTapped(object sender, EventArgs e)
        {
            reportType = "lost";
            UpdateReportTypeUI();
        }

        private void OnFoundSomethingTapped(object sender, EventArgs e)
        {
            reportType = "found";
            UpdateReportTypeUI();
        }

        private async void OnViewAllTapped(object sender, TappedEventArgs e)
        {
            await DisplayAlert("View All", "Show all items", "OK");
        }

        // Bottom navigation taps
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

        private void UpdateReportTypeUI()
        {
            if (reportType == "lost")
            {
                // "I Lost Something" - SELECTED (Red theme)
                LostFrame.BackgroundColor = Color.FromArgb("#FFE4E1");
                LostFrame.BorderColor = Color.FromArgb("#B71C1C");
                ((Label)LostFrame.Content).TextColor = Color.FromArgb("#B71C1C");
                ((Label)LostFrame.Content).FontAttributes = FontAttributes.Bold;

                // "I Found Something" - UNSELECTED (Gray)
                FoundFrame.BackgroundColor = Colors.White;
                FoundFrame.BorderColor = Color.FromArgb("#CCCCCC");
                ((Label)FoundFrame.Content).TextColor = Color.FromArgb("#999999");
                ((Label)FoundFrame.Content).FontAttributes = FontAttributes.None;
            }
            else
            {
                // "I Found Something" - SELECTED (Green theme)
                FoundFrame.BackgroundColor = Color.FromArgb("#E8F5E9");
                FoundFrame.BorderColor = Color.FromArgb("#228B22");
                ((Label)FoundFrame.Content).TextColor = Color.FromArgb("#228B22");
                ((Label)FoundFrame.Content).FontAttributes = FontAttributes.Bold;

                // "I Lost Something" - UNSELECTED (Gray)
                LostFrame.BackgroundColor = Colors.White;
                LostFrame.BorderColor = Color.FromArgb("#CCCCCC");
                ((Label)LostFrame.Content).TextColor = Color.FromArgb("#999999");
                ((Label)LostFrame.Content).FontAttributes = FontAttributes.None;
            }
        }

        // ========== FILE UPLOAD ==========
        private async void OnUploadTapped(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an image or file",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    selectedFilePath = result.FullPath;

                    using (var stream = await result.OpenReadAsync())
                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        selectedFileBytes = memoryStream.ToArray();
                    }

                    UploadText.Text = result.FileName;
                    UploadSubtext.Text = "File selected - tap to change";
                    UploadIcon.Text = "📎";
                    UploadFrame.BorderColor = Colors.Green;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Upload failed: {ex.Message}", "OK");
            }
        }

        // ========== SUBMIT TO DATABASE ==========
        private async void OnSubmitTapped(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ItemNameEntry.Text))
            {
                await DisplayAlert("Validation", "Please enter an item name", "OK");
                return;
            }

            if (CategoryPicker.SelectedIndex == -1)
            {
                await DisplayAlert("Validation", "Please select a category", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(LocationEntry.Text))
            {
                await DisplayAlert("Validation", "Please enter a location", "OK");
                return;
            }

            try
            {
                bool success = await InsertReportToDatabase();

                if (success)
                {
                    await DisplayAlert("Success", "Report submitted successfully!", "OK");
                    ClearForm();
                }
                else
                {
                    await DisplayAlert("Error", "Failed to submit report", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Database Error", ex.Message, "OK");
            }
        }

        private async Task<bool> InsertReportToDatabase()
        {
            using var connection = new MySqlConnection(DatabaseConfig.ConnectionString);
            await connection.OpenAsync();

            int userId = Preferences.Get("UserID", 0);
            if (userId == 0)
            {
                await DisplayAlert("Error", "No logged-in user found.", "OK");
                return false;
            }

            // 1) Insert new report into item_reports
            string insertSql = @"
                INSERT INTO item_reports
                (user_id, report_type, item_name, category, description, location, image, status, date_reported)
                VALUES
                (@userId, @reportType, @itemName, @category, @description, @location, @image, 'open', @dateReported);
                SELECT LAST_INSERT_ID();";

            int newReportId;

            using (var insertCmd = new MySqlCommand(insertSql, connection))
            {
                insertCmd.Parameters.AddWithValue("@userId", userId);
                insertCmd.Parameters.AddWithValue("@reportType", reportType);
                insertCmd.Parameters.AddWithValue("@itemName", ItemNameEntry.Text.Trim());
                insertCmd.Parameters.AddWithValue("@category", CategoryPicker.SelectedItem.ToString().ToLower());
                insertCmd.Parameters.AddWithValue("@description",
                    string.IsNullOrWhiteSpace(DescriptionEditor.Text) ? "" : DescriptionEditor.Text.Trim());
                insertCmd.Parameters.AddWithValue("@location", LocationEntry.Text.Trim());
                insertCmd.Parameters.AddWithValue("@image", selectedFileBytes ?? Array.Empty<byte>());
                insertCmd.Parameters.AddWithValue("@dateReported", DatePicker.Date);

                var result = await insertCmd.ExecuteScalarAsync();
                newReportId = Convert.ToInt32(result);
            }

            if (newReportId == 0)
                return false;

            // 2) Build the newly inserted report as ItemData
            var newItem = new ItemData
            {
                Id = newReportId,
                Name = ItemNameEntry.Text.Trim(),
                Category = CategoryPicker.SelectedItem.ToString().ToLower(),
                Description = string.IsNullOrWhiteSpace(DescriptionEditor.Text) ? "" : DescriptionEditor.Text.Trim(),
                Location = LocationEntry.Text.Trim(),
                Date = DatePicker.Date ?? DateTime.Now
            };

            // 3) Get opposite-type candidate reports with same category
            string oppositeType = reportType == "lost" ? "found" : "lost";

            string candidateSql = @"
                SELECT report_id, item_name, category, description, location, date_reported
                FROM item_reports
                WHERE report_type = @oppositeType
                  AND category = @category
                  AND status = 'open'
                  AND report_id <> @newReportId";

            var candidates = new List<ItemData>();

            using (var candidateCmd = new MySqlCommand(candidateSql, connection))
            {
                candidateCmd.Parameters.AddWithValue("@oppositeType", oppositeType);
                candidateCmd.Parameters.AddWithValue("@category", CategoryPicker.SelectedItem.ToString().ToLower());
                candidateCmd.Parameters.AddWithValue("@newReportId", newReportId);

                using var reader = await candidateCmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    candidates.Add(new ItemData
                    {
                        Id = reader.GetInt32("report_id"),
                        Name = reader.GetString("item_name"),
                        Category = reader.GetString("category"),
                        Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                        Location = reader.IsDBNull(reader.GetOrdinal("location")) ? "" : reader.GetString("location"),
                        Date = reader.GetDateTime("date_reported")
                    });
                }
            }

            // 4) Use ItemMatcher to calculate similarity, then insert into matches
            foreach (var candidate in candidates)
            {
                ItemData lostItem;
                ItemData foundItem;

                if (reportType == "lost")
                {
                    lostItem = newItem;
                    foundItem = candidate;
                }
                else
                {
                    lostItem = candidate;
                    foundItem = newItem;
                }

                int similarity = ItemMatcher.CalculateSimilarity(lostItem, foundItem);

                // Optional threshold to ignore weak matches
                if (similarity < 40)
                    continue;

                string matchInsertSql = @"
                    INSERT INTO matches (lost_report_id, found_report_id, similarity_score, match_status, created_at)
                    VALUES (@lostReportId, @foundReportId, @similarityScore, 'pending', NOW())";

                using var matchCmd = new MySqlCommand(matchInsertSql, connection);
                matchCmd.Parameters.AddWithValue("@lostReportId", lostItem.Id);
                matchCmd.Parameters.AddWithValue("@foundReportId", foundItem.Id);
                matchCmd.Parameters.AddWithValue("@similarityScore", similarity);

                await matchCmd.ExecuteNonQueryAsync();
            }

            return true;
        }

        private void ClearForm()
        {
            ItemNameEntry.Text = string.Empty;
            CategoryPicker.SelectedIndex = -1;
            DescriptionEditor.Text = string.Empty;
            LocationEntry.Text = string.Empty;
            DatePicker.Date = DateTime.Now;
            selectedFilePath = null;
            selectedFileBytes = null;
            reportType = "lost";

            UploadText.Text = "Click to upload images or files";
            UploadSubtext.Text = "PNG, JPG, PDF up to 10mb";
            UploadIcon.Text = "☁️";
            UploadFrame.BorderColor = Color.FromArgb("#AAAAAA");

            UpdateReportTypeUI();
        }

        // Navigation handlers
        private async void OnHomeTapped(object sender, EventArgs e) => await Navigation.PushModalAsync(new StudentDashboard());
        private void OnReceiveTapped(object sender, EventArgs e) { }
        private void OnNewsTapped(object sender, EventArgs e) { }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            if (await DisplayAlert("Logout", "Are you sure?", "Yes", "No"))
            {
                // Navigate to login
            }
        }
    }
}