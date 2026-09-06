using System.Windows;
using System.Windows.Input;

namespace Qx.Ui;

public partial class CategoryDialog : Window
{
    private CategoryDialog(
        string scriptName,
        ScriptMeta meta,
        IReadOnlyList<string> categories)
    {
        InitializeComponent();
        ComboBoxPopupBackground.Apply(CategoryBox);

        TitleText.Text = $"Category of “{scriptName}”";
        Title = TitleText.Text;

        CategoryBox.ItemsSource = categories;
        CategoryBox.Text = meta.Category ?? "";

        MaxHeight = SystemParameters.WorkArea.Height * 0.9;
        Loaded += (_, _) => CategoryBox.Focus();
    }

    public static ScriptMeta? Ask(
        Window owner,
        string scriptName,
        ScriptMeta meta,
        IReadOnlyList<string> categories)
    {
        var dialog = new CategoryDialog(scriptName, meta, categories) { Owner = owner };
        return dialog.ShowDialog() == true
            ? dialog.Result()
            : null;
    }

    private ScriptMeta Result() => new()
    {
        Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? null : CategoryBox.Text.Trim()
    };

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        e.Handled = true;
        DialogResult = false;
    }
}
