using System.Windows;

namespace LabManagement.Host;

public partial class App : Application
{
    private readonly HostPasswordManager _passwordManager = new();

    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    public static HostPasswordManager PasswordManager =>
        ((App)Current)._passwordManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!AuthenticateStaff())
        {
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow(_passwordManager);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    private bool AuthenticateStaff()
    {
        if (_passwordManager.Status == PasswordConfigurationStatus.Invalid)
        {
            MessageBox.Show(
                "Konfigurasi password Host tidak dapat dibaca. " +
                "Hubungi administrator UPT Lab untuk memulihkannya.",
                "Deep Fry",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        if (_passwordManager.Status == PasswordConfigurationStatus.NotConfigured)
        {
            var setupDialog = new PasswordDialog(PasswordDialogMode.Setup);
            if (setupDialog.ShowDialog() != true)
                return false;

            _passwordManager.SetPassword(setupDialog.NewPassword!);
            return true;
        }

        while (true)
        {
            var signInDialog = new PasswordDialog(PasswordDialogMode.SignIn);
            if (signInDialog.ShowDialog() != true)
                return false;

            if (_passwordManager.VerifyPassword(signInDialog.NewPassword!))
                return true;

            MessageBox.Show(
                "Password tidak sesuai.",
                "Deep Fry",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
