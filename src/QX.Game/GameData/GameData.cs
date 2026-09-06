using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Qx.Game;

internal sealed record GameDataState(
    long Revision,
    long LoadGeneration,
    string? WebHost,
    bool Loaded,
    FurniData? Furni,
    ProductData? Products,
    ExternalTexts? Texts,
    ExternalVariables? Variables);

internal interface IGameDataTransport
{
    Task<IReadOnlyDictionary<string, string>> LoadHashesAsync(
        string web_host,
        CancellationToken cancellation_token);

    Task<string> FetchAsync(
        string web_host,
        string path,
        string hash,
        string cache_name,
        string cache_key,
        CancellationToken cancellation_token);
}

public sealed class GameData
{
    private static readonly Dictionary<string, string> web_hosts = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["game-us.habbo.com"] = "www.habbo.com",
        ["game-es.habbo.com"] = "www.habbo.es",
        ["game-fi.habbo.com"] = "www.habbo.fi",
        ["game-it.habbo.com"] = "www.habbo.it",
        ["game-nl.habbo.com"] = "www.habbo.nl",
        ["game-de.habbo.com"] = "www.habbo.de",
        ["game-fr.habbo.com"] = "www.habbo.fr",
        ["game-br.habbo.com"] = "www.habbo.com.br",
        ["game-tr.habbo.com"] = "www.habbo.com.tr",
        ["game-s2.habbo.com"] = "sandbox.habbo.com"
    };

    private readonly object state_sync = new();
    private readonly IGameDataTransport transport;
    private GameDataState state = new(0, 0, null, false, null, null, null, null);
    private GameDataLoadOperation? active_load;
    private long load_generation;

    public GameData()
        : this(new DefaultGameDataTransport())
    {
    }

    internal GameData(IGameDataTransport transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public FurniData? Furni => State.Furni;
    public ProductData? Products => State.Products;
    public ExternalTexts? Texts => State.Texts;
    public ExternalVariables? Variables => State.Variables;
    public bool IsLoaded => State.Loaded;

    internal GameDataState State => Volatile.Read(ref state);

    public event Action? Loaded;
    public event Action<string>? Status;

    public static string WebHostFor(string gameHost) =>
        web_hosts.GetValueOrDefault(gameHost, "www.habbo.com");

    public async Task LoadAsync(
        string gameHost,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string web_host = WebHostFor(gameHost);
        GameDataLoadOperation operation;
        GameDataLoadOperation? superseded = null;
        bool start = false;
        lock (state_sync)
        {
            GameDataState current = state;
            if (current.Loaded &&
                string.Equals(current.WebHost, web_host, StringComparison.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }
            if (active_load is { } active &&
                string.Equals(active.WebHost, web_host, StringComparison.OrdinalIgnoreCase))
            {
                operation = active;
            }
            else
            {
                superseded = active_load;
                operation = new GameDataLoadOperation(
                    web_host,
                    checked(++load_generation));
                active_load = operation;
                Volatile.Write(ref state, new GameDataState(
                    checked(current.Revision + 1),
                    operation.Generation,
                    web_host,
                    false,
                    null,
                    null,
                    null,
                    null));
                start = true;
            }
        }
        Cancel(superseded);
        if (start)
            _ = RunLoadAsync(operation);
        try
        {
            await operation.Completion.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private async Task RunLoadAsync(GameDataLoadOperation operation)
    {
        try
        {
            PublishStatus(operation, $"loading game data from {operation.WebHost} ...");
            IReadOnlyDictionary<string, string> hashes = await transport
                .LoadHashesAsync(operation.WebHost, operation.Cancellation.Token)
                .ConfigureAwait(false);
            operation.Cancellation.Token.ThrowIfCancellationRequested();

            string furni_hash = ValueOrDefault(hashes, "furnidata", "1");
            string product_hash = ValueOrDefault(hashes, "productdata", "1");
            string texts_hash = ValueOrDefault(hashes, "external_texts", "1");
            string variables_hash = ValueOrDefault(hashes, "external_variables", "1");

            Task<string> furni_request = transport.FetchAsync(
                operation.WebHost,
                "furnidata_json",
                furni_hash,
                "furnidata",
                furni_hash,
                operation.Cancellation.Token);
            Task<string> product_request = transport.FetchAsync(
                operation.WebHost,
                "productdata_json",
                product_hash,
                "productdata",
                product_hash,
                operation.Cancellation.Token);
            Task<string> texts_request = transport.FetchAsync(
                operation.WebHost,
                "external_flash_texts",
                texts_hash,
                "external_texts",
                texts_hash,
                operation.Cancellation.Token);
            Task<VariableLoadResult> variables_request = LoadVariablesAsync(
                operation,
                variables_hash);

            await Task.WhenAll(furni_request, product_request, texts_request)
                .ConfigureAwait(false);
            FurniData furni = FurniData.LoadJson(await furni_request.ConfigureAwait(false));
            ProductData products = ProductData.LoadJson(
                await product_request.ConfigureAwait(false));
            ExternalTexts texts = ExternalTexts.Load(
                await texts_request.ConfigureAwait(false));
            VariableLoadResult variables = await variables_request.ConfigureAwait(false);
            operation.Cancellation.Token.ThrowIfCancellationRequested();

            if (variables.Error is { } variables_error)
            {
                PublishStatus(
                    operation,
                    $"external variables unavailable: {variables_error.Message}");
            }

            bool committed;
            lock (state_sync)
            {
                committed = ReferenceEquals(active_load, operation) &&
                    state.LoadGeneration == operation.Generation &&
                    string.Equals(
                        state.WebHost,
                        operation.WebHost,
                        StringComparison.OrdinalIgnoreCase);
                if (committed)
                {
                    GameDataState current = state;
                    Volatile.Write(ref state, new GameDataState(
                        checked(current.Revision + 1),
                        operation.Generation,
                        operation.WebHost,
                        true,
                        furni,
                        products,
                        texts,
                        variables.Value));
                    active_load = null;
                }
            }
            if (!committed)
                return;

            string variable_count = variables.Value is null
                ? "no variables"
                : $"{variables.Value.Count} variables";
            PublishStatus(
                operation,
                $"game data ready: {furni.FloorItems.Count + furni.WallItems.Count} furni, " +
                $"{products.Count} products, {texts.Count} texts, {variable_count}");
            PublishLoaded(operation);
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested)
        {
            ClearActive(operation);
        }
        catch (Exception error)
        {
            bool current = ClearActive(operation);
            if (current)
                PublishStatus(operation, $"game data load failed: {error.Message}");
        }
        finally
        {
            operation.Completion.TrySetResult();
            operation.Cancellation.Dispose();
        }
    }

    private async Task<VariableLoadResult> LoadVariablesAsync(
        GameDataLoadOperation operation,
        string hash)
    {
        try
        {
            string content = await transport.FetchAsync(
                operation.WebHost,
                "external_variables",
                hash,
                "external_variables",
                hash,
                operation.Cancellation.Token).ConfigureAwait(false);
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["url.prefix"] = $"https://{operation.WebHost}"
            };
            return new VariableLoadResult(
                ExternalVariables.Load(content, isSecure: true, arguments),
                null);
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            return new VariableLoadResult(null, error);
        }
    }

    private bool ClearActive(GameDataLoadOperation operation)
    {
        lock (state_sync)
        {
            if (!ReferenceEquals(active_load, operation))
                return false;
            active_load = null;
            return true;
        }
    }

    private bool Current(GameDataLoadOperation operation)
    {
        GameDataState current = State;
        return current.LoadGeneration == operation.Generation &&
            string.Equals(
                current.WebHost,
                operation.WebHost,
                StringComparison.OrdinalIgnoreCase);
    }

    private void PublishStatus(GameDataLoadOperation operation, string message)
    {
        Action<string>? listeners = Status;
        if (listeners is null)
            return;
        foreach (Action<string> listener in listeners.GetInvocationList().Cast<Action<string>>())
        {
            if (!Current(operation))
                return;
            try
            {
                listener(message);
            }
            catch
            {
            }
        }
    }

    private void PublishLoaded(GameDataLoadOperation operation)
    {
        Action? listeners = Loaded;
        if (listeners is null)
            return;
        foreach (Action listener in listeners.GetInvocationList().Cast<Action>())
        {
            if (!Current(operation) || !State.Loaded)
                return;
            try
            {
                listener();
            }
            catch
            {
            }
        }
    }

    private static string ValueOrDefault(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback) => values.TryGetValue(key, out string? value)
        ? value
        : fallback;

    private static void Cancel(GameDataLoadOperation? operation)
    {
        if (operation is null)
            return;
        try
        {
            operation.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private sealed class GameDataLoadOperation(string web_host, long generation)
    {
        public string WebHost { get; } = web_host;
        public long Generation { get; } = generation;
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record VariableLoadResult(
        ExternalVariables? Value,
        Exception? Error);

    private sealed class DefaultGameDataTransport : IGameDataTransport
    {
        private static readonly HttpClient http = CreateClient();
        private readonly string cache_root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QX Scripter",
            "gamedata");

        public async Task<IReadOnlyDictionary<string, string>> LoadHashesAsync(
            string web_host,
            CancellationToken cancellation_token)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string json = await http.GetStringAsync(
                $"https://{web_host}/gamedata/hashes2",
                cancellation_token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("hashes", out JsonElement hashes))
                return result;
            foreach (JsonElement entry in hashes.EnumerateArray())
            {
                if (entry.TryGetProperty("name", out JsonElement name) &&
                    entry.TryGetProperty("hash", out JsonElement hash))
                {
                    result[name.GetString() ?? ""] = hash.GetString() ?? "1";
                }
            }
            return result;
        }

        public async Task<string> FetchAsync(
            string web_host,
            string path,
            string hash,
            string cache_name,
            string cache_key,
            CancellationToken cancellation_token)
        {
            string directory = Path.Combine(cache_root, web_host);
            string file = Path.Combine(directory, $"{cache_name}_{cache_key}");
            if (File.Exists(file))
                return await File.ReadAllTextAsync(file, cancellation_token).ConfigureAwait(false);

            string content = await http.GetStringAsync(
                $"https://{web_host}/gamedata/{path}/{hash}",
                cancellation_token).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(file, content, cancellation_token)
                    .ConfigureAwait(false);
            }
            catch
            {
            }
            return content;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "QX");
            return client;
        }
    }
}
