using System.Text.RegularExpressions;
using MySqlConnector;

namespace InventiFind;

public partial class SignUpPage : ContentPage
{
    public SignUpPage()
    {
        InitializeComponent();
    }

    private void OnEmailTextChanged(object sender, TextChangedEventArgs e)
    {
        string email = e.NewTextValue;
    }

    private void OnPasswordTextChanged(object sender, TextChangedEventArgs e)
    {
        string password = e.NewTextValue;
        UpdatePasswordRequirements(password);
        CheckPasswordsMatch();
    }

    private void OnConfirmPasswordTextChanged(object sender, TextChangedEventArgs e)
    {
        CheckPasswordsMatch();
    }

    private void UpdatePasswordRequirements(string password)
    {
        bool hasUppercase = Regex.IsMatch(password, @"[A-Z]");
        UpdateRequirementLabel(UppercaseCheck, hasUppercase);

        bool hasLowercase = Regex.IsMatch(password, @"[a-z]");
        UpdateRequirementLabel(LowercaseCheck, hasLowercase);

        bool hasNumber = Regex.IsMatch(password, @"[0-9]");
        UpdateRequirementLabel(NumberCheck, hasNumber);

        bool hasSpecialChar = Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':\""\\|,.<>\/?]");
        UpdateRequirementLabel(SpecialCharCheck, hasSpecialChar);

        bool hasLength = password.Length >= 8;
        LengthCheck.Text = hasLength ? "✓ At least 8 characters" : "✗ At least 8 characters";
        LengthCheck.TextColor = hasLength ? Colors.Green : Color.FromArgb("#DC143C");
    }

    private void UpdateRequirementLabel(Label label, bool isValid)
    {
        label.Text = isValid ? "✓" : "✗";
        label.TextColor = isValid ? Colors.Green : Color.FromArgb("#DC143C");
    }

    private void CheckPasswordsMatch()
    {
        string password = PasswordEntry.Text ?? "";
        string confirmPassword = ConfirmPasswordEntry.Text ?? "";

        if (string.IsNullOrEmpty(confirmPassword))
        {
            PasswordMatchLabel.IsVisible = false;
            ConfirmPasswordFrame.BorderColor = Color.FromArgb("#E0E0E0");
            return;
        }

        bool match = password == confirmPassword;
        PasswordMatchLabel.IsVisible = !match;
        ConfirmPasswordFrame.BorderColor = match ? Colors.Green : Color.FromArgb("#DC143C");
    }

    private bool IsPasswordValid()
    {
        string password = PasswordEntry.Text ?? "";

        bool hasUppercase = Regex.IsMatch(password, @"[A-Z]");
        bool hasLowercase = Regex.IsMatch(password, @"[a-z]");
        bool hasNumber = Regex.IsMatch(password, @"[0-9]");
        bool hasSpecialChar = Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':\""\\|,.<>\/?]");
        bool hasLength = password.Length >= 8;

        return hasUppercase && hasLowercase && hasNumber && hasSpecialChar && hasLength;
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FirstNameEntry.Text))
        {
            await DisplayAlert("Error", "Please enter your first name.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(SurnameEntry.Text))
        {
            await DisplayAlert("Error", "Please enter your surname.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(EmailEntry.Text))
        {
            await DisplayAlert("Error", "Please enter your email.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(ContactEntry.Text))
        {
            await DisplayAlert("Error", "Please enter your contact number.", "OK");
            return;
        }

        if (!IsPasswordValid())
        {
            await DisplayAlert("Error", "Please ensure your password meets all security requirements.", "OK");
            return;
        }

        if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            await DisplayAlert("Error", "Passwords do not match.", "OK");
            return;
        }

        var (emailExists, emailError) = await EmailExists(EmailEntry.Text);
        if (!string.IsNullOrEmpty(emailError))
        {
            await DisplayAlert("Database Error", $"Failed to check email: {emailError}", "OK");
            return;
        }

        if (emailExists)
        {
            await DisplayAlert("Error", "An account with this email already exists.", "OK");
            return;
        }

        var (success, saveError) = await SaveUserToDatabase();

        if (success)
        {
            await DisplayAlert("Success", "Account created successfully!", "OK");
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert("Error", $"Failed to create account: {saveError}", "OK");
        }
    }

    private async Task<(bool exists, string error)> EmailExists(string email)
    {
        try
        {
            using var connection = new MySqlConnection(DatabaseConfig.ConnectionString);
            await connection.OpenAsync();

            string query = "SELECT COUNT(*) FROM users WHERE Email = @Email";
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", email);

            var result = await command.ExecuteScalarAsync();
            return (Convert.ToInt32(result) > 0, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking email: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private async Task<(bool success, string error)> SaveUserToDatabase()
    {
        try
        {
            using var connection = new MySqlConnection(DatabaseConfig.ConnectionString);
            await connection.OpenAsync();

            string query = @"INSERT INTO users (FirstName, Surname, Email, Contact, Password, Role) 
                           VALUES (@FirstName, @Surname, @Email, @Contact, @Password, @Role)";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@FirstName", FirstNameEntry.Text.Trim());
            command.Parameters.AddWithValue("@Surname", SurnameEntry.Text.Trim());
            command.Parameters.AddWithValue("@Email", EmailEntry.Text.Trim());
            command.Parameters.AddWithValue("@Contact", ContactEntry.Text.Trim());
            command.Parameters.AddWithValue("@Password", PasswordEntry.Text);
            command.Parameters.AddWithValue("@Role", "student");

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return (rowsAffected > 0, null);
        }
        catch (MySqlException sqlEx)
        {
            System.Diagnostics.Debug.WriteLine($"SQL Error {sqlEx.Number}: {sqlEx.Message}");
            return (false, $"SQL Error {sqlEx.Number}: {sqlEx.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving user: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private async void OnGoogleSignUpTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Google Sign Up", "Google sign up clicked", "OK");
    }

    private async void OnSignInTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }
}