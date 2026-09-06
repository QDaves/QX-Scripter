using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Qx.Ui;

internal static class ComboBoxPopupBackground
{
    public static void Apply(ComboBox comboBox)
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        comboBox.DropDownOpened += (_, _) => Repaint(comboBox);
    }

    private static void Repaint(ComboBox comboBox)
    {
        comboBox.ApplyTemplate();
        if (comboBox.Template?.FindName("PART_Popup", comboBox) is ComboBoxPopup popup)
            popup.Background = comboBox.TryFindResource("MaterialDesign.Brush.Background") as Brush;
    }
}
