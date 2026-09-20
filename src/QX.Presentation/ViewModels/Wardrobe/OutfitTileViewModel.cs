using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Services.Wardrobe;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Wardrobe;

public sealed partial class OutfitTileViewModel : ObservableObject
{
    public const int FrontDirection = 2;
    public const int DirectionCount = 8;

    readonly IImageService _images;
    readonly HotelContext _hotel;
    int _direction = FrontDirection;
    string _host = "";

    public OutfitTileViewModel(SavedOutfit outfit, IImageService images, HotelContext hotel)
    {
        _images = images ?? throw new ArgumentNullException(nameof(images));
        _hotel = hotel ?? throw new ArgumentNullException(nameof(hotel));
        Outfit = outfit ?? throw new ArgumentNullException(nameof(outfit));
        Show(_direction, _hotel.WebHost);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Figure), nameof(Name), nameof(Gender), nameof(Title))]
    public partial SavedOutfit Outfit { get; private set; }

    [ObservableProperty]
    public partial ImageRequest? Image { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TurnLeftCommand), nameof(TurnRightCommand))]
    public partial bool IsTurning { get; private set; }

    public string Figure => Outfit.Figure;

    public string Name => Outfit.Name;

    public string Gender => WardrobeText.Gender(Outfit.Gender);

    public string Title => WardrobeText.Title(Outfit.Figure, Outfit.Name);

    public int Direction => _direction;

    public void Adopt(SavedOutfit outfit)
    {
        ArgumentNullException.ThrowIfNull(outfit);
        Outfit = outfit;
        Rehost();
    }

    public void Rehost()
    {
        string host = _hotel.WebHost;
        if (string.Equals(_host, host, StringComparison.OrdinalIgnoreCase))
            return;
        Show(_direction, host);
    }

    public bool Matches(string term) =>
        term.Length == 0 ||
        Title.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
        Figure.Contains(term, StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanTurn))]
    Task TurnLeftAsync(CancellationToken cancellation_token) => TurnAsync(-1, cancellation_token);

    [RelayCommand(CanExecute = nameof(CanTurn))]
    Task TurnRightAsync(CancellationToken cancellation_token) => TurnAsync(1, cancellation_token);

    bool CanTurn() => !IsTurning;

    async Task TurnAsync(int by, CancellationToken cancellation_token)
    {
        if (IsTurning)
            return;
        int next = (((_direction + by) % DirectionCount) + DirectionCount) % DirectionCount;
        string host = _hotel.WebHost;
        if (Render(next, host) is not { } request)
            return;
        IsTurning = true;
        try
        {
            if (!await _images.PreloadAsync(request.Url, cancellation_token))
                return;
            _direction = next;
            _host = host;
            Image = request;
            OnPropertyChanged(nameof(Direction));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsTurning = false;
        }
    }

    void Show(int direction, string host)
    {
        _host = host;
        Image = Render(direction, host);
    }

    ImageRequest? Render(int direction, string host) =>
        HabboUrls.Figure(Outfit.Figure, direction, host) is { } url ? new ImageRequest(url, false, IconKind.Outfit) : null;
}
