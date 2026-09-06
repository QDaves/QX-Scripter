using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Qx.Ui;

/// <summary>
/// Fetches avatar renders and furni icons from Habbo, once each.
/// </summary>
/// <remarks>
/// <para>
/// Written as a service rather than left to <see cref="BitmapImage.UriSource"/>, which is what the
/// room list used before. That path sends no user agent, and the imaging host answers a request
/// without one with <c>463</c> — so every avatar silently never arrived. WPF swallows the failure,
/// which is why it looked like a rendering problem rather than a rejected request.
/// </para>
/// <para>
/// Three things this adds over a bare fetch: a user agent, a ceiling on how many requests are in
/// flight, and a cache that survives a refresh. A room list rebuilt on every furni event would
/// otherwise re-request every icon it already had.
/// </para>
/// </remarks>
public static class HabboImages
{
    /// <summary>
    /// Any non-empty user agent is accepted; an absent one is refused with <c>463</c>.
    /// </summary>
    private const string UserAgent = "QX";

    /// <summary>
    /// How many fetches may be in flight.
    /// </summary>
    /// <remarks>
    /// A room with two hundred pieces of furni asks for two hundred icons the moment the list
    /// realises, and nothing about a list tells the loader to pace itself. Eight is enough to fill
    /// a view quickly and few enough that the hotel is not hit with a burst per keystroke of a
    /// filter.
    /// </remarks>
    private static readonly SemaphoreSlim Gate = new(8, 8);

    private static readonly HttpClient Http = CreateClient();

    /// <summary>Keyed by url. Holds the task, so callers asking at once share one fetch.</summary>
    private static readonly ConcurrentDictionary<string, Task<ImageSource?>> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Urls that failed, and when.
    /// </summary>
    /// <remarks>
    /// A furni with no icon on the hotel is a 404 every time, and a list that redraws on every
    /// filter keystroke would ask again on each one. Remembering the failure for a while turns that
    /// into one request instead of hundreds.
    /// </remarks>
    private static readonly ConcurrentDictionary<string, DateTime> Failed = new(StringComparer.Ordinal);

    private static readonly TimeSpan FailureHold = TimeSpan.FromMinutes(30);

    private static readonly string DiskRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "imagecache");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        return client;
    }

    /// <summary>
    /// The hotel the renders are asked of, set once when a session opens.
    /// </summary>
    /// <remarks>
    /// Held here rather than threaded through every view. The room list, the chat log, the friend
    /// list and the trade view all draw the same heads, and passing a host into each of them would
    /// be five copies of one fact that changes in one place. It was previously hardcoded to
    /// <c>www.habbo.com</c>, which is simply wrong on any other hotel.
    /// </remarks>
    public static string WebHost { get; set; } = "www.habbo.com";

    /// <summary>
    /// The head render used in a list row, at the size the hotel returns by default.
    /// </summary>
    /// <param name="figure">A modern figure string.</param>
    public static string? HeadUrl(string? figure) => AvatarUrl(figure, WebHost, headOnly: true);

    /// <summary>The full body render, for a profile card rather than a row.</summary>
    public static string? BodyUrl(string? figure) => AvatarUrl(figure, WebHost, headOnly: false);

    /// <summary>The head render against a named hotel, for tests and for anything off-session.</summary>
    public static string? HeadUrl(string? figure, string webHost) =>
        AvatarUrl(figure, webHost, headOnly: true);

    /// <summary>
    /// The head render of someone named rather than described.
    /// </summary>
    /// <remarks>
    /// A ban list carries an id and a name and no figure, because whoever is on it is not in the
    /// room to be looked at. The imaging host will look a name up itself, which is the only way to
    /// put a face to those rows.
    /// </remarks>
    public static string? HeadUrlForName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string host = string.IsNullOrWhiteSpace(WebHost) ? "www.habbo.com" : WebHost;
        return $"https://{host}/habbo-imaging/avatarimage" +
            $"?direction=2&head_direction=2&headonly=1&user={Uri.EscapeDataString(name)}";
    }

    /// <summary>
    /// The whole figure, facing a given way.
    /// </summary>
    /// <remarks>
    /// A head render says who somebody is; a wardrobe is about what an outfit looks like, which is
    /// mostly below the neck. The direction is asked for because an outfit seen only from the front
    /// hides half of what was chosen.
    /// </remarks>
    /// <param name="figure">A modern figure string.</param>
    /// <param name="direction">Zero to seven, clockwise from facing away and to the left.</param>
    /// <param name="size">
    /// "s", "n" or "l". The normal render is what a tile wants: it is drawn at its own size, so a
    /// larger one would only be scaled down again and a smaller one blown up and blurred.
    /// </param>
    public static string? FigureUrl(string? figure, int direction, string size = "n")
    {
        if (string.IsNullOrWhiteSpace(figure))
            return null;

        int facing = ((direction % 8) + 8) % 8;
        string host = string.IsNullOrWhiteSpace(WebHost) ? "www.habbo.com" : WebHost;
        return $"https://{host}/habbo-imaging/avatarimage" +
            $"?direction={facing}&head_direction={facing}&size={size}&gesture=sml&action=std" +
            $"&figure={Uri.EscapeDataString(figure)}";
    }

    /// <summary>The body render against a named hotel.</summary>
    public static string? BodyUrl(string? figure, string webHost) =>
        AvatarUrl(figure, webHost, headOnly: false);

    private static string? AvatarUrl(string? figure, string webHost, bool headOnly)
    {
        if (string.IsNullOrWhiteSpace(figure))
            return null;

        string host = string.IsNullOrWhiteSpace(webHost) ? "www.habbo.com" : webHost;
        return $"https://{host}/habbo-imaging/avatarimage" +
            $"?direction=2&head_direction=2{(headOnly ? "&headonly=1" : "")}" +
            $"&figure={Uri.EscapeDataString(figure)}";
    }

    /// <summary>
    /// The hotel identifier, as the thumbnail store keys its buckets.
    /// </summary>
    /// <remarks>
    /// Derived from the web host rather than carried separately: <c>www.habbo.de</c> is hotel
    /// <c>de</c> and <c>sandbox.habbo.com</c> is <c>s2</c> — but <c>www.habbo.com</c> is <b>us</b>,
    /// not <c>com</c>. The international hotel is called <c>us</c> everywhere inside Habbo, and
    /// deriving the identifier from the domain alone got that one wrong, so every room picture on
    /// the biggest hotel asked for a key that does not exist and was answered with 403.
    /// </remarks>
    private static string HotelIdentifier(string webHost) => webHost switch
    {
        "sandbox.habbo.com" => "s2",
        _ when webHost.EndsWith(".habbo.com.br", StringComparison.OrdinalIgnoreCase) ||
               webHost.EndsWith(".habbo.com.tr", StringComparison.OrdinalIgnoreCase) =>
            webHost[(webHost.LastIndexOf('.') + 1)..],
        _ when webHost.EndsWith(".habbo.com", StringComparison.OrdinalIgnoreCase) => "us",
        _ when webHost.LastIndexOf('.') is var dot && dot > 0 => webHost[(dot + 1)..],
        _ => "us"
    };

    /// <summary>
    /// The picture the navigator shows for a room, or null when there can be none.
    /// </summary>
    /// <remarks>
    /// Neither the imaging host nor the hotel serves these — they are in their own store, keyed by
    /// hotel and room. Not every room has one; a room whose owner never took a picture answers 403
    /// and the caller keeps its placeholder.
    /// </remarks>
    public static string? RoomThumbnailUrl(long roomId) =>
        roomId <= 0
            ? null
            : "https://habbo-stories-content.s3.amazonaws.com/navigator-thumbnail/" +
              $"hh{HotelIdentifier(WebHost)}/{roomId}.png";

    /// <summary>
    /// The banner an official room carries, when the room is one.
    /// </summary>
    /// <remarks>
    /// A different picture from a different place. Staff rooms are given a banner that rides along
    /// in the room data itself, while everything else has at most a camera shot in the navigator's
    /// store — so a room can have one, the other, or neither, and this is worth trying first
    /// because when it exists it is certain rather than a guess at a key.
    /// </remarks>
    public static string? OfficialRoomPictureUrl(string? pictureRef) =>
        string.IsNullOrWhiteSpace(pictureRef)
            ? null
            : $"https://images.habbo.com/web_images/{pictureRef.TrimStart('/')}";

    /// <summary>
    /// The icon for one furni kind.
    /// </summary>
    /// <param name="revision">The furnidata revision. There is no way to guess it.</param>
    /// <param name="identifier">The furnidata class name, colour suffix and all.</param>
    /// <remarks>
    /// The asterisk in a colour variant is an underscore in the file name — <c>chair_basic*3</c> is
    /// served as <c>chair_basic_3_icon.png</c>. The host is global, not per hotel.
    /// </remarks>
    public static string? FurniIconUrl(int revision, string? identifier)
    {
        if (revision <= 0 || string.IsNullOrWhiteSpace(identifier))
            return null;

        return $"https://images.habbo.com/dcr/hof_furni/{revision}/{identifier.Replace('*', '_')}_icon.png";
    }

    /// <summary>
    /// The image at <paramref name="url"/>, from memory, from disk, or from the hotel.
    /// </summary>
    /// <returns>The image, or null when there is none to be had.</returns>
    public static Task<ImageSource?> LoadAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.FromResult<ImageSource?>(null);

        return Cache.GetOrAdd(url, key => FetchAsync(key));
    }

    private static async Task<ImageSource?> FetchAsync(string url)
    {
        if (Failed.TryGetValue(url, out DateTime when))
        {
            if (DateTime.UtcNow - when < FailureHold)
                return null;
            Failed.TryRemove(url, out _);
        }

        string path = DiskPath(url);
        bool exact_pixel_size = url.Contains("/dcr/hof_furni/", StringComparison.OrdinalIgnoreCase);
        if (Decode(ReadDisk(path), exact_pixel_size) is { } cached)
            return cached;

        byte[]? raw = null;
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using HttpResponseMessage response = await Http.GetAsync(url).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                raw = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
        }
        finally
        {
            Gate.Release();
        }

        if (Decode(raw, exact_pixel_size) is not { } image)
        {
            Failed[url] = DateTime.UtcNow;
            // The entry is dropped so a later attempt can retry once the hold has passed; without
            // this the cached null would be the answer forever.
            Cache.TryRemove(url, out _);
            return null;
        }

        WriteDisk(path, raw!);
        return image;
    }

    /// <summary>
    /// Decodes into a frozen bitmap so it can be handed to any thread's binding.
    /// </summary>
    /// <remarks>
    /// <c>OnLoad</c> because the stream is gone the moment this returns, and frozen because the
    /// fetch does not run on the interface thread and an unfrozen bitmap could not cross to it.
    /// </remarks>
    internal static ImageSource? Decode(byte[]? raw, bool exact_pixel_size)
    {
        if (raw is null || raw.Length == 0)
            return null;

        try
        {
            using var stream = new MemoryStream(raw);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            if (!exact_pixel_size)
                return image;

            BitmapSource exact = image;
            if (Math.Abs(image.DpiX - 96) >= 0.01 || Math.Abs(image.DpiY - 96) >= 0.01)
            {
                int stride = checked((image.PixelWidth * image.Format.BitsPerPixel + 7) / 8);
                byte[] pixels = new byte[checked(stride * image.PixelHeight)];
                image.CopyPixels(pixels, stride, 0);
                exact = BitmapSource.Create(
                    image.PixelWidth,
                    image.PixelHeight,
                    96,
                    96,
                    image.Format,
                    image.Palette,
                    pixels,
                    stride);
                exact.Freeze();
            }

            return TrimTransparent(exact);
        }
        catch
        {
            // A 404 body is html, and the hotel answers a refused request with a page rather than
            // an image. Both land here.
            return null;
        }
    }

    private static BitmapSource TrimTransparent(BitmapSource source)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        if (width == 0 || height == 0)
            return source;

        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bgra.CopyPixels(pixels, stride, 0);

        int left = width;
        int top = height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x * 4 + 3] == 0)
                    continue;

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left ||
            (left == 0 && top == 0 && right == width - 1 && bottom == height - 1))
        {
            return source;
        }

        var cropped = new CroppedBitmap(
            source,
            new Int32Rect(left, top, right - left + 1, bottom - top + 1));
        cropped.Freeze();
        return cropped;
    }

    private static string DiskPath(string url)
    {
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32];
        // Split on the first byte so one hotel's furni does not put twenty thousand files in a
        // single directory.
        return Path.Combine(DiskRoot, name[..2], name + ".img");
    }

    private static byte[]? ReadDisk(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteDisk(string path, byte[] raw)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Written beside and moved, so a half-written file is never read as an image.
            string staging = path + ".tmp";
            File.WriteAllBytes(staging, raw);
            File.Move(staging, path, overwrite: true);
        }
        catch
        {
        }
    }
}
