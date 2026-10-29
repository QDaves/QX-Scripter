using System.ComponentModel;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Search;
using Qx.Presentation.Services.Library;
using Qx.Presentation.ViewModels.Library;

namespace Qx.Desktop.Views.Library;

public sealed partial class CommunityView : UserControl
{
    CommunityViewModel? _model;

    public CommunityView()
    {
        InitializeComponent();
        SearchPanel.Install(Source);
        ActualThemeVariantChanged += (_, _) => color_source();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        connect(DataContext as CommunityViewModel);
        color_source();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        connect(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        connect(DataContext as CommunityViewModel);
    }

    void connect(CommunityViewModel? model)
    {
        if (_model is not null)
            _model.PropertyChanged -= changed;
        _model = model;
        if (_model is not null)
            _model.PropertyChanged += changed;
        show_source();
    }

    void changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommunityViewModel.Detail))
            show_source();
    }

    void show_source()
    {
        string code = _model?.Detail?.Code ?? "";
        if (Source.Text == code)
            return;
        Source.Text = code;
        Source.ScrollToHome();
    }

    void color_source()
    {
        using Stream stream = typeof(HighlightingManager).Assembly.GetManifestResourceStream("AvaloniaEdit.Highlighting.Resources.CSharp-Mode.xshd")!;
        XDocument definition = XDocument.Load(stream);
        foreach (XElement element in definition.Descendants().Where(element => element.Name.LocalName == "Color"))
        {
            string key = (string?)element.Attribute("name") switch
            {
                "Comment" => "QxSyntaxCommentBrush",
                "String" or "Char" => "QxSyntaxStringBrush",
                "Preprocessor" => "QxSyntaxPreprocessorBrush",
                "NumberLiteral" => "QxSyntaxNumberBrush",
                "MethodCall" => "QxSyntaxMethodBrush",
                "StringInterpolation" or "Punctuation" => "QxSyntaxPunctuationBrush",
                _ => "QxSyntaxKeywordBrush"
            };
            if (this.TryFindResource(key, ActualThemeVariant, out object? found) && found is ISolidColorBrush brush)
                element.SetAttributeValue("foreground", brush.Color.ToString());
            element.Attribute("fontWeight")?.Remove();
        }
        using var reader = definition.CreateReader();
        Source.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    void open_script(object? sender, TappedEventArgs e)
    {
        if (DataContext is not CommunityViewModel model || e.Source is not Control source ||
            source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is not CommunityScript script)
            return;
        model.OpenCommand.Execute(script);
        e.Handled = true;
    }
}
