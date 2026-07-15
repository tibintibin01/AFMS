using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AFMS.App.Behaviors;

public static class PasswordBoxBinding
{
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
        "Password",
        typeof(string),
        typeof(PasswordBoxBinding),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating",
        typeof(bool),
        typeof(PasswordBoxBinding));

    public static string GetPassword(DependencyObject element) => (string)element.GetValue(PasswordProperty);
    public static void SetPassword(DependencyObject element, string value) => element.SetCurrentValue(PasswordProperty, value);

    private static void PasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= HandlePasswordChanged;
        if (!(bool)passwordBox.GetValue(IsUpdatingProperty))
        {
            passwordBox.Password = args.NewValue as string ?? string.Empty;
        }

        passwordBox.PasswordChanged += HandlePasswordChanged;
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs args)
    {
        var passwordBox = (PasswordBox)sender;
        passwordBox.SetValue(IsUpdatingProperty, true);
        passwordBox.SetCurrentValue(PasswordProperty, passwordBox.Password);
        BindingOperations.GetBindingExpression(passwordBox, PasswordProperty)?.UpdateSource();
        passwordBox.SetValue(IsUpdatingProperty, false);
    }
}
