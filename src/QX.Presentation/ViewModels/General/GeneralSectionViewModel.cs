namespace Qx.Presentation.ViewModels.General;

public sealed class GeneralSectionViewModel
{
    public GeneralSectionViewModel(string title, string description, IReadOnlyList<GeneralRowViewModel> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
            throw new ArgumentException("A section needs at least one row.", nameof(rows));
        Title = title;
        Description = description;
        Rows = rows;
        rows[^1].MarkLast();
    }

    public string Title { get; }

    public string Description { get; }

    public IReadOnlyList<GeneralRowViewModel> Rows { get; }
}
