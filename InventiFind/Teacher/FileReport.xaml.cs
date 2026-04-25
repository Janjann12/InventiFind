using Microsoft.Maui.Storage;
using MySqlConnector;
using System;
using System.IO;

namespace InventiFind;

public partial class FileReport : ContentPage
{
    private string selectedFilePath;
    private byte[] selectedFileBytes;
    private string reportType = "lost";

    public FileReport()
    {
        InitializeComponent();
        DatePicker.Date = DateTime.Now;
    }

    private void OnLostTapped(object sender, EventArgs e)
    {
        reportType = "lost";
        LostButton.BackgroundColor = Color.FromArgb("#C8102E");
        LostButton.TextColor = Colors.White;

        FoundButton.BackgroundColor = Color.FromArgb("#E5E7EB");
        FoundButton.TextColor = Color.FromArgb("#111827");
    }

    private void OnFoundTapped(object sender, EventArgs e)
    {
        reportType = "found";
        FoundButton.BackgroundColor = Color.FromArgb("#16A34A");
        FoundButton.TextColor = Colors.White;

        LostButton.BackgroundColor = Color.FromArgb("#E5E7EB");
        LostButton.TextColor = Color.FromArgb("#111827");
    }



    private async void OnUploadTapped(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select an image",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                selectedFilePath = result.FullPath;

                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();

                await stream.CopyToAsync(memoryStream);
                selectedFileBytes = memoryStream.ToArray();

                // SHOW FILE NAME
                UploadedFileNameLabel.Text = $"Selected: {result.FileName}";
                UploadedFileNameLabel.TextColor = Color.FromArgb("#16A34A");
                UploadedFileNameLabel.FontAttributes = FontAttributes.Bold;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
    private async void OnSubmitTapped(object sender, EventArgs e)
    {
        try
        {
            using var conn = new MySqlConnection(
                "server=localhost;database=inventifind;uid=root;pwd=;");

            await conn.OpenAsync();

            int userId = Preferences.Get("UserID", 0);

            string sql = @"
                INSERT INTO item_reports
                (user_id, report_type, item_name, category, description, location, image, status, date_reported)
                VALUES
                (@userId, @reportType, @itemName, @category, @description, @location, @image, 'open', @dateReported)";

            using var cmd = new MySqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@reportType", reportType);
            cmd.Parameters.AddWithValue("@itemName", ItemNameEntry.Text);
            cmd.Parameters.AddWithValue("@category", CategoryPicker.SelectedItem?.ToString());
            cmd.Parameters.AddWithValue("@description", DescriptionEditor.Text);
            cmd.Parameters.AddWithValue("@location", LocationEntry.Text);
            cmd.Parameters.AddWithValue("@image", selectedFileBytes ?? Array.Empty<byte>());
            cmd.Parameters.AddWithValue("@dateReported", DatePicker.Date);

            await cmd.ExecuteNonQueryAsync();

            await DisplayAlert("Success", "Report submitted.", "OK");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnCancelTapped(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}