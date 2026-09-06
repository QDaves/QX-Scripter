using System.Windows;
using System.Windows.Controls;

namespace Qx.Ui;

/// <summary>Which clients a piece of the interface works on.</summary>
public enum ClientSupport
{
    /// <summary>Works on Flash and on Unity. Everything should be this.</summary>
    Both,

    /// <summary>Proven on Flash only. Hidden or disabled on a Unity session.</summary>
    FlashOnly,

    /// <summary>Proven on Unity only.</summary>
    UnityOnly
}

/// <summary>What a control does when the connected client cannot do what it offers.</summary>
public enum UnsupportedBehaviour
{
    /// <summary>Stays visible, greyed out, and says why on hover. The honest default.</summary>
    Disable,

    /// <summary>Disappears. For whole tabs and sections that would otherwise be empty.</summary>
    Hide
}

/// <summary>
/// Marks a control with the clients it works on, and enforces it.
/// </summary>
/// <remarks>
/// <para>
/// QX talks to two clients whose wire formats genuinely differ, and a function proven on one is not
/// thereby proven on the other. Left to discipline alone that goes wrong: a Flash-only path stays
/// reachable on a Unity session, throws somewhere deep in a parser, and the interface reports
/// nothing — which is exactly how a marketplace handler spent a day failing in silence.
/// </para>
/// <para>
/// So the claim is written on the control itself. <c>Requires="FlashOnly"</c> is a statement that
/// this action has only been proven for Flash, and the window makes that true by disabling it
/// elsewhere and saying so. A control with no marking claims both, which is what everything should
/// eventually claim.
/// </para>
/// </remarks>
public static class ClientCapability
{
    /// <summary>The connected client, pushed in by the window when a session starts or ends.</summary>
    public static readonly DependencyProperty ClientProperty =
        DependencyProperty.RegisterAttached(
            "Client",
            typeof(ClientType),
            typeof(ClientCapability),
            new FrameworkPropertyMetadata(
                ClientType.None,
                FrameworkPropertyMetadataOptions.Inherits,
                OnClientChanged),
            IsClient);

    public static void SetClient(DependencyObject element, ClientType value) =>
        element.SetValue(ClientProperty, value);

    public static ClientType GetClient(DependencyObject element) =>
        (ClientType)element.GetValue(ClientProperty);

    /// <summary>What this control needs from the connected client.</summary>
    public static readonly DependencyProperty RequiresProperty =
        DependencyProperty.RegisterAttached(
            "Requires",
            typeof(ClientSupport),
            typeof(ClientCapability),
            new PropertyMetadata(ClientSupport.Both, OnRequiresChanged),
            IsRequirement);

    public static void SetRequires(DependencyObject element, ClientSupport value) =>
        element.SetValue(RequiresProperty, value);

    public static ClientSupport GetRequires(DependencyObject element) =>
        (ClientSupport)element.GetValue(RequiresProperty);

    /// <summary>Whether an unsupported control greys out or disappears.</summary>
    public static readonly DependencyProperty WhenUnsupportedProperty =
        DependencyProperty.RegisterAttached(
            "WhenUnsupported",
            typeof(UnsupportedBehaviour),
            typeof(ClientCapability),
            new PropertyMetadata(UnsupportedBehaviour.Disable, OnRequiresChanged),
            IsUnsupportedBehaviour);

    public static void SetWhenUnsupported(DependencyObject element, UnsupportedBehaviour value) =>
        element.SetValue(WhenUnsupportedProperty, value);

    public static UnsupportedBehaviour GetWhenUnsupported(DependencyObject element) =>
        (UnsupportedBehaviour)element.GetValue(WhenUnsupportedProperty);

    /// <summary>Whether <paramref name="client"/> can do what <paramref name="requires"/> asks.</summary>
    /// <remarks>
    /// Nothing is refused while no session is open. Before a client is known there is nothing to
    /// contradict, and greying out the whole window until someone connects helps no one.
    /// </remarks>
    public static bool IsSupported(ClientSupport requires, ClientType client) => client switch
    {
        ClientType.None => requires is ClientSupport.Both or ClientSupport.FlashOnly or ClientSupport.UnityOnly,
        ClientType.Flash => requires is ClientSupport.Both or ClientSupport.FlashOnly,
        ClientType.Unity => requires is ClientSupport.Both or ClientSupport.UnityOnly,
        _ => false
    };

    /// <summary>The sentence shown on hover when something is out of reach.</summary>
    public static string Explain(ClientSupport requires) => requires switch
    {
        ClientSupport.FlashOnly => "Only proven for the Flash client, so it is not offered on Unity.",
        ClientSupport.UnityOnly => "Only proven for the Unity client, so it is not offered on Flash.",
        _ => ""
    };

    private static bool IsClient(object value) =>
        value is ClientType client && client is ClientType.None or ClientType.Flash or ClientType.Unity;

    private static bool IsRequirement(object value) =>
        value is ClientSupport requires &&
        requires is ClientSupport.Both or ClientSupport.FlashOnly or ClientSupport.UnityOnly;

    private static bool IsUnsupportedBehaviour(object value) =>
        value is UnsupportedBehaviour behaviour &&
        behaviour is UnsupportedBehaviour.Disable or UnsupportedBehaviour.Hide;

    private static void OnClientChanged(DependencyObject element, DependencyPropertyChangedEventArgs e) =>
        Apply(element);

    private static void OnRequiresChanged(DependencyObject element, DependencyPropertyChangedEventArgs e) =>
        Apply(element);

    private static void Apply(DependencyObject element)
    {
        ClientSupport requires = GetRequires(element);
        if (requires is ClientSupport.Both)
            return;

        if (element is not UIElement target)
            return;

        bool supported = IsSupported(requires, GetClient(element));

        if (GetWhenUnsupported(element) is UnsupportedBehaviour.Hide)
        {
            target.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        target.IsEnabled = supported;
        if (element is FrameworkElement framework)
        {
            // Kept live on the disabled control so the reason is readable rather than guessable.
            ToolTipService.SetShowOnDisabled(framework, true);
            if (!supported)
                framework.ToolTip = Explain(requires);
            else if (ReferenceEquals(framework.ToolTip, Explain(requires)))
                framework.ToolTip = null;
        }
    }
}
