using System.Windows;
using System.Windows.Controls;

namespace LabManagement.Host;

public sealed class PairingKeyDialog : Window
{
    private readonly string _key;
    private readonly TextBlock _statusText;

    public PairingKeyDialog(string key, bool wasRotated = false)
    {
        _key = key;
        Title = wasRotated ? "Client Pairing Key Baru" : "Client Pairing Key";
        Width = 620;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = wasRotated
                ? "Key baru dibuat. Perbarui semua Client dengan key ini sebelum mereka dapat terhubung kembali."
                : "Gunakan key ini saat memasang Client pada PC di lab yang sama.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var keyBox = new TextBox
        {
            Text = key,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 52,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(keyBox);

        _statusText = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = System.Windows.Media.Brushes.Gray
        };
        panel.Children.Add(_statusText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var copyButton = new Button { Content = "Copy", MinWidth = 82, Margin = new Thickness(0, 0, 8, 0) };
        copyButton.Click += (_, _) => CopyKey();
        buttons.Children.Add(copyButton);
        var closeButton = new Button { Content = "Tutup", MinWidth = 82, IsDefault = true };
        closeButton.Click += (_, _) => Close();
        buttons.Children.Add(closeButton);
        panel.Children.Add(buttons);

        Content = panel;
        Loaded += (_, _) =>
        {
            keyBox.Focus();
            keyBox.SelectAll();
            CopyKey();
        };
    }

    private void CopyKey()
    {
        try
        {
            Clipboard.SetText(_key);
            _statusText.Text = "Key berhasil disalin ke Clipboard.";
        }
        catch (Exception)
        {
            _statusText.Text = "Key sudah siap dipakai. Jika belum bisa dipaste, tekan Ctrl+C untuk menyalin dari textbox.";
        }
    }
}
