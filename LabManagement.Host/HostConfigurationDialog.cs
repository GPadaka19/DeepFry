using System.Windows;
using System.Windows.Controls;

namespace LabManagement.Host;

public sealed class HostConfigurationDialog : Window
{
    private readonly TextBox _labNameBox;
    private readonly TextBox _portBox;

    public HostConfigurationDialog(HostConfiguration configuration)
    {
        Title = "Lab Settings";
        Width = 390;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Nama lab" });
        _labNameBox = new TextBox { Text = configuration.LabName, Margin = new Thickness(0, 4, 0, 12) };
        panel.Children.Add(_labNameBox);
        panel.Children.Add(new TextBlock { Text = "TCP port" });
        _portBox = new TextBox { Text = configuration.TcpPort.ToString(), Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(_portBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Perubahan port berlaku setelah Host dijalankan ulang.",
            Foreground = System.Windows.Media.Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Batal", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(cancel);
        var save = new Button { Content = "Simpan", MinWidth = 80, IsDefault = true };
        save.Click += Save_Click;
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public HostConfiguration? Configuration { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_labNameBox.Text) ||
            !int.TryParse(_portBox.Text, out int port) || port is < 1 or > 65535)
        {
            MessageBox.Show("Nama lab dan TCP port 1–65535 wajib valid.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Configuration = new HostConfiguration(_labNameBox.Text.Trim(), port);
        DialogResult = true;
    }
}
