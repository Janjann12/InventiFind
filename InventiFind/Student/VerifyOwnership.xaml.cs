using MySqlConnector;
using Microsoft.Maui.Storage;
using System.IO;

namespace InventiFind;

public partial class VerifyOwnership : ContentPage
{
    private readonly ReceiveModule.MatchPair _pair;

    // ✅ Use byte[] instead of path
    private byte[]? _photoBytes;

    public VerifyOwnership(ReceiveModule.MatchPair pair)
    {
        InitializeComponent();
        _pair = pair;
        PopulateSummary();
    }

    public static class UserSession
    {
        public static int UserId { get; set; }
    }

    // ── Summary ───────────────────────────────────────────

    private void PopulateSummary()
    {
        ItemNameLabel.Text = _pair.ItemName;
        CategoryLabel.Text = _pair.Category;
        MatchIdLabel.Text = $"Match ID: {_pair.LostId} ⇌ {_pair.SurrenderedId}";
    }

    // ── Photo picker (UPDATED like ReportModule) ───────────

    private async void OnPickPhotoTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select proof image",
                FileTypes = FilePickerFileType.Images
            });

            if (result == null) return;

            // ✅ Convert to byte[]
            using (var stream = await result.OpenReadAsync())
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                _photoBytes = memoryStream.ToArray();
            }

            // ✅ Preview image
            PhotoPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_photoBytes));

            PhotoPickerBorder.IsVisible = false;
            PhotoPreviewBorder.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not pick photo:\n{ex.Message}", "OK");
        }
    }

    private void OnRemovePhotoClicked(object sender, EventArgs e)
    {
        _photoBytes = null;

        PhotoPreviewImage.Source = null;
        PhotoPreviewBorder.IsVisible = false;
        PhotoPickerBorder.IsVisible = true;
    }

    // ── Validation ────────────────────────────────────────

    private bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(ItemDescriptionEditor.Text))
        {
            error = "Please describe the item.";
            return false;
        }

        if (DateLostPicker.Date > DateTime.Today)
        {
            error = "Date lost cannot be in the future.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    // ── Submit (UPDATED to BLOB) ──────────────────────────

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (!Validate(out var error))
        {
            await DisplayAlert("Incomplete", error, "OK");
            return;
        }

        SubmitBtn.IsEnabled = false;
        SubmitBtn.Text = "Submitting…";

        try
        {
            await using var conn = new MySqlConnection(DatabaseConfig.ConnectionString);
            await conn.OpenAsync();

            const string sql = """
                INSERT INTO proofs
                    (match_id, user_id, item_description, date_lost, photo, submitted_at, status)
                VALUES
                    (@matchId, @userId, @itemDesc, @dateLost, @photo, NOW(), 'pending')
            """;

            await using var cmd = new MySqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@matchId", _pair.LostId);
            cmd.Parameters.AddWithValue("@userId", UserSession.UserId);
            cmd.Parameters.AddWithValue("@itemDesc", ItemDescriptionEditor.Text.Trim());
            cmd.Parameters.Add("@dateLost", MySqlDbType.Date).Value = DateLostPicker.Date;


            // ✅ MEDIUMBLOB insert
            cmd.Parameters.Add("@photo", MySqlDbType.MediumBlob).Value =
                (object?)_photoBytes ?? DBNull.Value;

            await cmd.ExecuteNonQueryAsync();

            await DisplayAlert("Submitted",
                "Your ownership verification has been submitted and is pending review.",
                "OK");

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not submit:\n{ex.Message}", "OK");
        }
        finally
        {
            SubmitBtn.IsEnabled = true;
            SubmitBtn.Text = "Submit Verification";
        }
    }

    // ── Back ──────────────────────────────────────────────

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Navigation.PopModalAsync();
}
