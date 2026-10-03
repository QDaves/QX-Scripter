using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

/// <summary>Specifies the kind of an application member.</summary>
public enum ApplicationMemberKind
{
    /// <summary>A member that reads state the application already holds.</summary>
    Query,
    /// <summary>A member that can send messages to the hotel, wait for answers or change state.</summary>
    Operation,
    /// <summary>A member that publishes values to its subscribers.</summary>
    Event
}

/// <summary>Specifies whether an application member can run in a short-lived runtime.</summary>
public enum ApplicationInvocationScope
{
    /// <summary>The member works in a short-lived runtime, such as a single <c>QX app invoke</c> command.</summary>
    Transient,
    /// <summary>The member reads state that a long-running runtime collects, so the command line invokes it only inside <c>QX app session</c>.</summary>
    Persistent
}

/// <summary>Specifies the surfaces an application member is offered on.</summary>
/// <remarks>
/// The command line lists and invokes only members with <see cref="Cli"/>, and the MCP server
/// creates an <c>application_*</c> tool only for queries and operations with <see cref="Mcp"/>.
/// </remarks>
[Flags]
public enum ApplicationExposure
{
    /// <summary>No surface. A descriptor must name at least one surface.</summary>
    None = 0,
    /// <summary>The desktop user interface.</summary>
    Ui = 1,
    /// <summary>The <c>QX app</c> command line.</summary>
    Cli = 2,
    /// <summary>The C# scripting runtime.</summary>
    Scripting = 4,
    /// <summary>The MCP server, which offers the member as an <c>application_*</c> tool.</summary>
    Mcp = 8,
    /// <summary>Every surface.</summary>
    All = Ui | Cli | Scripting | Mcp
}

/// <summary>Specifies a part of the client state that an application member requires or affects.</summary>
/// <remarks>
/// A member is available only when every state in <see cref="ApplicationDescriptor.RequiredStates"/>
/// is satisfied. Keys that name a whole domain, such as <see cref="CatalogCache"/> or
/// <see cref="Achievements"/>, are always satisfied and describe state effects.
/// </remarks>
public enum ApplicationStateKey
{
    /// <summary>A hotel session is active.</summary>
    HotelConnected,
    /// <summary>The catalog cache.</summary>
    CatalogCache,
    /// <summary>The user is in a room.</summary>
    RoomActive,
    /// <summary>
    /// The current room is ready: the server reported it ready and confirmed the entry. Its avatars
    /// and furni may still be arriving.
    /// </summary>
    RoomReady,
    /// <summary>The local user's profile has been received.</summary>
    ProfileLoaded,
    /// <summary>The block list has been received.</summary>
    ProfileBlockListLoaded,
    /// <summary>The ignore list has been received.</summary>
    ProfileIgnoreListLoaded,
    /// <summary>The owned figure sets have been received.</summary>
    ProfileFigureSetsLoaded,
    /// <summary>The account sanction status has been received.</summary>
    ProfileSanctionsLoaded,
    /// <summary>The complete friend list has been received.</summary>
    FriendsLoaded,
    /// <summary>The navigator metadata has been received.</summary>
    NavigatorMetadataLoaded,
    /// <summary>The room categories have been received.</summary>
    NavigatorFlatCategoriesLoaded,
    /// <summary>The marketplace configuration has been received.</summary>
    MarketplaceConfigurationLoaded,
    /// <summary>The marketplace eligibility has been received.</summary>
    MarketplaceEligibilityLoaded,
    /// <summary>The complete furni inventory has been received in the active session.</summary>
    InventoryFurniLoaded,
    /// <summary>The complete pet inventory has been received in the active session.</summary>
    InventoryPetsLoaded,
    /// <summary>No trade is open in the active session.</summary>
    TradeInactive,
    /// <summary>A trade is open in the active session.</summary>
    TradeActive,
    /// <summary>The open trade is in the trading phase.</summary>
    TradeTrading,
    /// <summary>The open trade is waiting for the final confirmation.</summary>
    TradeAwaitingConfirmation,
    /// <summary>The local user is allowed to trade in the open trade.</summary>
    TradeLocalCanTrade,
    /// <summary>The silver offered by both sides of the open trade covers its silver fee.</summary>
    TradeSilverFeeReached,
    /// <summary>The trade NFT inventory has been received in the active session.</summary>
    TradeNftInventoryLoaded,
    /// <summary>The ban list of the current room has been received in the active session.</summary>
    RoomBansLoaded,
    /// <summary>The credit and activity point balances have been received in the active session.</summary>
    WalletLoaded,
    /// <summary>The settings of at least one room have been received in the active session.</summary>
    RoomSettingsLoaded,
    /// <summary>The catalog purchase state.</summary>
    CatalogPurchase,
    /// <summary>The subscription state.</summary>
    Subscriptions,
    /// <summary>The gift state.</summary>
    Gifts,
    /// <summary>The crafting state.</summary>
    Crafting,
    /// <summary>The achievement state.</summary>
    Achievements,
    /// <summary>The badge inventory.</summary>
    BadgeInventory,
    /// <summary>The earnings vault.</summary>
    Earnings,
    /// <summary>The daily tasks.</summary>
    DailyTasks,
    /// <summary>The quests.</summary>
    Quests,
    /// <summary>The forum cache.</summary>
    Forums,
    /// <summary>The leaderboards.</summary>
    Leaderboards,
    /// <summary>The habbicons.</summary>
    Habbicons
}

/// <summary>Specifies how an application member affects a part of the client state.</summary>
public enum ApplicationStateEffectKind
{
    /// <summary>The member reads the state.</summary>
    Reads,
    /// <summary>The member can change the state.</summary>
    Changes,
    /// <summary>The member can invalidate the state, so it has to be loaded again.</summary>
    Invalidates
}

/// <summary>Specifies how an application member uses a message.</summary>
public enum ApplicationMessageRole
{
    /// <summary>The member reads the message when it arrives.</summary>
    Observe,
    /// <summary>The member sends the message.</summary>
    Send
}

/// <summary>Represents one request parameter of an application member.</summary>
/// <remarks>
/// The JSON input schema of a member is built from its parameters. Each parameter stands for one
/// parameter of the public constructor of the request type: the name is the snake_case form of the
/// constructor parameter name, and the type, required flag and default value match the constructor
/// parameter. <see cref="ApplicationDescriptor"/> checks this when it is created.
/// </remarks>
/// <param name="Name">The parameter name as it appears in JSON, such as <c>timeout_milliseconds</c>.</param>
/// <param name="Type">The type of the parameter value.</param>
/// <param name="Required">Whether the parameter must be given.</param>
/// <param name="DefaultValue">The value used when the parameter is omitted, or <see langword="null"/> when there is none. A required parameter has no default.</param>
/// <param name="Description">The description of the parameter.</param>
/// <param name="Constraints">The limits the value must satisfy, or <see langword="null"/> when there are none.</param>
public sealed record ApplicationParameterDescriptor(
    string Name,
    Type Type,
    bool Required,
    object? DefaultValue,
    string Description,
    ApplicationParameterConstraints? Constraints = null);

/// <summary>Represents the limits a parameter value must satisfy.</summary>
/// <remarks>
/// Each limit must fit the parameter type: numeric limits need a byte, short, int or long type,
/// length and UTF-8 limits need a string, and item limits need a collection.
/// </remarks>
/// <param name="Minimum">The smallest allowed number, or <see langword="null"/> for no lower bound.</param>
/// <param name="Maximum">The largest allowed number, or <see langword="null"/> for no upper bound.</param>
/// <param name="MinLength">The smallest allowed string length, or <see langword="null"/> for no lower bound.</param>
/// <param name="MaxLength">The largest allowed string length, or <see langword="null"/> for no upper bound.</param>
/// <param name="MinItems">The smallest allowed number of collection items, or <see langword="null"/> for no lower bound.</param>
/// <param name="MaxItems">The largest allowed number of collection items, or <see langword="null"/> for no upper bound.</param>
/// <param name="MaxUtf8Bytes">The largest allowed UTF-8 byte count of a string, or <see langword="null"/> for no limit.</param>
/// <param name="Pattern">The regular expression a string must match, or <see langword="null"/> for none.</param>
public sealed record ApplicationParameterConstraints(
    long? Minimum = null,
    long? Maximum = null,
    int? MinLength = null,
    int? MaxLength = null,
    int? MinItems = null,
    int? MaxItems = null,
    int? MaxUtf8Bytes = null,
    string? Pattern = null);

/// <summary>Represents how an application member affects one part of the client state.</summary>
/// <param name="State">The part of the state that is affected.</param>
/// <param name="Kind">How the state is affected.</param>
public sealed record ApplicationStateEffect(
    ApplicationStateKey State,
    ApplicationStateEffectKind Kind);

/// <summary>Represents a message that an application member sends or observes.</summary>
/// <remarks>The availability of a member is computed from its required messages in the active session.</remarks>
/// <param name="Key">The semantic key of the message.</param>
/// <param name="Direction">The direction the message travels.</param>
/// <param name="Role">Whether the member sends or observes the message.</param>
/// <param name="Required">Whether the member needs the message. An optional message does not affect availability.</param>
public sealed record ApplicationMessageRequirement(
    MessageKey Key,
    MessageDirection Direction,
    ApplicationMessageRole Role,
    bool Required = true);

/// <summary>Represents the hints the MCP server reports for the tool of an application member.</summary>
/// <remarks>The values become the MCP tool annotations. A read-only member cannot be destructive.</remarks>
/// <param name="ReadOnly">Whether the member leaves its environment unchanged.</param>
/// <param name="Destructive">Whether the member can make changes that cannot be undone.</param>
/// <param name="Idempotent">Whether calling the member again with the same arguments has no further effect.</param>
/// <param name="OpenWorld">Whether the member talks to the hotel rather than only reading state the application holds.</param>
public sealed record ApplicationToolHints(
    bool ReadOnly,
    bool Destructive,
    bool Idempotent,
    bool OpenWorld);

/// <summary>Represents the metadata of one application member.</summary>
/// <remarks>
/// Application members are the queries, operations and events that the MCP <c>application_*</c>
/// tools, the <c>QX app</c> commands and scripts call through <see cref="IApplicationRuntime"/>.
/// </remarks>
public sealed class ApplicationDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="ApplicationDescriptor"/> class.</summary>
    /// <param name="id">The member id, such as <c>room.chat.talk</c>.</param>
    /// <param name="title">The short display title.</param>
    /// <param name="description">The description of what the member does.</param>
    /// <param name="kind">The kind of member.</param>
    /// <param name="exposure">The surfaces the member is offered on.</param>
    /// <param name="requestType">The request type, or <see langword="null"/> for an event. A call binds its parameters through the single public constructor of this type.</param>
    /// <param name="resultType">The result type of a call, or the value type of an event.</param>
    /// <param name="parameters">The request parameters with unique names, or <see langword="null"/> for none. An event has no parameters, and the parameters of a call must match the parameters of the public constructor of <paramref name="requestType"/>.</param>
    /// <param name="requiredStates">The states that must be satisfied before the member can be invoked, or <see langword="null"/> for none. Duplicates are removed.</param>
    /// <param name="stateEffects">The ways the member affects the client state, or <see langword="null"/> for none.</param>
    /// <param name="messages">The messages the member sends or observes, or <see langword="null"/> for none.</param>
    /// <param name="toolHints">The MCP tool hints, or <see langword="null"/>. Required when <paramref name="exposure"/> includes <see cref="ApplicationExposure.Mcp"/>.</param>
    /// <param name="invocationScope">Whether the member can run in a short-lived runtime.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/>, <paramref name="title"/>, <paramref name="description"/> or <paramref name="resultType"/> is <see langword="null"/>, or a parameter has no name or type.</exception>
    /// <exception cref="ArgumentException">Thrown when a text argument is empty or white space, the request type does not fit the kind or does not have exactly one public constructor, an event declares parameters or is offered to MCP, an MCP member has no tool hints, the hints are both read-only and destructive, a parameter or message requirement is invalid, or the parameters of a call differ from the parameters of the request type's public constructor in name, type, required flag or default value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="exposure"/> is <see cref="ApplicationExposure.None"/> or holds an undefined flag, or <paramref name="invocationScope"/> is not defined.</exception>
    public ApplicationDescriptor(
        string id,
        string title,
        string description,
        ApplicationMemberKind kind,
        ApplicationExposure exposure,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type? requestType,
        Type resultType,
        IEnumerable<ApplicationParameterDescriptor>? parameters = null,
        IEnumerable<ApplicationStateKey>? requiredStates = null,
        IEnumerable<ApplicationStateEffect>? stateEffects = null,
        IEnumerable<ApplicationMessageRequirement>? messages = null,
        ApplicationToolHints? toolHints = null,
        ApplicationInvocationScope invocationScope = ApplicationInvocationScope.Transient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(resultType);
        if (kind is ApplicationMemberKind.Event && requestType is not null)
            throw new ArgumentException("An event cannot declare a request type.", nameof(requestType));
        if (kind is not ApplicationMemberKind.Event && requestType is null)
            throw new ArgumentException("A call requires a request type.", nameof(requestType));
        if (exposure is ApplicationExposure.None || (exposure & ~ApplicationExposure.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(exposure));
        if (!Enum.IsDefined(invocationScope))
            throw new ArgumentOutOfRangeException(nameof(invocationScope));
        if (exposure.HasFlag(ApplicationExposure.Mcp) && kind is ApplicationMemberKind.Event)
            throw new ArgumentException("MCP event exposure requires a streaming binding.", nameof(exposure));
        if (exposure.HasFlag(ApplicationExposure.Mcp) && toolHints is null)
            throw new ArgumentException("MCP call exposure requires explicit tool hints.", nameof(toolHints));
        if (toolHints is { ReadOnly: true, Destructive: true })
            throw new ArgumentException("A read-only application member cannot be destructive.", nameof(toolHints));

        ApplicationParameterDescriptor[] parameter_values = [.. parameters ?? []];
        if (kind is ApplicationMemberKind.Event && parameter_values.Length != 0)
            throw new ArgumentException("An event cannot declare parameters.", nameof(parameters));
        string[] duplicate_parameters = parameter_values
            .GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicate_parameters.Length != 0)
            throw new ArgumentException("Application parameter names must be unique.", nameof(parameters));
        foreach (ApplicationParameterDescriptor parameter in parameter_values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Name);
            ArgumentNullException.ThrowIfNull(parameter.Type);
            ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Description);
            if (parameter.Required && parameter.DefaultValue is not null)
                throw new ArgumentException($"Required parameter '{parameter.Name}' cannot declare a default value.", nameof(parameters));
            if (parameter.DefaultValue is not null && !parameter.Type.IsInstanceOfType(parameter.DefaultValue))
                throw new ArgumentException($"Default value for '{parameter.Name}' does not match '{parameter.Type.FullName}'.", nameof(parameters));
            ValidateConstraints(parameter, nameof(parameters));
        }
        if (requestType is not null)
            ValidateRequestParameters(requestType, parameter_values, nameof(requestType), nameof(parameters));

        ApplicationStateKey[] state_values = [.. (requiredStates ?? []).Distinct()];
        ApplicationStateEffect[] effect_values = [.. stateEffects ?? []];
        ApplicationMessageRequirement[] message_values = [.. messages ?? []];
        if (message_values.Any(message => message.Key.IsEmpty || message.Direction is MessageDirection.None))
            throw new ArgumentException("Application message requirements need a semantic key and direction.", nameof(messages));

        Id = id;
        Title = title;
        Description = description;
        Kind = kind;
        Exposure = exposure;
        RequestType = requestType;
        ResultType = resultType;
        Parameters = Array.AsReadOnly(parameter_values);
        RequiredStates = Array.AsReadOnly(state_values);
        StateEffects = Array.AsReadOnly(effect_values);
        Messages = Array.AsReadOnly(message_values);
        ToolHints = toolHints;
        InvocationScope = invocationScope;
    }

    /// <summary>Gets the member id, such as <c>room.chat.talk</c>.</summary>
    public string Id { get; }
    /// <summary>Gets the short display title.</summary>
    public string Title { get; }
    /// <summary>Gets the description of what the member does.</summary>
    public string Description { get; }
    /// <summary>Gets the kind of member.</summary>
    public ApplicationMemberKind Kind { get; }
    /// <summary>Gets the surfaces the member is offered on.</summary>
    public ApplicationExposure Exposure { get; }
    /// <summary>Gets the request type, or <see langword="null"/> for an event.</summary>
    public Type? RequestType { get; }
    /// <summary>Gets the result type of a call, or the value type of an event.</summary>
    public Type ResultType { get; }
    /// <summary>Gets the request parameters.</summary>
    public IReadOnlyList<ApplicationParameterDescriptor> Parameters { get; }
    /// <summary>Gets the states that must be satisfied before the member can be invoked.</summary>
    public IReadOnlyList<ApplicationStateKey> RequiredStates { get; }
    /// <summary>Gets the ways the member affects the client state.</summary>
    public IReadOnlyList<ApplicationStateEffect> StateEffects { get; }
    /// <summary>Gets the messages the member sends or observes.</summary>
    public IReadOnlyList<ApplicationMessageRequirement> Messages { get; }
    /// <summary>Gets the MCP tool hints, or <see langword="null"/> when none were given.</summary>
    public ApplicationToolHints? ToolHints { get; }
    /// <summary>Gets whether the member can run in a short-lived runtime.</summary>
    public ApplicationInvocationScope InvocationScope { get; }

    private static void ValidateConstraints(
        ApplicationParameterDescriptor parameter,
        string argument_name)
    {
        if (parameter.Constraints is not { } constraints)
            return;
        Type type = Nullable.GetUnderlyingType(parameter.Type) ?? parameter.Type;
        bool numeric = type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long);
        if ((constraints.Minimum is not null || constraints.Maximum is not null) && !numeric)
            throw new ArgumentException($"Parameter '{parameter.Name}' has numeric constraints for a non-numeric type.", argument_name);
        if (constraints.Minimum > constraints.Maximum)
            throw new ArgumentException($"Parameter '{parameter.Name}' has an invalid numeric range.", argument_name);
        if ((constraints.MinLength is not null ||
             constraints.MaxLength is not null ||
             constraints.MaxUtf8Bytes is not null) && type != typeof(string))
        {
            throw new ArgumentException($"Parameter '{parameter.Name}' has string constraints for a non-string type.", argument_name);
        }
        if (constraints.MinLength < 0 ||
            constraints.MaxLength < 0 ||
            constraints.MaxUtf8Bytes < 1 ||
            constraints.MinLength > constraints.MaxLength)
        {
            throw new ArgumentException($"Parameter '{parameter.Name}' has an invalid string range.", argument_name);
        }
        bool collection = type != typeof(string) &&
            typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        if ((constraints.MinItems is not null || constraints.MaxItems is not null) && !collection)
            throw new ArgumentException($"Parameter '{parameter.Name}' has collection constraints for a non-collection type.", argument_name);
        if (constraints.MinItems < 0 ||
            constraints.MaxItems < 0 ||
            constraints.MinItems > constraints.MaxItems)
        {
            throw new ArgumentException($"Parameter '{parameter.Name}' has an invalid collection range.", argument_name);
        }
        if (constraints.Pattern is { Length: 0 })
            throw new ArgumentException($"Parameter '{parameter.Name}' has an empty pattern.", argument_name);
    }

    private static void ValidateRequestParameters(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        ApplicationParameterDescriptor[] parameters,
        string request_argument_name,
        string parameters_argument_name)
    {
        ConstructorInfo[] constructors = request_type.GetConstructors();
        if (constructors.Length != 1)
            throw new ArgumentException($"Request type '{request_type}' must have exactly one public constructor.", request_argument_name);
        Dictionary<string, ApplicationParameterDescriptor> declared =
            parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        foreach (ParameterInfo constructor_parameter in constructors[0].GetParameters())
        {
            string name = JsonNamingPolicy.SnakeCaseLower.ConvertName(constructor_parameter.Name!);
            if (!declared.Remove(name, out ApplicationParameterDescriptor? parameter))
                throw new ArgumentException($"Parameter '{name}' of '{request_type}' is not declared.", parameters_argument_name);
            if (parameter.Type != constructor_parameter.ParameterType)
                throw new ArgumentException($"Parameter '{name}' must have the type '{constructor_parameter.ParameterType}' to match '{request_type}'.", parameters_argument_name);
            if (parameter.Required == constructor_parameter.HasDefaultValue)
                throw new ArgumentException($"Parameter '{name}' must be {(parameter.Required ? "optional" : "required")} to match '{request_type}'.", parameters_argument_name);
            if (constructor_parameter.HasDefaultValue && !Equals(parameter.DefaultValue, constructor_parameter.DefaultValue))
                throw new ArgumentException($"Parameter '{name}' must default to '{constructor_parameter.DefaultValue ?? "null"}' to match '{request_type}'.", parameters_argument_name);
        }
        if (declared.Count != 0)
            throw new ArgumentException($"Parameter '{declared.Keys.First()}' is not a parameter of '{request_type}'.", parameters_argument_name);
    }
}

/// <summary>Contains the ids of every application member.</summary>
/// <remarks>
/// Pass an id to <see cref="IApplicationRuntime"/> to describe, invoke or subscribe to a member. The
/// MCP tool of a query or operation is named <c>application_</c> followed by the id with dots
/// replaced by underscores.
/// </remarks>
public static class ApplicationMemberIds
{
    /// <summary>The id of the <c>room.chat.history</c> query, which reads the room chat journal page by page.</summary>
    public const string RoomChatHistory = "room.chat.history";
    /// <summary>The id of the <c>room.chat.talk</c> operation, which says a public message in the current room.</summary>
    public const string RoomChatTalk = "room.chat.talk";
    /// <summary>The id of the <c>room.chat.shout</c> operation, which shouts a public message in the current room.</summary>
    public const string RoomChatShout = "room.chat.shout";
    /// <summary>The id of the <c>room.chat.whisper</c> operation, which whispers a private message to a user in the current room.</summary>
    public const string RoomChatWhisper = "room.chat.whisper";
    /// <summary>The id of the <c>room.chat.received</c> event, which publishes each talk, shout and whisper message received in the room.</summary>
    public const string RoomChatReceived = "room.chat.received";
    /// <summary>The id of the <c>room.variable_fx.state</c> query, which reads the Fx bar configurations and values of the current room.</summary>
    public const string RoomVariableFxState = "room.variable_fx.state";
    /// <summary>The id of the <c>room.variable_fx.changed</c> event, which publishes each Fx bar value that changes or is removed in the current room.</summary>
    public const string RoomVariableFxChanged = "room.variable_fx.changed";
    /// <summary>The id of the <c>room.avatar.walk</c> operation, which requests movement to a tile in the current room.</summary>
    public const string RoomAvatarWalk = "room.avatar.walk";
    /// <summary>The id of the <c>room.avatar.look</c> operation, which turns the local avatar toward a tile.</summary>
    public const string RoomAvatarLook = "room.avatar.look";
    /// <summary>The id of the <c>room.avatar.dance</c> operation, which sets the dance style of the local avatar.</summary>
    public const string RoomAvatarDance = "room.avatar.dance";
    /// <summary>The id of the <c>room.avatar.expression</c> operation, which plays an expression on the local avatar.</summary>
    public const string RoomAvatarExpression = "room.avatar.expression";
    /// <summary>The id of the <c>room.avatar.posture</c> operation, which sets the posture of the local avatar.</summary>
    public const string RoomAvatarPosture = "room.avatar.posture";
    /// <summary>The id of the <c>room.avatar.sign</c> operation, which raises a sign above the local avatar.</summary>
    public const string RoomAvatarSign = "room.avatar.sign";
    /// <summary>The id of the <c>room.avatar.effect</c> operation, which selects an effect for the local avatar.</summary>
    public const string RoomAvatarEffect = "room.avatar.effect";
    /// <summary>The id of the <c>room.avatar.typing.set</c> operation, which shows or hides the typing indicator of the local avatar.</summary>
    public const string RoomAvatarTyping = "room.avatar.typing.set";
    /// <summary>The id of the <c>room.item.floor.use</c> operation, which uses a floor item in the current room.</summary>
    public const string RoomItemFloorUse = "room.item.floor.use";
    /// <summary>The id of the <c>room.item.wall.use</c> operation, which uses a wall item in the current room.</summary>
    public const string RoomItemWallUse = "room.item.wall.use";
    /// <summary>The id of the <c>room.item.floor.click</c> operation, which clicks a floor item the way the client does, without using it.</summary>
    public const string RoomItemFloorClick = "room.item.floor.click";
    /// <summary>The id of the <c>room.item.wall.click</c> operation, which clicks a wall item the way the client does, without using it.</summary>
    public const string RoomItemWallClick = "room.item.wall.click";
    /// <summary>The id of the <c>room.item.one_way_door.enter</c> operation, which requests passage through a one-way door.</summary>
    public const string RoomItemOneWayDoorEnter = "room.item.one_way_door.enter";
    /// <summary>The id of the <c>room.item.dice.throw</c> operation, which throws a dice in the current room.</summary>
    public const string RoomItemDiceThrow = "room.item.dice.throw";
    /// <summary>The id of the <c>room.item.dice.clear</c> operation, which clears a dice to its blank state.</summary>
    public const string RoomItemDiceClear = "room.item.dice.clear";
    /// <summary>The id of the <c>room.item.wall.remove</c> operation, which deletes a wall item from the current room.</summary>
    public const string RoomItemWallRemove = "room.item.wall.remove";
    /// <summary>The id of the <c>room.item.sticky.set</c> operation, which replaces the color and text of a sticky note.</summary>
    public const string RoomItemStickySet = "room.item.sticky.set";
    /// <summary>The id of the <c>room.item.post_it.place</c> operation, which places an empty post-it on a room wall.</summary>
    public const string RoomItemPostItPlace = "room.item.post_it.place";
    /// <summary>The id of the <c>room.item.post_it.add</c> operation, which places a post-it with a color and text on a room wall.</summary>
    public const string RoomItemPostItAdd = "room.item.post_it.add";
    /// <summary>The id of the <c>room.enter</c> operation, which requests entry into a room.</summary>
    public const string RoomEnter = "room.enter";
    /// <summary>The id of the <c>room.leave</c> operation, which requests exit from the current room.</summary>
    public const string RoomLeave = "room.leave";
    /// <summary>The id of the <c>room.doorbell.answer</c> operation, which lets in or rejects a user waiting at the door of the current room.</summary>
    public const string RoomDoorbellAnswer = "room.doorbell.answer";
    /// <summary>The id of the <c>room.hand_item.drop</c> operation, which drops the hand item of the local avatar.</summary>
    public const string RoomHandItemDrop = "room.hand_item.drop";
    /// <summary>The id of the <c>room.hand_item.pass</c> operation, which passes the hand item of the local avatar to a room user.</summary>
    public const string RoomHandItemPass = "room.hand_item.pass";
    /// <summary>The id of the <c>room.rating.submit</c> operation, which submits a rating for the current room.</summary>
    public const string RoomRatingSubmit = "room.rating.submit";
    /// <summary>The id of the <c>room.staff_pick.set</c> operation, which adds a room to or removes it from the staff picks.</summary>
    public const string RoomStaffPickSet = "room.staff_pick.set";
    /// <summary>The id of the <c>room.people.respect</c> operation, which gives a respect to a user in the current room.</summary>
    public const string RoomPeopleRespect = "room.people.respect";
    /// <summary>The id of the <c>room.people.rights.grant</c> operation, which grants room rights to a user in the current room.</summary>
    public const string RoomPeopleRightsGrant = "room.people.rights.grant";
    /// <summary>The id of the <c>room.pet.respect</c> operation, which gives a respect to a pet in the current room.</summary>
    public const string RoomPetRespect = "room.pet.respect";
    /// <summary>The id of the <c>room.pet.mount.set</c> operation, which mounts or dismounts a pet in the current room.</summary>
    public const string RoomPetMountSet = "room.pet.mount.set";
    /// <summary>The id of the <c>room.pet.remove</c> operation, which returns a pet from the current room to the inventory.</summary>
    public const string RoomPetRemove = "room.pet.remove";
    /// <summary>The id of the <c>room.bot.remove</c> operation, which returns a bot from the current room to the inventory.</summary>
    public const string RoomBotRemove = "room.bot.remove";
    /// <summary>The id of the <c>room.placement.floor.place</c> operation, which places a floor item from the inventory without waiting for the hotel to accept it.</summary>
    public const string RoomPlacementFloorPlace = "room.placement.floor.place";
    /// <summary>The id of the <c>room.placement.wall.place</c> operation, which places a wall item from the inventory without waiting for the hotel to accept it.</summary>
    public const string RoomPlacementWallPlace = "room.placement.wall.place";
    /// <summary>The id of the <c>room.placement.floor.move</c> operation, which moves a floor item in the current room without waiting for the hotel to accept it.</summary>
    public const string RoomPlacementFloorMove = "room.placement.floor.move";
    /// <summary>The id of the <c>room.placement.wall.move</c> operation, which moves a wall item in the current room without waiting for the hotel to accept it.</summary>
    public const string RoomPlacementWallMove = "room.placement.wall.move";
    /// <summary>The id of the <c>room.placement.pickup</c> operation, which picks up an item in the current room without waiting for the hotel to accept it.</summary>
    public const string RoomPlacementPickup = "room.placement.pickup";
    /// <summary>The id of the <c>room.placement.changed</c> event, which publishes placement changes of single items and room lifecycle invalidations.</summary>
    public const string RoomPlacementChanged = "room.placement.changed";
    /// <summary>The id of the <c>room.placement.pickup_confirmation</c> event, which publishes the pickup confirmation prompt the Flash client shows.</summary>
    public const string RoomPlacementPickupConfirmation = "room.placement.pickup_confirmation";
    /// <summary>The id of the <c>room.moderation.state</c> query, which reads a page of the ban list of the current room.</summary>
    public const string RoomModerationState = "room.moderation.state";
    /// <summary>The id of the <c>room.moderation.refresh</c> operation, which requests the ban list of the current room and returns its first page.</summary>
    public const string RoomModerationRefresh = "room.moderation.refresh";
    /// <summary>The id of the <c>room.moderation.mute</c> operation, which mutes a user in the current room for a number of minutes.</summary>
    public const string RoomModerationMute = "room.moderation.mute";
    /// <summary>The id of the <c>room.moderation.kick</c> operation, which kicks a user from the current room.</summary>
    public const string RoomModerationKick = "room.moderation.kick";
    /// <summary>The id of the <c>room.moderation.ban</c> operation, which bans a user from the current room for an hour, a day or permanently.</summary>
    public const string RoomModerationBan = "room.moderation.ban";
    /// <summary>The id of the <c>room.moderation.unban</c> operation, which lifts the ban of a user in a given room.</summary>
    public const string RoomModerationUnban = "room.moderation.unban";
    /// <summary>The id of the <c>room.moderation.bounce</c> operation, which bans a user from the current room for an hour and then lifts the ban.</summary>
    public const string RoomModerationBounce = "room.moderation.bounce";
    /// <summary>The id of the <c>room.moderation.changed</c> event, which publishes changes to the ban list state.</summary>
    public const string RoomModerationChanged = "room.moderation.changed";
    /// <summary>The id of the <c>room.settings.state</c> query, which reads the cached settings of one owned room.</summary>
    public const string RoomSettingsState = "room.settings.state";
    /// <summary>The id of the <c>room.settings.get</c> operation, which loads the editable settings and read-only details of one owned room.</summary>
    public const string RoomSettingsGet = "room.settings.get";
    /// <summary>The id of the <c>room.settings.save</c> operation, which replaces the editable settings of one owned room and waits for the hotel answer.</summary>
    public const string RoomSettingsSave = "room.settings.save";
    /// <summary>The id of the <c>room.settings.changed</c> event, which publishes room settings refreshes, rejections, invalidations and save results.</summary>
    public const string RoomSettingsChanged = "room.settings.changed";
    /// <summary>The id of the <c>room.data.get</c> operation, which loads the navigator data of one room.</summary>
    public const string RoomDataGet = "room.data.get";
    /// <summary>The id of the <c>room.rights.list</c> operation, which loads the users with rights in one owned room.</summary>
    public const string RoomRightsList = "room.rights.list";
    /// <summary>The id of the <c>room.sticky.get</c> operation, which loads the color and text of one sticky note.</summary>
    public const string RoomStickyGet = "room.sticky.get";
    /// <summary>The id of the <c>pets.info.get</c> operation, which loads the statistics of one pet.</summary>
    public const string PetsInfoGet = "pets.info.get";
    /// <summary>The id of the <c>catalog.room_ad.info.get</c> operation, which loads the rooms eligible for a room advertisement and the membership flag.</summary>
    public const string CatalogRoomAdInfoGet = "catalog.room_ad.info.get";
    /// <summary>The id of the <c>profile.state</c> query, which reads the local account profile and what has been loaded.</summary>
    public const string ProfileState = "profile.state";
    /// <summary>The id of the <c>profile.refresh</c> operation, which loads the local account profile.</summary>
    public const string ProfileRefresh = "profile.refresh";
    /// <summary>The id of the <c>profile.blocks.list</c> query, which reads a page of the block list.</summary>
    public const string ProfileBlocksList = "profile.blocks.list";
    /// <summary>The id of the <c>profile.blocks.refresh</c> operation, which loads the complete block list and returns a page.</summary>
    public const string ProfileBlocksRefresh = "profile.blocks.refresh";
    /// <summary>The id of the <c>profile.block.add</c> operation, which adds a user to the block list.</summary>
    public const string ProfileBlockAdd = "profile.block.add";
    /// <summary>The id of the <c>profile.block.remove</c> operation, which removes a user from the block list.</summary>
    public const string ProfileBlockRemove = "profile.block.remove";
    /// <summary>The id of the <c>profile.ignores.list</c> query, which reads a page of the ignore list.</summary>
    public const string ProfileIgnoresList = "profile.ignores.list";
    /// <summary>The id of the <c>profile.ignores.refresh</c> operation, which loads the complete ignore list and returns a page.</summary>
    public const string ProfileIgnoresRefresh = "profile.ignores.refresh";
    /// <summary>The id of the <c>profile.ignore.add_by_id</c> operation, which adds a user id to the ignore list.</summary>
    public const string ProfileIgnoreAddById = "profile.ignore.add_by_id";
    /// <summary>The id of the <c>profile.ignore.remove</c> operation, which removes one user id or user name from the ignore list.</summary>
    public const string ProfileIgnoreRemove = "profile.ignore.remove";
    /// <summary>The id of the <c>profile.figure_sets.list</c> query, which reads pages of the owned figure sets and their furniture names.</summary>
    public const string ProfileFigureSetsList = "profile.figure_sets.list";
    /// <summary>The id of the <c>profile.sanctions.list</c> query, which reads a page of the account sanctions.</summary>
    public const string ProfileSanctionsList = "profile.sanctions.list";
    /// <summary>The id of the <c>profile.sanctions.refresh</c> operation, which loads the account sanction status.</summary>
    public const string ProfileSanctionsRefresh = "profile.sanctions.refresh";
    /// <summary>The id of the <c>profile.wardrobe.get</c> operation, which loads the wardrobe and returns pages from one snapshot.</summary>
    public const string ProfileWardrobeGet = "profile.wardrobe.get";
    /// <summary>The id of the <c>profile.motto.set</c> operation, which changes the motto of the local account.</summary>
    public const string ProfileMottoSet = "profile.motto.set";
    /// <summary>The id of the <c>profile.figure.set</c> operation, which changes the figure and gender of the local account.</summary>
    public const string ProfileFigureSet = "profile.figure.set";
    /// <summary>The id of the <c>profile.wardrobe.outfit.save</c> operation, which saves a figure and gender into a wardrobe slot.</summary>
    public const string ProfileWardrobeOutfitSave = "profile.wardrobe.outfit.save";
    /// <summary>The id of the <c>profile.favorite_group.select</c> operation, which selects the favorite group of the local account.</summary>
    public const string ProfileFavoriteGroupSelect = "profile.favorite_group.select";
    /// <summary>The id of the <c>profile.favorite_group.deselect</c> operation, which deselects the favorite group of the local account.</summary>
    public const string ProfileFavoriteGroupDeselect = "profile.favorite_group.deselect";
    /// <summary>The id of the <c>profile.changed</c> event, which publishes changes to the local profile, its lists, figure sets and sanctions.</summary>
    public const string ProfileChanged = "profile.changed";
    /// <summary>The id of the <c>profile.block.updated</c> event, which publishes the hotel results of block and unblock requests.</summary>
    public const string ProfileBlockUpdated = "profile.block.updated";
    /// <summary>The id of the <c>profile.ignore.updated</c> event, which publishes the hotel results of ignore requests.</summary>
    public const string ProfileIgnoreUpdated = "profile.ignore.updated";
    /// <summary>The id of the <c>people.profile.get</c> operation, which loads the profile of another user.</summary>
    public const string PeopleProfileGet = "people.profile.get";
    /// <summary>The id of the <c>people.relationship.get</c> operation, which loads the relationship summary of another user.</summary>
    public const string PeopleRelationshipGet = "people.relationship.get";
    /// <summary>The id of the <c>people.badges.get</c> operation, which loads the selected badges of another user.</summary>
    public const string PeopleBadgesGet = "people.badges.get";
    /// <summary>The id of the <c>people.profile.open</c> operation, which opens the profile of another user in the game client.</summary>
    public const string PeopleProfileOpen = "people.profile.open";
    /// <summary>The id of the <c>groups.details.get</c> operation, which loads the details of a group.</summary>
    public const string GroupsDetailsGet = "groups.details.get";
    /// <summary>The id of the <c>groups.members.page</c> operation, which loads one page of the members of a group.</summary>
    public const string GroupsMembersPage = "groups.members.page";
    /// <summary>The id of the <c>groups.memberships.get</c> operation, which loads or continues a page of group memberships from one snapshot.</summary>
    public const string GroupsMembershipsGet = "groups.memberships.get";
    /// <summary>The id of the <c>catalog.state</c> query, which reads the cache state of one catalog type.</summary>
    public const string CatalogState = "catalog.state";
    /// <summary>The id of the <c>catalog.index.get</c> operation, which loads the catalog index and returns it as a flat list.</summary>
    public const string CatalogIndexGet = "catalog.index.get";
    /// <summary>The id of the <c>catalog.page.get</c> operation, which loads one catalog page and its offers.</summary>
    public const string CatalogPageGet = "catalog.page.get";
    /// <summary>The id of the <c>catalog.pages.load</c> operation, which loads every page listed in the catalog index into the cache.</summary>
    public const string CatalogPagesLoad = "catalog.pages.load";
    /// <summary>The id of the <c>catalog.pages.list</c> query, which reads a page of the cached catalog page summaries.</summary>
    public const string CatalogPagesList = "catalog.pages.list";
    /// <summary>The id of the <c>catalog.offers.search</c> query, which searches the offers on the cached catalog pages.</summary>
    public const string CatalogOffersSearch = "catalog.offers.search";
    /// <summary>The id of the <c>catalog.cache.clear</c> operation, which clears the cache of one catalog type or of every type.</summary>
    public const string CatalogCacheClear = "catalog.cache.clear";
    /// <summary>The id of the <c>catalog.purchase.state</c> query, which reads the latest catalog purchase outcome.</summary>
    public const string CatalogPurchaseState = "catalog.purchase.state";
    /// <summary>The id of the <c>catalog.purchase.send</c> operation, which sends one catalog purchase without waiting for the outcome.</summary>
    public const string CatalogPurchaseSend = "catalog.purchase.send";
    /// <summary>The id of the <c>catalog.purchase.outcome</c> event, which publishes each catalog purchase outcome the hotel reports.</summary>
    public const string CatalogPurchaseOutcome = "catalog.purchase.outcome";
    /// <summary>The id of the <c>catalog.published</c> event, which publishes each catalog publication after the cache has been invalidated.</summary>
    public const string CatalogPublished = "catalog.published";
    /// <summary>The id of the <c>subscriptions.state</c> query, which reads the subscription products and the latest club offer, kickback and Builders Club data.</summary>
    public const string SubscriptionsState = "subscriptions.state";
    /// <summary>The id of the <c>subscriptions.club_offers.list</c> query, which reads a page of the last club offers received.</summary>
    public const string SubscriptionsClubOffersList = "subscriptions.club_offers.list";
    /// <summary>The id of the <c>subscriptions.club_offers.refresh</c> operation, which requests the club offers and returns their first page.</summary>
    public const string SubscriptionsClubOffersRefresh = "subscriptions.club_offers.refresh";
    /// <summary>The id of the <c>subscriptions.user_info.refresh</c> operation, which requests the subscription details of one product, such as <c>habbo_club</c>.</summary>
    public const string SubscriptionsUserInfoRefresh = "subscriptions.user_info.refresh";
    /// <summary>The id of the <c>subscriptions.kickback.refresh</c> operation, which requests the club kickback summary.</summary>
    public const string SubscriptionsKickbackRefresh = "subscriptions.kickback.refresh";
    /// <summary>The id of the <c>subscriptions.builders_club.furni_count.refresh</c> operation, which requests the Builders Club furniture count.</summary>
    public const string SubscriptionsBuildersClubFurniCountRefresh =
        "subscriptions.builders_club.furni_count.refresh";
    /// <summary>The id of the <c>subscriptions.builders_club.floor_offer.place</c> operation, which places a Builders Club floor offer in the current room.</summary>
    public const string SubscriptionsBuildersClubFloorOfferPlace =
        "subscriptions.builders_club.floor_offer.place";
    /// <summary>The id of the <c>subscriptions.builders_club.wall_offer.place</c> operation, which places a Builders Club wall offer in the current room.</summary>
    public const string SubscriptionsBuildersClubWallOfferPlace =
        "subscriptions.builders_club.wall_offer.place";
    /// <summary>The id of the <c>subscriptions.changed</c> event, which publishes subscription, kickback and Builders Club changes.</summary>
    public const string SubscriptionsChanged = "subscriptions.changed";
    /// <summary>The id of the <c>gifts.state</c> query, which reads a summary of the gift state.</summary>
    public const string GiftsState = "gifts.state";
    /// <summary>The id of the <c>gifts.wrapping.list</c> query, which reads a page of one gift wrapping list.</summary>
    public const string GiftsWrappingList = "gifts.wrapping.list";
    /// <summary>The id of the <c>gifts.club_info.list</c> query, which reads a page of one club gift list.</summary>
    public const string GiftsClubInfoList = "gifts.club_info.list";
    /// <summary>The id of the <c>gifts.club_selected.list</c> query, which reads a page of the last confirmed club gift selection.</summary>
    public const string GiftsClubSelectedList = "gifts.club_selected.list";
    /// <summary>The id of the <c>gifts.new_user_offer.list</c> query, which reads a page of one new user gift offer list.</summary>
    public const string GiftsNewUserOfferList = "gifts.new_user_offer.list";
    /// <summary>The id of the <c>gifts.refresh</c> operation, which reloads the gift wrapping configuration and the club gift information.</summary>
    public const string GiftsRefresh = "gifts.refresh";
    /// <summary>The id of the <c>gifts.present.open</c> operation, which opens a present placed in the current room.</summary>
    public const string GiftsPresentOpen = "gifts.present.open";
    /// <summary>The id of the <c>gifts.purchase</c> operation, which buys a catalog offer as a gift without waiting for the outcome.</summary>
    public const string GiftsPurchase = "gifts.purchase";
    /// <summary>The id of the <c>gifts.club.select</c> operation, which selects a club gift.</summary>
    public const string GiftsClubSelect = "gifts.club.select";
    /// <summary>The id of the <c>gifts.offer_giftability.refresh</c> operation, which asks whether a catalog offer can be sent as a gift.</summary>
    public const string GiftsOfferGiftabilityRefresh = "gifts.offer_giftability.refresh";
    /// <summary>The id of the <c>gifts.new_user.select</c> operation, which chooses gifts from the new user gift offer.</summary>
    public const string GiftsNewUserSelect = "gifts.new_user.select";
    /// <summary>The id of the <c>gifts.new_user.advance</c> operation, which advances the new user flow to its next step.</summary>
    public const string GiftsNewUserAdvance = "gifts.new_user.advance";
    /// <summary>The id of the <c>gifts.changed</c> event, which publishes changes to the gift state.</summary>
    public const string GiftsChanged = "gifts.changed";
    /// <summary>The id of the <c>crafting.state</c> query, which reads the crafting state.</summary>
    public const string CraftingState = "crafting.state";
    /// <summary>The id of the <c>crafting.products.list</c> query, which reads a page of the craftable products or usable furniture classes.</summary>
    public const string CraftingProductsList = "crafting.products.list";
    /// <summary>The id of the <c>crafting.recipe.list</c> query, which reads a page of the ingredients of the last recipe received.</summary>
    public const string CraftingRecipeList = "crafting.recipe.list";
    /// <summary>The id of the <c>crafting.products.refresh</c> operation, which requests the products a crafting furniture can craft.</summary>
    public const string CraftingProductsRefresh = "crafting.products.refresh";
    /// <summary>The id of the <c>crafting.recipe.refresh</c> operation, which requests the ingredients of a crafting recipe.</summary>
    public const string CraftingRecipeRefresh = "crafting.recipe.refresh";
    /// <summary>The id of the <c>crafting.availability.refresh</c> operation, which requests the recipes a set of ingredient items can craft.</summary>
    public const string CraftingAvailabilityRefresh =
        "crafting.availability.refresh";
    /// <summary>The id of the <c>crafting.craft</c> operation, which crafts a known recipe without waiting for the result.</summary>
    public const string CraftingCraft = "crafting.craft";
    /// <summary>The id of the <c>crafting.secret_craft</c> operation, which crafts a secret recipe from ingredient items without waiting for the result.</summary>
    public const string CraftingSecretCraft = "crafting.secret_craft";
    /// <summary>The id of the <c>crafting.changed</c> event, which publishes changes to the crafting state.</summary>
    public const string CraftingChanged = "crafting.changed";
    /// <summary>The id of the <c>achievements.state</c> query, which reads the achievement state.</summary>
    public const string AchievementsState = "achievements.state";
    /// <summary>The id of the <c>achievements.list</c> query, which reads a page of achievements.</summary>
    public const string AchievementsList = "achievements.list";
    /// <summary>The id of the <c>achievements.point_limits.list</c> query, which reads a page of badge point limits.</summary>
    public const string AchievementPointLimitsList = "achievements.point_limits.list";
    /// <summary>The id of the <c>achievements.refresh</c> operation, which reloads the achievement list from the server.</summary>
    public const string AchievementsRefresh = "achievements.refresh";
    /// <summary>The id of the <c>achievements.point_limits.refresh</c> operation, which reloads the badge point limits from the server.</summary>
    public const string AchievementPointLimitsRefresh =
        "achievements.point_limits.refresh";
    /// <summary>The id of the <c>achievements.changed</c> event, which publishes changes to the achievement state.</summary>
    public const string AchievementsChanged = "achievements.changed";
    /// <summary>The id of the <c>badges.state</c> query, which reads the badge state.</summary>
    public const string BadgesState = "badges.state";
    /// <summary>The id of the <c>badges.owned.list</c> query, which reads a page of the badges the user owns.</summary>
    public const string BadgesOwnedList = "badges.owned.list";
    /// <summary>The id of the <c>badges.selected_sets.list</c> query, which reads a page of the selected badge sets received in the session.</summary>
    public const string BadgesSelectedSetsList = "badges.selected_sets.list";
    /// <summary>The id of the <c>badges.selected.list</c> query, which reads a page of the selected badges of one user.</summary>
    public const string BadgesSelectedList = "badges.selected.list";
    /// <summary>The id of the <c>badges.refresh</c> operation, which reloads the badge inventory from the server.</summary>
    public const string BadgesRefresh = "badges.refresh";
    /// <summary>The id of the <c>badges.changed</c> event, which publishes changes to the badge state.</summary>
    public const string BadgesChanged = "badges.changed";
    /// <summary>The id of the <c>earnings.state</c> query, which reads the earnings vault state.</summary>
    public const string EarningsState = "earnings.state";
    /// <summary>The id of the <c>earnings.entries.list</c> query, which reads a page of earnings vault lines.</summary>
    public const string EarningsEntriesList = "earnings.entries.list";
    /// <summary>The id of the <c>earnings.refresh</c> operation, which reloads the earnings vault from the server.</summary>
    public const string EarningsRefresh = "earnings.refresh";
    /// <summary>The id of the <c>earnings.claim</c> operation, which claims the earnings of one category or of every category.</summary>
    public const string EarningsClaim = "earnings.claim";
    /// <summary>The id of the <c>earnings.changed</c> event, which publishes changes to the earnings vault.</summary>
    public const string EarningsChanged = "earnings.changed";
    /// <summary>The id of the <c>daily_tasks.state</c> query, which reads the daily task state.</summary>
    public const string DailyTasksState = "daily_tasks.state";
    /// <summary>The id of the <c>daily_tasks.entries.list</c> query, which reads a page of daily tasks.</summary>
    public const string DailyTasksEntriesList = "daily_tasks.entries.list";
    /// <summary>The id of the <c>daily_tasks.refresh</c> operation, which reloads the daily task list from the server.</summary>
    public const string DailyTasksRefresh = "daily_tasks.refresh";
    /// <summary>The id of the <c>daily_tasks.claim</c> operation, which claims the reward of a daily task without waiting for the result.</summary>
    public const string DailyTasksClaim = "daily_tasks.claim";
    /// <summary>The id of the <c>daily_tasks.changed</c> event, which publishes changes to the daily tasks.</summary>
    public const string DailyTasksChanged = "daily_tasks.changed";
    /// <summary>The id of the <c>quests.state</c> query, which reads the quest state.</summary>
    public const string QuestsState = "quests.state";
    /// <summary>The id of the <c>quests.entries.list</c> query, which reads a page of available, seasonal or combined quests.</summary>
    public const string QuestsEntriesList = "quests.entries.list";
    /// <summary>The id of the <c>quests.available.refresh</c> operation, which reloads the available quests from the server.</summary>
    public const string QuestsAvailableRefresh = "quests.available.refresh";
    /// <summary>The id of the <c>quests.seasonal.refresh</c> operation, which reloads the seasonal quests from the server.</summary>
    public const string QuestsSeasonalRefresh = "quests.seasonal.refresh";
    /// <summary>The id of the <c>quests.daily.refresh</c> operation, which requests a daily quest.</summary>
    public const string QuestsDailyRefresh = "quests.daily.refresh";
    /// <summary>The id of the <c>quests.accept</c> operation, which accepts a quest.</summary>
    public const string QuestsAccept = "quests.accept";
    /// <summary>The id of the <c>quests.activate</c> operation, which activates a quest.</summary>
    public const string QuestsActivate = "quests.activate";
    /// <summary>The id of the <c>quests.reject</c> operation, which rejects a quest.</summary>
    public const string QuestsReject = "quests.reject";
    /// <summary>The id of the <c>quests.cancel</c> operation, which cancels the active quest.</summary>
    public const string QuestsCancel = "quests.cancel";
    /// <summary>The id of the <c>quests.tracker.open</c> operation, which tells the hotel the quest tracker was opened.</summary>
    public const string QuestsTrackerOpen = "quests.tracker.open";
    /// <summary>The id of the <c>quests.friend_request.complete</c> operation, which reports progress on the friend request quest step.</summary>
    public const string QuestsFriendRequestComplete =
        "quests.friend_request.complete";
    /// <summary>The id of the <c>quests.changed</c> event, which publishes changes to the quests.</summary>
    public const string QuestsChanged = "quests.changed";
    /// <summary>The id of the <c>forums.state</c> query, which reads the forum data received in the session.</summary>
    public const string ForumsState = "forums.state";
    /// <summary>The id of the <c>forums.details.request</c> operation, which sends a forum details request without waiting for the answer.</summary>
    public const string ForumDetailsRequest = "forums.details.request";
    /// <summary>The id of the <c>forums.list.request</c> operation, which sends a forum directory page request without waiting for the answer.</summary>
    public const string ForumsListRequest = "forums.list.request";
    /// <summary>The id of the <c>forums.threads.request</c> operation, which sends a forum thread page request without waiting for the answer.</summary>
    public const string ForumThreadsRequest = "forums.threads.request";
    /// <summary>The id of the <c>forums.messages.request</c> operation, which sends a forum message page request without waiting for the answer.</summary>
    public const string ForumMessagesRequest = "forums.messages.request";
    /// <summary>The id of the <c>forums.thread.request</c> operation, which sends a forum thread request without waiting for the answer.</summary>
    public const string ForumThreadRequest = "forums.thread.request";
    /// <summary>The id of the <c>forums.unread.request</c> operation, which sends an unread forum count request without waiting for the answer.</summary>
    public const string ForumsUnreadRequest = "forums.unread.request";
    /// <summary>The id of the <c>forums.list.refresh</c> operation, which requests a page of the forum directory and waits for it.</summary>
    public const string ForumsListRefresh = "forums.list.refresh";
    /// <summary>The id of the <c>forums.threads.refresh</c> operation, which requests a page of forum threads and waits for it.</summary>
    public const string ForumThreadsRefresh = "forums.threads.refresh";
    /// <summary>The id of the <c>forums.messages.refresh</c> operation, which requests a page of thread messages and waits for it.</summary>
    public const string ForumMessagesRefresh = "forums.messages.refresh";
    /// <summary>The id of the <c>forums.details.refresh</c> operation, which requests the details of a group forum and waits for them.</summary>
    public const string ForumDetailsRefresh = "forums.details.refresh";
    /// <summary>The id of the <c>forums.thread.refresh</c> operation, which requests a single forum thread and waits for it.</summary>
    public const string ForumThreadRefresh = "forums.thread.refresh";
    /// <summary>The id of the <c>forums.unread.refresh</c> operation, which requests the number of forums with unread messages and waits for it.</summary>
    public const string ForumsUnreadRefresh = "forums.unread.refresh";
    /// <summary>The id of the <c>forums.post</c> operation, which posts a forum message or starts a new thread.</summary>
    public const string ForumsPost = "forums.post";
    /// <summary>The id of the <c>forums.thread.moderate</c> operation, which hides or restores a forum thread.</summary>
    public const string ForumThreadModerate = "forums.thread.moderate";
    /// <summary>The id of the <c>forums.message.moderate</c> operation, which hides or restores a forum message.</summary>
    public const string ForumMessageModerate = "forums.message.moderate";
    /// <summary>The id of the <c>forums.settings.update</c> operation, which changes the permission levels of a group forum.</summary>
    public const string ForumSettingsUpdate = "forums.settings.update";
    /// <summary>The id of the <c>forums.read_markers.update</c> operation, which updates the read markers of group forums.</summary>
    public const string ForumReadMarkersUpdate = "forums.read_markers.update";
    /// <summary>The id of the <c>forums.thread.update</c> operation, which changes the sticky and locked flags of a forum thread.</summary>
    public const string ForumThreadUpdate = "forums.thread.update";
    /// <summary>The id of the <c>forums.thread.report</c> operation, which reports a forum thread to the moderators.</summary>
    public const string ForumThreadReport = "forums.thread.report";
    /// <summary>The id of the <c>forums.message.report</c> operation, which reports a forum message to the moderators.</summary>
    public const string ForumMessageReport = "forums.message.report";
    /// <summary>The id of the <c>forums.changed</c> event, which publishes the forum data after each change.</summary>
    public const string ForumsChanged = "forums.changed";
    /// <summary>The id of the <c>leaderboards.state</c> query, which reads the state of one leaderboard.</summary>
    public const string LeaderboardsState = "leaderboards.state";
    /// <summary>The id of the <c>leaderboards.entries.list</c> query, which reads a page of leaderboard entries.</summary>
    public const string LeaderboardsEntriesList = "leaderboards.entries.list";
    /// <summary>The id of the <c>leaderboards.refresh</c> operation, which reloads one leaderboard from the server.</summary>
    public const string LeaderboardsRefresh = "leaderboards.refresh";
    /// <summary>The id of the <c>leaderboards.week_offset.set</c> operation, which sets the week offset of the weekly leaderboards.</summary>
    public const string LeaderboardsWeekOffsetSet = "leaderboards.week_offset.set";
    /// <summary>The id of the <c>leaderboards.changed</c> event, which publishes leaderboard changes.</summary>
    public const string LeaderboardsChanged = "leaderboards.changed";
    /// <summary>The id of the <c>habbicons.state</c> query, which reads the habbicon state.</summary>
    public const string HabbiconsState = "habbicons.state";
    /// <summary>The id of the <c>habbicons.collections.list</c> query, which reads a page of habbicon collections.</summary>
    public const string HabbiconCollectionsList = "habbicons.collections.list";
    /// <summary>The id of the <c>habbicons.entries.list</c> query, which reads a page of habbicons across all collections.</summary>
    public const string HabbiconEntriesList = "habbicons.entries.list";
    /// <summary>The id of the <c>habbicons.shop.refresh</c> operation, which reloads the habbicon shop from the server.</summary>
    public const string HabbiconShopRefresh = "habbicons.shop.refresh";
    /// <summary>The id of the <c>habbicons.info.refresh</c> operation, which requests the details of one habbicon.</summary>
    public const string HabbiconInfoRefresh = "habbicons.info.refresh";
    /// <summary>The id of the <c>habbicons.buy</c> operation, which buys one habbicon.</summary>
    public const string HabbiconBuy = "habbicons.buy";
    /// <summary>The id of the <c>habbicons.collection.buy</c> operation, which buys a whole habbicon collection.</summary>
    public const string HabbiconCollectionBuy = "habbicons.collection.buy";
    /// <summary>The id of the <c>habbicons.claim</c> operation, which claims an earned habbicon.</summary>
    public const string HabbiconClaim = "habbicons.claim";
    /// <summary>The id of the <c>habbicons.favorite</c> operation, which marks a habbicon as a favorite.</summary>
    public const string HabbiconFavorite = "habbicons.favorite";
    /// <summary>The id of the <c>habbicons.unfavorite</c> operation, which removes a habbicon from the favorites.</summary>
    public const string HabbiconUnfavorite = "habbicons.unfavorite";
    /// <summary>The id of the <c>habbicons.changed</c> event, which publishes changes to the habbicon state.</summary>
    public const string HabbiconsChanged = "habbicons.changed";
    /// <summary>The id of the <c>inventory.state</c> query, which reads the load state of the furni and pet inventories.</summary>
    public const string InventoryState = "inventory.state";
    /// <summary>The id of the <c>inventory.furni.list</c> query, which reads a page of the furni inventory.</summary>
    public const string InventoryFurniList = "inventory.furni.list";
    /// <summary>The id of the <c>inventory.furni.refresh</c> operation, which reloads the furni inventory and returns its first page.</summary>
    public const string InventoryFurniRefresh = "inventory.furni.refresh";
    /// <summary>The id of the <c>inventory.pets.list</c> query, which reads a page of the pet inventory.</summary>
    public const string InventoryPetsList = "inventory.pets.list";
    /// <summary>The id of the <c>inventory.pets.refresh</c> operation, which reloads the pet inventory and returns its first page.</summary>
    public const string InventoryPetsRefresh = "inventory.pets.refresh";
    /// <summary>The id of the <c>inventory.avatar_effect.activate</c> operation, which activates an avatar effect the user owns.</summary>
    public const string InventoryAvatarEffectActivate = "inventory.avatar_effect.activate";
    /// <summary>The id of the <c>inventory.furni.changed</c> event, which publishes changes to the furni inventory.</summary>
    public const string InventoryFurniChanged = "inventory.furni.changed";
    /// <summary>The id of the <c>inventory.pets.changed</c> event, which publishes changes to the pet inventory.</summary>
    public const string InventoryPetsChanged = "inventory.pets.changed";
    /// <summary>The id of the <c>wallet.state</c> query, which reads the credits and a page of activity point balances.</summary>
    public const string WalletState = "wallet.state";
    /// <summary>The id of the <c>wallet.refresh</c> operation, which refreshes the credits and returns the latest activity point balances.</summary>
    public const string WalletRefresh = "wallet.refresh";
    /// <summary>The id of the <c>wallet.changed</c> event, which publishes credit and activity point changes.</summary>
    public const string WalletChanged = "wallet.changed";
    /// <summary>The id of the <c>polls.state</c> query, which reads the latest poll offer, contents and error.</summary>
    public const string PollsState = "polls.state";
    /// <summary>The id of the <c>polls.start</c> operation, which sends a poll start request without waiting for the contents.</summary>
    public const string PollsStart = "polls.start";
    /// <summary>The id of the <c>polls.contents.get</c> operation, which starts a poll and waits for its contents.</summary>
    public const string PollsContentsGet = "polls.contents.get";
    /// <summary>The id of the <c>polls.reject</c> operation, which rejects a poll.</summary>
    public const string PollsReject = "polls.reject";
    /// <summary>The id of the <c>polls.answer</c> operation, which sends answers to a poll.</summary>
    public const string PollsAnswer = "polls.answer";
    /// <summary>The id of the <c>polls.changed</c> event, which publishes poll offer, contents, error and reset changes.</summary>
    public const string PollsChanged = "polls.changed";
    /// <summary>The id of the <c>trade.state</c> query, which reads the open trade and the trade NFT inventory summary.</summary>
    public const string TradeState = "trade.state";
    /// <summary>The id of the <c>trade.open</c> operation, which requests a trade with a user in the current room.</summary>
    public const string TradeOpen = "trade.open";
    /// <summary>The id of the <c>trade.items.add</c> operation, which adds inventory items to the trade offer.</summary>
    public const string TradeItemsAdd = "trade.items.add";
    /// <summary>The id of the <c>trade.item.remove</c> operation, which removes one inventory item from the trade offer.</summary>
    public const string TradeItemRemove = "trade.item.remove";
    /// <summary>The id of the <c>trade.accept</c> operation, which accepts the current trade offers.</summary>
    public const string TradeAccept = "trade.accept";
    /// <summary>The id of the <c>trade.unaccept</c> operation, which withdraws the acceptance of the open trade.</summary>
    public const string TradeUnaccept = "trade.unaccept";
    /// <summary>The id of the <c>trade.confirm</c> operation, which sends the final confirmation of a trade.</summary>
    public const string TradeConfirm = "trade.confirm";
    /// <summary>The id of the <c>trade.close</c> operation, which requests closing the open trade.</summary>
    public const string TradeClose = "trade.close";
    /// <summary>The id of the <c>trade.nft.inventory.list</c> query, which reads a page of the trade NFT inventory.</summary>
    public const string TradeNftInventoryList = "trade.nft.inventory.list";
    /// <summary>The id of the <c>trade.nft.inventory.refresh</c> operation, which loads the trade NFT inventory and returns its first page.</summary>
    public const string TradeNftInventoryRefresh = "trade.nft.inventory.refresh";
    /// <summary>The id of the <c>trade.changed</c> event, which publishes trade lifecycle changes.</summary>
    public const string TradeChanged = "trade.changed";
    /// <summary>The id of the <c>groups.membership.join</c> operation, which requests membership in a group.</summary>
    public const string GroupMembershipJoin = "groups.membership.join";
    /// <summary>The id of the <c>groups.membership.kick</c> operation, which removes a user from a group and can block rejoining.</summary>
    public const string GroupMembershipKick = "groups.membership.kick";
    /// <summary>The id of the <c>groups.membership.approve</c> operation, which approves a pending group membership request.</summary>
    public const string GroupMembershipApprove = "groups.membership.approve";
    /// <summary>The id of the <c>groups.membership.reject</c> operation, which rejects a pending group membership request.</summary>
    public const string GroupMembershipReject = "groups.membership.reject";
    /// <summary>The id of the <c>friends.list</c> query, which reads a filtered page of the friend list.</summary>
    public const string FriendsList = "friends.list";
    /// <summary>The id of the <c>friends.refresh</c> operation, which reloads the friend list and returns a filtered page.</summary>
    public const string FriendsRefresh = "friends.refresh";
    /// <summary>The id of the <c>friends.search</c> operation, which searches the hotel for users by name.</summary>
    public const string FriendsSearch = "friends.search";
    /// <summary>The id of the <c>friends.message.history</c> query, which reads a page of the private messages received in the session.</summary>
    public const string FriendMessageHistory = "friends.message.history";
    /// <summary>The id of the <c>friends.message.send</c> operation, which sends a private message to a friend.</summary>
    public const string FriendMessageSend = "friends.message.send";
    /// <summary>The id of the <c>friends.request.send</c> operation, which sends a friend request to a user by name.</summary>
    public const string FriendRequestSend = "friends.request.send";
    /// <summary>The id of the <c>friends.request.accept</c> operation, which accepts pending friend requests.</summary>
    public const string FriendRequestAccept = "friends.request.accept";
    /// <summary>The id of the <c>friends.request.decline</c> operation, which declines selected pending friend requests.</summary>
    public const string FriendRequestDecline = "friends.request.decline";
    /// <summary>The id of the <c>friends.requests.decline_all</c> operation, which declines every pending friend request.</summary>
    public const string FriendRequestsDeclineAll = "friends.requests.decline_all";
    /// <summary>The id of the <c>friends.requests.list</c> operation, which requests the pending friend requests.</summary>
    public const string FriendRequestsList = "friends.requests.list";
    /// <summary>The id of the <c>friends.remove</c> operation, which removes users from the friend list.</summary>
    public const string FriendsRemove = "friends.remove";
    /// <summary>The id of the <c>friends.follow</c> operation, which follows a friend to their current room.</summary>
    public const string FriendFollow = "friends.follow";
    /// <summary>The id of the <c>friends.relationship.set</c> operation, which changes the relationship marker shown for a friend.</summary>
    public const string FriendRelationshipSet = "friends.relationship.set";
    /// <summary>The id of the <c>friends.changed</c> event, which publishes friend list changes.</summary>
    public const string FriendsChanged = "friends.changed";
    /// <summary>The id of the <c>friends.message.received</c> event, which publishes each private message received.</summary>
    public const string FriendMessageReceived = "friends.message.received";
    /// <summary>The id of the <c>friends.message.failed</c> event, which publishes private message delivery failures.</summary>
    public const string FriendMessageFailed = "friends.message.failed";
    /// <summary>The id of the <c>friends.operation.failed</c> event, which publishes hotel rejections of messenger operations.</summary>
    public const string FriendOperationFailed = "friends.operation.failed";
    /// <summary>The id of the <c>friends.request.received</c> event, which publishes incoming friend requests.</summary>
    public const string FriendRequestReceived = "friends.request.received";
    /// <summary>The id of the <c>navigator.state</c> query, which reads the navigator metadata and personal settings.</summary>
    public const string NavigatorState = "navigator.state";
    /// <summary>The id of the <c>navigator.metadata.refresh</c> operation, which loads the navigator categories.</summary>
    public const string NavigatorMetadataRefresh = "navigator.metadata.refresh";
    /// <summary>The id of the <c>navigator.flat_categories.refresh</c> operation, which loads the room categories.</summary>
    public const string NavigatorFlatCategoriesRefresh = "navigator.flat_categories.refresh";
    /// <summary>The id of the <c>navigator.search.view</c> operation, which searches a navigator view with a filter.</summary>
    public const string NavigatorSearchView = "navigator.search.view";
    /// <summary>The id of the <c>navigator.search.text</c> operation, which searches rooms by text in the selected field.</summary>
    public const string NavigatorSearchText = "navigator.search.text";
    /// <summary>The id of the <c>navigator.search.my_rooms</c> operation, which loads the rooms owned by the user.</summary>
    public const string NavigatorSearchMyRooms = "navigator.search.my_rooms";
    /// <summary>The id of the <c>navigator.search.my_favourites</c> operation, which loads the favorite rooms of the user.</summary>
    public const string NavigatorSearchMyFavourites = "navigator.search.my_favourites";
    /// <summary>The id of the <c>navigator.search.my_room_rights</c> operation, which loads the rooms where the user has rights.</summary>
    public const string NavigatorSearchMyRoomRights = "navigator.search.my_room_rights";
    /// <summary>The id of the <c>navigator.search.my_history</c> operation, which loads the rooms the user visited recently.</summary>
    public const string NavigatorSearchMyHistory = "navigator.search.my_history";
    /// <summary>The id of the <c>navigator.search.my_frequent_history</c> operation, which loads the rooms the user visits most often.</summary>
    public const string NavigatorSearchMyFrequentHistory = "navigator.search.my_frequent_history";
    /// <summary>The id of the <c>navigator.search.my_friends_rooms</c> operation, which loads the rooms owned by friends of the user.</summary>
    public const string NavigatorSearchMyFriendsRooms = "navigator.search.my_friends_rooms";
    /// <summary>The id of the <c>navigator.search.friends_present</c> operation, which loads the rooms where friends of the user are present.</summary>
    public const string NavigatorSearchFriendsPresent = "navigator.search.friends_present";
    /// <summary>The id of the <c>navigator.search.my_guild_bases</c> operation, which loads the bases of the groups the user joined.</summary>
    public const string NavigatorSearchMyGuildBases = "navigator.search.my_guild_bases";
    /// <summary>The id of the <c>navigator.search.popular</c> operation, which loads popular rooms, optionally filtered by tag.</summary>
    public const string NavigatorSearchPopular = "navigator.search.popular";
    /// <summary>The id of the <c>navigator.search.highest_score</c> operation, which loads the highest scoring rooms.</summary>
    public const string NavigatorSearchHighestScore = "navigator.search.highest_score";
    /// <summary>The id of the <c>navigator.search.guild_bases</c> operation, which loads public group base rooms.</summary>
    public const string NavigatorSearchGuildBases = "navigator.search.guild_bases";
    /// <summary>The id of the <c>navigator.saved_search.add</c> operation, which adds a navigator search to the saved searches.</summary>
    public const string NavigatorSavedSearchAdd = "navigator.saved_search.add";
    /// <summary>The id of the <c>navigator.saved_search.delete</c> operation, which deletes a saved navigator search.</summary>
    public const string NavigatorSavedSearchDelete = "navigator.saved_search.delete";
    /// <summary>The id of the <c>navigator.category.collapse</c> operation, which collapses a navigator category.</summary>
    public const string NavigatorCategoryCollapse = "navigator.category.collapse";
    /// <summary>The id of the <c>navigator.category.expand</c> operation, which expands a collapsed navigator category.</summary>
    public const string NavigatorCategoryExpand = "navigator.category.expand";
    /// <summary>The id of the <c>navigator.room.create</c> operation, which creates a room owned by the user.</summary>
    public const string NavigatorRoomCreate = "navigator.room.create";
    /// <summary>The id of the <c>navigator.room.delete</c> operation, which deletes a room owned by the user.</summary>
    public const string NavigatorRoomDelete = "navigator.room.delete";
    /// <summary>The id of the <c>navigator.home_room.set</c> operation, which sets or clears the home room of the user.</summary>
    public const string NavigatorHomeRoomSet = "navigator.home_room.set";
    /// <summary>The id of the <c>navigator.changed</c> event, which publishes navigator metadata and personal setting changes.</summary>
    public const string NavigatorChanged = "navigator.changed";
    /// <summary>The id of the <c>navigator.search.received</c> event, which publishes each navigator search result received.</summary>
    public const string NavigatorSearchReceived = "navigator.search.received";
    /// <summary>The id of the <c>marketplace.state</c> query, which reads a page of the marketplace state.</summary>
    public const string MarketplaceState = "marketplace.state";
    /// <summary>The id of the <c>marketplace.configuration.refresh</c> operation, which loads the marketplace availability, prices, fees and limits.</summary>
    public const string MarketplaceConfigurationRefresh = "marketplace.configuration.refresh";
    /// <summary>The id of the <c>marketplace.eligibility.refresh</c> operation, which loads whether the user may create an offer.</summary>
    public const string MarketplaceEligibilityRefresh = "marketplace.eligibility.refresh";
    /// <summary>The id of the <c>marketplace.item_stats.get</c> operation, which loads the offers and sale statistics of one furniture type.</summary>
    public const string MarketplaceItemStatsGet = "marketplace.item_stats.get";
    /// <summary>The id of the <c>marketplace.search</c> operation, which searches the public marketplace offers.</summary>
    public const string MarketplaceSearch = "marketplace.search";
    /// <summary>The id of the <c>marketplace.own_offers.get</c> operation, which loads a page of the open, sold or expired offers of the user.</summary>
    public const string MarketplaceOwnOffersGet = "marketplace.own_offers.get";
    /// <summary>The id of the <c>marketplace.offer.make</c> operation, which lists inventory items for sale.</summary>
    public const string MarketplaceOfferMake = "marketplace.offer.make";
    /// <summary>The id of the <c>marketplace.offer.buy</c> operation, which buys an offer and waits for the hotel result.</summary>
    public const string MarketplaceOfferBuy = "marketplace.offer.buy";
    /// <summary>The id of the <c>marketplace.offer.buy.send</c> operation, which sends a purchase for a cached offer without waiting for the result.</summary>
    public const string MarketplaceOfferBuySend = "marketplace.offer.buy.send";
    /// <summary>The id of the <c>marketplace.offer.cancel</c> operation, which cancels one own offer and waits for the result.</summary>
    public const string MarketplaceOfferCancel = "marketplace.offer.cancel";
    /// <summary>The id of the <c>marketplace.offer.cancel.send</c> operation, which sends an offer cancellation without waiting for the result.</summary>
    public const string MarketplaceOfferCancelSend = "marketplace.offer.cancel.send";
    /// <summary>The id of the <c>marketplace.offers.cancel_all</c> operation, which cancels every open offer of the user.</summary>
    public const string MarketplaceOffersCancelAll = "marketplace.offers.cancel_all";
    /// <summary>The id of the <c>marketplace.history.clear</c> operation, which clears the sold and expired offer history of the user.</summary>
    public const string MarketplaceHistoryClear = "marketplace.history.clear";
    /// <summary>The id of the <c>marketplace.credits.redeem</c> operation, which collects the credits waiting on sold offers.</summary>
    public const string MarketplaceCreditsRedeem = "marketplace.credits.redeem";
    /// <summary>The id of the <c>marketplace.tokens.buy</c> operation, which buys a batch of marketplace listing tokens.</summary>
    public const string MarketplaceTokensBuy = "marketplace.tokens.buy";
    /// <summary>The id of the <c>marketplace.changed</c> event, which publishes each marketplace state revision.</summary>
    public const string MarketplaceChanged = "marketplace.changed";
    /// <summary>The id of the <c>marketplace.configuration.changed</c> event, which publishes each marketplace configuration received.</summary>
    public const string MarketplaceConfigurationChanged = "marketplace.configuration.changed";
    /// <summary>The id of the <c>marketplace.eligibility.changed</c> event, which publishes the latest marketplace eligibility of the user.</summary>
    public const string MarketplaceEligibilityChanged = "marketplace.eligibility.changed";
    /// <summary>The id of the <c>marketplace.search.received</c> event, which publishes the first page of each marketplace search result.</summary>
    public const string MarketplaceSearchReceived = "marketplace.search.received";
    /// <summary>The id of the <c>marketplace.own_offers.received</c> event, which publishes the first page of each own offer list received.</summary>
    public const string MarketplaceOwnOffersReceived = "marketplace.own_offers.received";
    /// <summary>The id of the <c>marketplace.item_stats.received</c> event, which publishes the sale statistics received for one furniture type.</summary>
    public const string MarketplaceItemStatsReceived = "marketplace.item_stats.received";
    /// <summary>The id of the <c>marketplace.offer.make_result</c> event, which publishes the hotel result of each listing request.</summary>
    public const string MarketplaceOfferMakeResult = "marketplace.offer.make_result";
    /// <summary>The id of the <c>marketplace.offer.buy_result</c> event, which publishes the hotel result of each marketplace purchase.</summary>
    public const string MarketplaceOfferBuyResult = "marketplace.offer.buy_result";
    /// <summary>The id of the <c>marketplace.offer.cancel_result</c> event, which publishes the hotel result of each offer cancellation.</summary>
    public const string MarketplaceOfferCancelResult = "marketplace.offer.cancel_result";
    /// <summary>The id of the <c>marketplace.offers.cancel_all_result</c> event, which publishes the result of each cancel all request.</summary>
    public const string MarketplaceOffersCancelAllResult = "marketplace.offers.cancel_all_result";
    /// <summary>The id of the <c>marketplace.history.clear_result</c> event, which publishes the result of each history clear request.</summary>
    public const string MarketplaceHistoryClearResult = "marketplace.history.clear_result";
    /// <summary>The id of the <c>wired.state</c> query, which reads the Wired state of the current room.</summary>
    public const string WiredState = "wired.state";
    /// <summary>The id of the <c>wired.configuration.open</c> operation, which requests the configuration of a Wired furni without waiting for it.</summary>
    public const string WiredConfigurationOpen = "wired.configuration.open";
    /// <summary>The id of the <c>wired.configuration.get</c> operation, which requests and returns the configuration of a Wired furni.</summary>
    public const string WiredConfigurationGet = "wired.configuration.get";

    /// <summary>The wired.form.get application member.</summary>
    public const string WiredFormGet = "wired.form.get";

    /// <summary>The wired.form.save application member.</summary>
    public const string WiredFormSave = "wired.form.save";

    /// <summary>The wired.form.definitions application member.</summary>
    public const string WiredFormDefinitions = "wired.form.definitions";

    /// <summary>The wired.fx.styles application member.</summary>
    public const string WiredFxStyles = "wired.fx.styles";

    /// <summary>The wired.area_hide.get application member.</summary>
    public const string WiredAreaHideGet = "wired.area_hide.get";

    /// <summary>The wired.area_hide.set application member.</summary>
    public const string WiredAreaHideSet = "wired.area_hide.set";

    /// <summary>The wired.area_hide.toggle application member.</summary>
    public const string WiredAreaHideToggle = "wired.area_hide.toggle";
    /// <summary>The id of the <c>wired.configuration.snapshot.apply</c> operation, which stores the current state of a Wired furni as its restore snapshot.</summary>
    public const string WiredConfigurationSnapshotApply = "wired.configuration.snapshot.apply";
    /// <summary>The id of the <c>wired.configuration.trigger.save</c> operation, which saves a trigger configuration and returns the hotel result.</summary>
    public const string WiredConfigurationTriggerSave = "wired.configuration.trigger.save";
    /// <summary>The id of the <c>wired.configuration.action.save</c> operation, which saves an action configuration and returns the hotel result.</summary>
    public const string WiredConfigurationActionSave = "wired.configuration.action.save";
    /// <summary>The id of the <c>wired.configuration.condition.save</c> operation, which saves a condition configuration and returns the hotel result.</summary>
    public const string WiredConfigurationConditionSave = "wired.configuration.condition.save";
    /// <summary>The id of the <c>wired.configuration.selector.save</c> operation, which saves a selector configuration and returns the hotel result.</summary>
    public const string WiredConfigurationSelectorSave = "wired.configuration.selector.save";
    /// <summary>The id of the <c>wired.configuration.addon.save</c> operation, which saves an add-on configuration and returns the hotel result.</summary>
    public const string WiredConfigurationAddonSave = "wired.configuration.addon.save";
    /// <summary>The id of the <c>wired.configuration.variable.save</c> operation, which saves a variable box configuration and returns the hotel result.</summary>
    public const string WiredConfigurationVariableSave = "wired.configuration.variable.save";
    /// <summary>The id of the <c>wired.variables.hash.get</c> operation, which returns the room hash used to detect Wired variable definition changes.</summary>
    public const string WiredVariablesHashGet = "wired.variables.hash.get";
    /// <summary>The id of the <c>wired.variables.differences.get</c> operation, which returns one chunk of Wired variable definition differences.</summary>
    public const string WiredVariablesDifferencesGet = "wired.variables.differences.get";
    /// <summary>The id of the <c>wired.variables.list</c> operation, which loads every Wired variable definition of the room.</summary>
    public const string WiredVariablesList = "wired.variables.list";
    /// <summary>The id of the <c>wired.variables.object.get</c> operation, which returns the Wired variable values held by a furni, a room user or the global scope.</summary>
    public const string WiredVariablesObjectGet = "wired.variables.object.get";
    /// <summary>The id of the <c>wired.variables.holders.get</c> operation, which returns a variable definition and every object that holds its value.</summary>
    public const string WiredVariablesHoldersGet = "wired.variables.holders.get";
    /// <summary>The id of the <c>wired.variables.holders.delete</c> operation, which removes a variable from every furni and user that holds it.</summary>
    public const string WiredVariablesHoldersDelete = "wired.variables.holders.delete";
    /// <summary>The id of the <c>wired.variables.permanent.get</c> operation, which returns the permanent variables of one entity.</summary>
    public const string WiredVariablesPermanentGet = "wired.variables.permanent.get";
    /// <summary>The id of the <c>wired.variables.owners.get</c> operation, which returns a page of the entities that own a permanent variable.</summary>
    public const string WiredVariablesOwnersGet = "wired.variables.owners.get";
    /// <summary>The id of the <c>wired.variables.object.set</c> operation, which writes, creates or deletes a variable value on a furni, user or the global scope.</summary>
    public const string WiredVariablesObjectSet = "wired.variables.object.set";
    /// <summary>The id of the <c>wired.variables.permanent.set</c> operation, which writes, creates or deletes a permanent variable and waits for the result.</summary>
    public const string WiredVariablesPermanentSet = "wired.variables.permanent.set";
    /// <summary>The id of the <c>wired.variables.permanent.set.send</c> operation, which sends a permanent variable update without waiting for the result.</summary>
    public const string WiredVariablesPermanentSetSend = "wired.variables.permanent.set.send";
    /// <summary>The id of the <c>wired.room.settings.get</c> operation, which returns the Wired permission masks and timezone of the room.</summary>
    public const string WiredRoomSettingsGet = "wired.room.settings.get";
    /// <summary>The id of the <c>wired.room.settings.set</c> operation, which replaces the Wired permission masks and timezone of the room.</summary>
    public const string WiredRoomSettingsSet = "wired.room.settings.set";
    /// <summary>The id of the <c>wired.room.stats.get</c> operation, which returns the Wired execution cost, furniture limits and permanent variable usage of the room.</summary>
    public const string WiredRoomStatsGet = "wired.room.stats.get";
    /// <summary>The id of the <c>wired.room.logs.get</c> operation, which returns a page of Wired room log entries.</summary>
    public const string WiredRoomLogsGet = "wired.room.logs.get";
    /// <summary>The id of the <c>wired.room.error_logs.get</c> operation, which returns the Wired execution errors of the room.</summary>
    public const string WiredRoomErrorLogsGet = "wired.room.error_logs.get";
    /// <summary>The id of the <c>wired.room.error_logs.clear</c> operation, which clears the Wired execution error counters of the room.</summary>
    public const string WiredRoomErrorLogsClear = "wired.room.error_logs.clear";
    /// <summary>The id of the <c>wired.room.user.click</c> operation, which asks the hotel whether the Wired user menu opens for a room index.</summary>
    public const string WiredRoomUserClick = "wired.room.user.click";
    /// <summary>The id of the <c>wired.room.reload</c> operation, which reloads the saved Wired state of the room without rolling back changes.</summary>
    public const string WiredRoomReload = "wired.room.reload";
    /// <summary>The id of the <c>wired.room.rollback</c> operation, which discards room changes made since the last saved Wired state.</summary>
    public const string WiredRoomRollback = "wired.room.rollback";
    /// <summary>The id of the <c>wired.preferences.set</c> operation, which updates the Wired menu, inspection, play test, notification and interface preferences.</summary>
    public const string WiredPreferencesSet = "wired.preferences.set";
    /// <summary>Reads account preferences retained during the hotel connection.</summary>
    public const string WiredPreferencesGet = "wired.preferences.get";
    /// <summary>Generates a Wired Web API key and waits for its correlated result.</summary>
    public const string WiredWebApiKeyGenerate = "wired.web_api.key.generate";
    /// <summary>The id of the <c>wired.chest.open</c> operation, which requests the contents of a Wired chest.</summary>
    public const string WiredChestOpen = "wired.chest.open";
    /// <summary>The id of the <c>wired.chest.close</c> operation, which closes a Wired chest.</summary>
    public const string WiredChestClose = "wired.chest.close";
    /// <summary>The id of the <c>wired.chests.lock</c> operation, which locks or unlocks the selected Wired chests in the room.</summary>
    public const string WiredChestsLock = "wired.chests.lock";
    /// <summary>The id of the <c>wired.chest.upgrade</c> operation, which buys capacity upgrades for a Wired chest.</summary>
    public const string WiredChestUpgrade = "wired.chest.upgrade";
    /// <summary>The id of the <c>wired.chest.withdraw_all</c> operation, which withdraws every item or coin from a Wired chest.</summary>
    public const string WiredChestWithdrawAll = "wired.chest.withdraw_all";
    /// <summary>The id of the <c>wired.chest.withdraw_coins</c> operation, which withdraws coins from a Wired chest.</summary>
    public const string WiredChestWithdrawCoins = "wired.chest.withdraw_coins";
    /// <summary>The id of the <c>wired.chest.withdraw_items</c> operation, which withdraws a number of items of one furniture type from a Wired chest.</summary>
    public const string WiredChestWithdrawItems = "wired.chest.withdraw_items";
    /// <summary>The id of the <c>wired.chest.add.start</c> operation, which starts the chest trade used to deposit inventory items.</summary>
    public const string WiredChestAddStart = "wired.chest.add.start";
    /// <summary>The id of the <c>wired.chest.options.set</c> operation, which updates the lock, auto lock and capacity options of a Wired chest.</summary>
    public const string WiredChestOptionsSet = "wired.chest.options.set";
    /// <summary>The id of the <c>wired.chest.preferences.set</c> operation, which updates the name, description, state and preview preferences of a Wired chest.</summary>
    public const string WiredChestPreferencesSet = "wired.chest.preferences.set";
    /// <summary>The id of the <c>wired.chest.notification_preferences.set</c> operation, which updates the notification and event flags of a Wired chest.</summary>
    public const string WiredChestNotificationPreferencesSet = "wired.chest.notification_preferences.set";
    /// <summary>The id of the <c>wired.chest.deposit</c> operation, which deposits inventory items into a Wired chest through a complete chest trade.</summary>
    public const string WiredChestDeposit = "wired.chest.deposit";
    /// <summary>The id of the <c>wired.transaction.chest_logs.get</c> operation, which returns a page of the transactions of a chest.</summary>
    public const string WiredTransactionChestLogsGet = "wired.transaction.chest_logs.get";
    /// <summary>The id of the <c>wired.transaction.room_logs.get</c> operation, which returns a page of the Wired transactions of the room.</summary>
    public const string WiredTransactionRoomLogsGet = "wired.transaction.room_logs.get";
    /// <summary>The id of the <c>wired.transaction.details.get</c> operation, which returns the chest and furniture details of one transaction.</summary>
    public const string WiredTransactionDetailsGet = "wired.transaction.details.get";
    /// <summary>The id of the <c>wired.contract.open</c> operation, which requests and returns the contents of a Wired contract.</summary>
    public const string WiredContractOpen = "wired.contract.open";
    /// <summary>The id of the <c>wired.contract.open.send</c> operation, which requests a Wired contract without waiting for its contents.</summary>
    public const string WiredContractOpenSend = "wired.contract.open.send";
    /// <summary>The id of the <c>wired.contract.update</c> operation, which replaces a Wired contract and waits for the result.</summary>
    public const string WiredContractUpdate = "wired.contract.update";
    /// <summary>The id of the <c>wired.contract.update.send</c> operation, which sends a contract update without waiting for the result.</summary>
    public const string WiredContractUpdateSend = "wired.contract.update.send";
    /// <summary>The id of the <c>wired.trade.items.add</c> operation, which adds inventory items to the active Wired chest trade.</summary>
    public const string WiredTradeItemsAdd = "wired.trade.items.add";
    /// <summary>The id of the <c>wired.trade.items.remove</c> operation, which removes inventory items from the active Wired chest trade.</summary>
    public const string WiredTradeItemsRemove = "wired.trade.items.remove";
    /// <summary>The id of the <c>wired.trade.confirm</c> operation, which updates the confirmation of the active Wired chest trade.</summary>
    public const string WiredTradeConfirm = "wired.trade.confirm";
    /// <summary>The id of the operation that accepts and completes an unchanged reviewed Wired trade.</summary>
    public const string WiredTradeComplete = "wired.trade.complete";
    /// <summary>The id of the operation that sends an explicitly named Wired confirmation stage.</summary>
    public const string WiredTradeStageSend = "wired.trade.stage.send";
    /// <summary>The id of the <c>wired.trade.cancel</c> operation, which cancels the active Wired chest trade.</summary>
    public const string WiredTradeCancel = "wired.trade.cancel";
    /// <summary>The id of the <c>wired.changed</c> event, which publishes each Wired state revision.</summary>
    public const string WiredChanged = "wired.changed";
    /// <summary>The id of the <c>wired.permissions.changed</c> event, which publishes the latest Wired read and modify permissions of the user.</summary>
    public const string WiredPermissionsChanged = "wired.permissions.changed";
    /// <summary>The id of the <c>wired.environment.changed</c> event, which publishes the Wired environment of the room and its enabled achievements.</summary>
    public const string WiredEnvironmentChanged = "wired.environment.changed";
    /// <summary>The id of the <c>wired.click_settings.changed</c> event, which publishes the latest Wired user and furniture click options.</summary>
    public const string WiredClickSettingsChanged = "wired.click_settings.changed";
    /// <summary>The id of the <c>wired.room.settings.changed</c> event, which publishes the latest Wired permission masks and timezone of the room.</summary>
    public const string WiredRoomSettingsChanged = "wired.room.settings.changed";
    /// <summary>The id of the <c>wired.configuration.opened</c> event, which publishes the furni id named by each configuration open message.</summary>
    public const string WiredConfigurationOpened = "wired.configuration.opened";
    /// <summary>The id of the <c>wired.configuration.received</c> event, which publishes each Wired configuration received.</summary>
    public const string WiredConfigurationReceived = "wired.configuration.received";
    /// <summary>The id of the <c>wired.configuration.save_result</c> event, which publishes each configuration save success or validation failure.</summary>
    public const string WiredConfigurationSaveResult = "wired.configuration.save_result";
    /// <summary>The id of the <c>wired.menu.error</c> event, which publishes the Wired menu error codes the hotel reports.</summary>
    public const string WiredMenuError = "wired.menu.error";
    /// <summary>The id of the <c>wired.reward.result</c> event, which publishes the reason of each Wired reward result.</summary>
    public const string WiredRewardResult = "wired.reward.result";
    /// <summary>The id of the <c>wired.chest.opened</c> event, which publishes hotel requests to open a Wired chest.</summary>
    public const string WiredChestOpened = "wired.chest.opened";
    /// <summary>The id of the <c>wired.chest.coins.received</c> event, which publishes the coin contents of a Wired chest.</summary>
    public const string WiredChestCoinsReceived = "wired.chest.coins.received";
    /// <summary>The id of the <c>wired.chest.items.chunk_received</c> event, which publishes one fragment of the item contents of a Wired chest.</summary>
    public const string WiredChestItemsChunkReceived = "wired.chest.items.chunk_received";
    /// <summary>The id of the <c>wired.chest.items.updated</c> event, which publishes the items removed from and added to a Wired chest.</summary>
    public const string WiredChestItemsUpdated = "wired.chest.items.updated";
    /// <summary>The id of the <c>wired.chest.upgrade_result</c> event, which publishes the result of each chest capacity upgrade.</summary>
    public const string WiredChestUpgradeResult = "wired.chest.upgrade_result";
    /// <summary>The id of the <c>wired.chest.preferences.updated</c> event, which publishes confirmed chest preference updates.</summary>
    public const string WiredChestPreferencesUpdated = "wired.chest.preferences.updated";
    /// <summary>The id of the <c>wired.transaction.succeeded</c> event, which publishes successful Wired transactions and their rewards.</summary>
    public const string WiredTransactionSucceeded = "wired.transaction.succeeded";
    /// <summary>The id of the <c>wired.transaction.failed</c> event, which publishes the failure type of each failed Wired transaction.</summary>
    public const string WiredTransactionFailed = "wired.transaction.failed";
    /// <summary>The id of the <c>wired.contract.contents.received</c> event, which publishes each Wired contract definition received.</summary>
    public const string WiredContractContentsReceived = "wired.contract.contents.received";
    /// <summary>The id of the <c>wired.contract.opened</c> event, which publishes hotel requests to open the contract editor.</summary>
    public const string WiredContractOpened = "wired.contract.opened";
    /// <summary>The id of the <c>wired.contract.update_result</c> event, which publishes the result code of each contract update.</summary>
    public const string WiredContractUpdateResult = "wired.contract.update_result";
    /// <summary>The id of the <c>wired.trade.initiated</c> event, which publishes the requirement and timeout of each new Wired chest trade.</summary>
    public const string WiredTradeInitiated = "wired.trade.initiated";
    /// <summary>The id of the <c>wired.trade.items.updated</c> event, which publishes the item offers and acceptance state of the active Wired chest trade.</summary>
    public const string WiredTradeItemsUpdated = "wired.trade.items.updated";
    /// <summary>The id of the <c>wired.trade.cancelled</c> event, which publishes the failure type of a canceled Wired chest trade.</summary>
    public const string WiredTradeCancelled = "wired.trade.cancelled";
    /// <summary>The id of the <c>wired.trade.completed</c> event, which publishes the completion of the active Wired chest trade.</summary>
    public const string WiredTradeCompleted = "wired.trade.completed";
    /// <summary>The id of the <c>wired.trade.notification</c> event, which publishes Wired trade transaction notifications.</summary>
    public const string WiredTradeNotification = "wired.trade.notification";
}
