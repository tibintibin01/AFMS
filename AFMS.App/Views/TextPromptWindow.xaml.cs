using System.Windows;

namespace AFMS.App.Views;

/// <summary>
/// Small modal for the short answers the app needs, such as the reason a record
/// is being voided.
/// </summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string prompt, string title, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string EnteredText => InputBox.Text.Trim();

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
