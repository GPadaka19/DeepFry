using System.Windows;
using System.Windows.Controls;

namespace LabManagement.Host;

public enum PasswordDialogMode
{
    Setup,
    SignIn,
    Change
}

public sealed class PasswordDialog : Window
{
    private readonly PasswordDialogMode _mode;
    private readonly PasswordBox? _currentPasswordBox;
    private readonly PasswordBox _newPasswordBox;
    private readonly PasswordBox? _confirmPasswordBox;

    public PasswordDialog(PasswordDialogMode mode)
    {
        _mode = mode;
        Title = mode switch
        {
            PasswordDialogMode.Setup => "Buat Password Lab",
            PasswordDialogMode.Change => "Ganti Password Lab",
            _ => "Masuk Deep Fry"
        };
        Width = 390;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = mode switch
            {
                PasswordDialogMode.Setup =>
                    "Buat password untuk Host lab ini. Password tidak dapat dibaca kembali.",
                PasswordDialogMode.Change =>
                    "Masukkan password lama, lalu tentukan password baru.",
                _ => "Masukkan password staff UPT Lab untuk membuka control center."
            },
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        if (mode == PasswordDialogMode.Change)
        {
            panel.Children.Add(CreateLabel("Password saat ini"));
            _currentPasswordBox = CreatePasswordBox();
            panel.Children.Add(_currentPasswordBox);
        }

        _newPasswordBox = CreatePasswordBox();
        if (mode != PasswordDialogMode.SignIn)
        {
            panel.Children.Add(CreateLabel("Password baru (minimal 6 karakter)"));
            panel.Children.Add(_newPasswordBox);
            panel.Children.Add(CreateLabel("Konfirmasi password baru"));
            _confirmPasswordBox = CreatePasswordBox();
            panel.Children.Add(_confirmPasswordBox);
        }
        else
        {
            panel.Children.Add(CreateLabel("Password"));
            panel.Children.Add(_newPasswordBox);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancelButton = new Button
        {
            Content = "Batal",
            MinWidth = 82,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelButton.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(cancelButton);

        var submitButton = new Button
        {
            Content = mode == PasswordDialogMode.SignIn ? "Masuk" : "Simpan",
            MinWidth = 82,
            IsDefault = true
        };
        submitButton.Click += Submit_Click;
        buttons.Children.Add(submitButton);
        panel.Children.Add(buttons);

        Content = panel;
        Loaded += (_, _) => (_currentPasswordBox ?? _newPasswordBox).Focus();
    }

    public string? CurrentPassword { get; private set; }

    public string? NewPassword { get; private set; }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        CurrentPassword = _currentPasswordBox?.Password;
        NewPassword = _newPasswordBox.Password;

        if (_mode != PasswordDialogMode.SignIn)
        {
            if (NewPassword.Length < 6)
            {
                ShowValidation("Password baru harus memiliki minimal 6 karakter.");
                return;
            }

            if (!string.Equals(NewPassword, _confirmPasswordBox?.Password, StringComparison.Ordinal))
            {
                ShowValidation("Konfirmasi password belum sama.");
                return;
            }
        }

        if (!HasRequiredPassword(_mode, CurrentPassword, NewPassword))
        {
            ShowValidation("Password wajib diisi.");
            return;
        }

        DialogResult = true;
    }

    internal static bool HasRequiredPassword(
        PasswordDialogMode mode,
        string? currentPassword,
        string? newPassword) =>
        mode == PasswordDialogMode.Change
            ? !string.IsNullOrEmpty(currentPassword)
            : !string.IsNullOrEmpty(newPassword);

    private static TextBlock CreateLabel(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0, 8, 0, 4)
    };

    private static PasswordBox CreatePasswordBox() => new()
    {
        MinWidth = 300
    };

    private void ShowValidation(string message)
    {
        MessageBox.Show(message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
