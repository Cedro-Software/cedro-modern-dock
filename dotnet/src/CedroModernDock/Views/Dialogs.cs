using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace CedroModernDock.Views;

/// <summary>Small modal dialogs (prompt, confirm, message) in the Cedro style.</summary>
public static class Dialogs
{
    public static async Task<string?> PromptAsync(Window owner, string title, string message, string initial,
        string okText, string cancelText)
    {
        var box = new TextBox { Text = initial, MinWidth = 320 };
        string? result = null;
        var dialog = Build(title, message, box, okText, cancelText, danger: false,
            onOk: d => { result = box.Text ?? ""; d.Close(); });
        if (owner is MainWindow)
        {
            // The dock is a small no-activate bar: center on screen, stay on top.
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Topmost = true;
        }
        dialog.Opened += (_, _) => { box.Focus(); box.SelectAll(); };
        await dialog.ShowDialog(owner);
        return result;
    }

    public static async Task<bool> ConfirmAsync(Window owner, string title, string message,
        string okText, string cancelText, bool danger = false)
    {
        bool ok = false;
        var dialog = Build(title, message, null, okText, cancelText, danger, onOk: d => { ok = true; d.Close(); });
        await dialog.ShowDialog(owner);
        return ok;
    }

    public static Task MessageAsync(Window owner, string title, string message, string okText)
        => Build(title, message, null, okText, null, danger: false, onOk: d => d.Close()).ShowDialog(owner);

    private static Window Build(string title, string message, Control? input, string okText,
        string? cancelText, bool danger, Action<Window> onOk)
    {
        var ok = new Button { Content = okText, IsDefault = true, MinWidth = 96, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Classes.Add(danger ? "danger" : "accent");

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        Window? dialog = null;
        if (cancelText != null)
        {
            var cancel = new Button { Content = cancelText, IsCancel = true, MinWidth = 96, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancel.Classes.Add("subtle");
            cancel.Click += (_, _) => dialog!.Close();
            buttons.Children.Add(cancel);
        }
        buttons.Children.Add(ok);

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(24, 22, 24, 20), MaxWidth = 460 };
        content.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.SemiBold });
        if (!string.IsNullOrEmpty(message))
        {
            var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
            text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("CedroTextSecondary"));
            content.Children.Add(text);
        }
        if (input != null) content.Children.Add(input);
        content.Children.Add(new Border { Height = 4 });
        content.Children.Add(buttons);

        dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            RequestedThemeVariant = ThemeVariant.Dark,
            Content = content
        };
        dialog.Bind(Window.BackgroundProperty, dialog.GetResourceObservable("CedroWindowBackground"));
        dialog.Bind(Window.ForegroundProperty, dialog.GetResourceObservable("CedroText"));
        ok.Click += (_, _) => onOk(dialog);
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(); };
        return dialog;
    }
}
