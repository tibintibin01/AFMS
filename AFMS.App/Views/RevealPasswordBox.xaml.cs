using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace AFMS.App.Views;

/// <summary>
/// A password field with an eye toggle to check what was typed. Used wherever a
/// password is entered, so the behaviour and look stay identical across screens.
///
/// The revealed text lives only in this control while it is on screen. Nothing is
/// stored, logged, or copied anywhere.
/// </summary>
public partial class RevealPasswordBox : UserControl
{
    // Guards against the two inner boxes and the Password property updating each
    // other in a loop.
    private bool _syncing;

    public RevealPasswordBox()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyFieldName();
    }

    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password),
        typeof(string),
        typeof(RevealPasswordBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPasswordChanged));

    /// <summary>The typed password. Bind this two-way to the view model.</summary>
    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(
        nameof(MaxLength), typeof(int), typeof(RevealPasswordBox), new PropertyMetadata(128, OnMaxLengthChanged));

    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public static readonly DependencyProperty FieldNameProperty = DependencyProperty.Register(
        nameof(FieldName), typeof(string), typeof(RevealPasswordBox),
        new PropertyMetadata("Password", OnFieldNameChanged));

    /// <summary>Spoken by screen readers, for example "New password".</summary>
    public string FieldName
    {
        get => (string)GetValue(FieldNameProperty);
        set => SetValue(FieldNameProperty, value);
    }

    /// <summary>Puts the cursor in whichever box is currently showing.</summary>
    public void FocusInput()
    {
        if (Reveal.IsChecked == true)
        {
            Plain.Focus();
            Plain.CaretIndex = Plain.Text.Length;
        }
        else
        {
            Masked.Focus();
        }
    }

    /// <summary>
    /// Pushes an outside change, such as the view model wiping the password after a
    /// failed sign-in, into the boxes on screen.
    /// </summary>
    private static void OnPasswordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var control = (RevealPasswordBox)sender;
        if (control._syncing) return;

        var value = e.NewValue as string ?? string.Empty;
        control._syncing = true;
        try
        {
            if (control.Masked.Password != value) control.Masked.Password = value;
            if (control.Plain.Text != value) control.Plain.Text = value;
        }
        finally
        {
            control._syncing = false;
        }
    }

    private static void OnMaxLengthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var control = (RevealPasswordBox)sender;
        var length = (int)e.NewValue;
        control.Masked.MaxLength = length;
        control.Plain.MaxLength = length;
    }

    private static void OnFieldNameChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((RevealPasswordBox)sender).ApplyFieldName();

    private void ApplyFieldName()
    {
        AutomationProperties.SetName(Masked, FieldName);
        AutomationProperties.SetName(Plain, $"{FieldName}, visible");
        AutomationProperties.SetName(Reveal, $"Show or hide {FieldName.ToLowerInvariant()}");
    }

    private void OnMaskedChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        Write(Masked.Password);
    }

    private void OnPlainChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        Write(Plain.Text);
    }

    private void Write(string value)
    {
        _syncing = true;
        try
        {
            Password = value;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Swaps the visible box, carrying the text and caret across.</summary>
    private void OnRevealChanged(object sender, RoutedEventArgs e)
    {
        var reveal = Reveal.IsChecked == true;
        Reveal.ToolTip = reveal ? "Hide password" : "Show password";

        _syncing = true;
        try
        {
            if (reveal)
            {
                Plain.Text = Masked.Password;
                Plain.Visibility = Visibility.Visible;
                Masked.Visibility = Visibility.Collapsed;
            }
            else
            {
                Masked.Password = Plain.Text;
                Masked.Visibility = Visibility.Visible;
                Plain.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            _syncing = false;
        }

        FocusInput();
    }
}
