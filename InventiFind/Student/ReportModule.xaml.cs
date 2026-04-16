using Microsoft.Maui.Controls;
using MySqlConnector;
using System;
using System.Globalization;
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

                    // Convert to byte array for database
                    using (var stream = await result.OpenReadAsync())
                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        selectedFileBytes = memoryStream.ToArray();
                    }

                    // Update UI
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
            // Validation
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
            using (var connection = new MySqlConnection(DatabaseConfig.ConnectionString))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO items (name, category, description, location, date, image, r_type) 
                               VALUES (@name, @category, @description, @location, @date, @image, @r_type)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", ItemNameEntry.Text.Trim());
                    command.Parameters.AddWithValue("@category", CategoryPicker.SelectedItem.ToString().ToLower());
                    command.Parameters.AddWithValue("@description",
                        string.IsNullOrWhiteSpace(DescriptionEditor.Text) ? "" : DescriptionEditor.Text.Trim());
                    command.Parameters.AddWithValue("@location", LocationEntry.Text.Trim());
                    command.Parameters.AddWithValue("@date", DatePicker.Date);



                    command.Parameters.AddWithValue("@image", selectedFileBytes ?? new byte[0]);
                    command.Parameters.AddWithValue("@r_type", reportType);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
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

        // Navigation handlers...
        private async void OnHomeTapped(object sender, EventArgs e) => await Navigation.PushAsync(new StudentDashboard());
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