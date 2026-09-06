namespace Qx.Model.Figures;

/// <summary>
/// A figure together with the gender it is worn as. The client models this pairing as an
/// outfit because the figure string itself carries no gender: every message that transports
/// a figure transports the gender next to it.
/// </summary>
public sealed record FigureOutfit
{
    public static FigureOutfit Empty { get; } = new(Figures.Figure.Empty, FigureGender.Undefined);

    public Figure Figure { get; init; }
    public FigureGender Gender { get; init; }

    /// <summary>The gender code as sent on the wire, falling back to <c>U</c>.</summary>
    public string GenderCode => this.Gender.ToClientString();

    public FigureOutfit(Figure figure, FigureGender gender)
    {
        ArgumentNullException.ThrowIfNull(figure);

        Figure = figure;
        Gender = gender;
    }

    public FigureOutfit(Figure figure, string? gender)
        : this(figure, FigureGenderCode.TryParse(gender, out FigureGender parsed)
            ? parsed
            : FigureGender.Undefined)
    {
    }

    /// <summary>Parses the figure string and gender code as received from the server.</summary>
    public static FigureOutfit Parse(string figure, string gender)
    {
        ArgumentNullException.ThrowIfNull(figure);
        ArgumentNullException.ThrowIfNull(gender);
        return new FigureOutfit(Figures.Figure.Parse(figure), FigureGenderCode.Parse(gender));
    }

    /// <summary>Parses the figure string and gender code as received from the server.</summary>
    public static bool TryParse(string? figure, string? gender, out FigureOutfit outfit)
    {
        if (!Figures.Figure.TryParse(figure, out Figure parsedFigure) ||
            !FigureGenderCode.TryParse(gender, out FigureGender parsedGender))
        {
            outfit = Empty;
            return false;
        }

        outfit = new FigureOutfit(parsedFigure, parsedGender);
        return true;
    }

    public FigureOutfit WithFigure(Figure figure)
    {
        ArgumentNullException.ThrowIfNull(figure);
        return this with { Figure = figure };
    }

    public FigureOutfit WithGender(FigureGender gender) => this with { Gender = gender };

    public void Deconstruct(out Figure figure, out FigureGender gender)
    {
        figure = this.Figure;
        gender = this.Gender;
    }

    public override string ToString() => this.Figure.ToString();
}
