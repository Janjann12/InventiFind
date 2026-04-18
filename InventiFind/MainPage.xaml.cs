using MySqlConnector;

namespace InventiFind;

public partial class MainPage : ContentPage
{
    private string detectedRole = "User";

    public MainPage()
    {
        InitializeComponent();
    }

    // ==========================================
    // ROLE DETECTION - Auto-detect from email
    // ==========================================
    private void OnEmailTextChanged(object sender, TextChangedEventArgs e)
    {
        string email = e.NewTextValue?.ToLower() ?? "";

        if (string.IsNullOrWhiteSpace(email))
        {
            RoleDisplayFrame.IsVisible = false;
            UpdateLoginButtonText("User");
            detectedRole = "User";  // Reset to default
            return;
        }

        // Detect role based on email pattern
        if (email.Contains("@admin.") || email.Contains(".admin@") || email.EndsWith("@ue.edu.admin"))
        {
            detectedRole = "Admin";           // ← ADD THIS
            RoleDisplayFrame.IsVisible = true;
            // Optional: Update role display label text/color
        }
        else if (email.Contains("@teacher.") || email.Contains(".teacher@") || email.EndsWith("@ue.edu.teacher") || email.Contains("@prof."))
        {
            detectedRole = "teacher";         // ← ADD THIS
            RoleDisplayFrame.IsVisible = true;
        }
        else
        {
            detectedRole = "student";         // ← ADD THIS (or "User")
            RoleDisplayFrame.IsVisible = true;
        }

        UpdateLoginButtonText(detectedRole);
    }

    // ==========================================
    // UPDATE LOGIN BUTTON TEXT
    // ==========================================
    private void UpdateLoginButtonText(string role)
    {
        LoginButton.Text = $"Login as {role}";
    }

    // ==========================================
    // LOGIN BUTTON CLICKED
    // ==========================================
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string email = EmailEntry.Text;
        string password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Please enter both email and password", "OK");
            return;
        }

        // Get actual role from database
        var (isValid, userId, actualRole) = await ValidateCredentials(email, password);

        if (isValid && userId > 0 && !string.IsNullOrEmpty(actualRole))
        {
            Preferences.Set("UserID", userId);

            await DisplayAlert("Success", $"Welcome back! Logged in as {actualRole}", "OK");
            await NavigateBasedOnRole(actualRole);
        }
        else
        {
            await DisplayAlert("Login Failed", "Invalid email or password", "OK");
        }
    }

    // ==========================================
    // VALIDATE CREDENTIALS (TODO: Replace with API)
    // ==========================================
    // ==========================================
    // VALIDATE CREDENTIALS - Returns actual role from database
    // ==========================================
    private async Task<(bool success, int userId, string role)> ValidateCredentials(string email, string password)
    {
        try
        {
            using var connection = new MySqlConnection(DatabaseConfig.ConnectionString);
            await connection.OpenAsync();

            // Query to validate AND get the actual stored role
            string query = @"SELECT UserID, Role FROM users 
                        WHERE Email = @Email AND Password = @Password";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", email);
            command.Parameters.AddWithValue("@Password", password); // TODO: Use hashed passwords!

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                int userId = reader.GetInt32("UserID");
                string actualRole = reader.GetString("Role").ToLower();

                return (true, userId, actualRole);
            }

            return (false, 0, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
            return (false, 0, null);
        }
    }

    // ==========================================
    // NAVIGATE TO ROLE-SPECIFIC DASHBOARD
    // ==========================================
    private async Task NavigateBasedOnRole(string role)
    {
        // Normalize to handle case differences
        string normalizedRole = role?.ToLower();

        switch (normalizedRole)
        {
            case "admin":
                await Navigation.PushAsync(new AdminDashboard());
                break;
            case "teacher":      // lowercase to match detectedRole
                await Navigation.PushAsync(new TeacherDashboard());
                break;
            default:  // student or user
                await Navigation.PushAsync(new StudentDashboard());
                break;
        }
    }

    // ==========================================
    // GOOGLE SIGN IN TAP
    // ==========================================
    private async void OnGoogleSignInTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // TODO: Implement Google Authentication
            await DisplayAlert("Google Sign-In", "Opening Google authentication...", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Google sign-in failed: {ex.Message}", "OK");
        }
    }

    // ==========================================
    // FORGOT PASSWORD TAP
    // ==========================================
    private async void OnForgotPasswordTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Forgot Password",
            $"Password reset link will be sent to your email for {detectedRole} account.", "OK");
    }

    // ==========================================
    // SIGN UP TAP
    // ==========================================
    private async void OnSignUpTapped(object sender, TappedEventArgs e)
    {
        // TODO: Navigate to registration page
        await Navigation.PushAsync(new SignUpPage());
    }
}