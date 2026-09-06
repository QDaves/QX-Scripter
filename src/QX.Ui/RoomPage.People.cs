using System.Windows;
using System.Windows.Controls;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;

namespace Qx.Ui;

/// <summary>
/// What you can do to somebody from the room lists.
/// </summary>
/// <remarks>
/// <para>
/// The same menu serves the people who are here and the people who have been, because both are
/// users and what you want from one you want from the other. What is offered depends on what the
/// row actually is and on whether you own the room, so nothing on it silently does nothing.
/// </para>
/// </remarks>
public partial class RoomPage
{
    /// <summary>Whether bots and pets appear beside the people. Off shows only users.</summary>
    private bool _showBots = true;
    private bool _showPets = true;

    private DataGrid PeopleGrid =>
        ReferenceEquals(Tabs.SelectedItem, VisitorsTab) ? VisitorsList : UsersList;

    private RoomEntry[] PeopleSelection() =>
        [.. PeopleGrid.SelectedItems.OfType<RoomEntry>()];

    private RoomEntry? OnePerson() => PeopleSelection() is [RoomEntry only] ? only : null;

    private Id? SelfId() => Application?
        .Invoke<ProfileStateRequest, ProfileStateView>(
            ApplicationMemberIds.ProfileState,
            new ProfileStateRequest())
        .Identity?
        .Id;

    /// <summary>Finds one entry of the shared menu by what it says, since a menu in a resource dictionary has no generated fields.</summary>
    private static MenuItem? Entry(ContextMenu menu, string header) =>
        menu.Items.OfType<MenuItem>().FirstOrDefault(item =>
            item.Header as string == header);

    private void PeopleMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid grid || grid.ContextMenu is not { } menu)
            return;

        RoomEntry[] picked = [.. grid.SelectedItems.OfType<RoomEntry>()];
        bool one = picked.Length == 1;
        bool isUser = one && picked[0].Person is User or null;
        bool inRoom = one && picked[0].Person is not null;
        bool owner = _game?.Room.IsOwner == true;
        bool self = one && SelfId() == picked[0].EntityId;
        bool can_trade = one && !self && TradeAvailable(picked[0]);
        bool can_moderate = owner && one && !self && ModerationAvailable(picked[0]);

        // Finding somebody needs them to be standing here; a visitor who has left has no index to
        // put a bubble on. Trading needs the same, and neither is any use aimed at yourself.
        Enable(menu, "Find in the room", inRoom && !self);
        Enable(menu, "Trade", can_trade);
        Enable(menu, "Send a friend request", one && isUser && !self);
        Enable(menu, "Open profile", one && isUser && !self);
        Enable(menu, "Add outfit to my wardrobe", one && picked[0].Person is User { Figure.Length: > 0 });
        Enable(menu, "Moderate", can_moderate);

        if (Entry(menu, "Show bots") is { } bots)
            bots.IsChecked = _showBots;
        if (Entry(menu, "Show pets") is { } pets)
            pets.IsChecked = _showPets;
    }

    private static void Enable(ContextMenu menu, string header, bool enabled)
    {
        if (Entry(menu, header) is { } item)
            item.IsEnabled = enabled;
    }

    private void PeopleFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item)
            return;

        if (item.Header as string == "Show bots")
            _showBots = item.IsChecked;
        else
            _showPets = item.IsChecked;

        Apply();
    }

    private void FindPerson(object sender, RoutedEventArgs e)
    {
        if (OnePerson()?.Person is { } avatar)
            _game?.People.Find(avatar);
    }

    private void AddFriend(object sender, RoutedEventArgs e)
    {
        if (OnePerson() is { } row && Application is { } application)
        {
            application.Invoke<FriendRequestSendRequest, FriendOperationResult>(
                ApplicationMemberIds.FriendRequestSend,
                new FriendRequestSendRequest(row.Name));
        }
    }

    private void TradePerson(object sender, RoutedEventArgs e)
    {
        if (OnePerson() is not { } row ||
            CurrentTradeTarget(row) is not { } target ||
            Application is not { } application)
        {
            return;
        }
        try
        {
            TradeStateView trade = application.Invoke<TradeStateRequest, TradeStateView>(
                ApplicationMemberIds.TradeState,
                new TradeStateRequest());
            application.Invoke<TradeOpenRequest, TradeDispatchResult>(
                ApplicationMemberIds.TradeOpen,
                new TradeOpenRequest(
                    target.Index,
                    trade.SessionGeneration,
                    trade.Revision,
                    trade.LatestEpoch,
                    trade.RoomGeneration,
                    target.Id));
        }
        catch (Exception error)
        {
            Qx.Diagnostics.Diag.Warn($"Could not open trade: {error.Message}", "ui");
        }
    }

    private bool TradeAvailable(RoomEntry row)
    {
        if (CurrentTradeTarget(row) is null || Application is not { } application)
            return false;
        try
        {
            return application.Describe(ApplicationMemberIds.TradeOpen).Availability.Available;
        }
        catch
        {
            return false;
        }
    }

    private User? CurrentTradeTarget(RoomEntry row)
    {
        if (_game is not { } game || row.Person is not User)
            return null;
        return game.Room.Capture(current_room =>
            current_room.IsReady &&
            current_room.AvatarByIndex(row.Index) is User current &&
            current.Id == row.EntityId
                ? current
                : null);
    }

    private bool ModerationAvailable(RoomEntry row)
    {
        try
        {
            (IApplicationRuntime application, _) = ModerationTarget(row);
            return new[]
            {
                ApplicationMemberIds.RoomModerationMute,
                ApplicationMemberIds.RoomModerationKick,
                ApplicationMemberIds.RoomModerationBan,
                ApplicationMemberIds.RoomModerationBounce
            }.All(id => application.Describe(id).Availability.Available);
        }
        catch
        {
            return false;
        }
    }

    private (IApplicationRuntime application, RoomModerationStateView state) ModerationTarget(
        RoomEntry row)
    {
        if (Application is not { } application ||
            row.Person is not User user ||
            row.Index < 0 ||
            user.Id != row.EntityId ||
            user.Index != row.Index)
        {
            throw new InvalidOperationException("The selected row is not a current room user.");
        }
        RoomModerationStateView state = application.Invoke<
            RoomModerationStateRequest,
            RoomModerationStateView>(
                ApplicationMemberIds.RoomModerationState,
                new RoomModerationStateRequest());
        if (!state.RoomReady ||
            state.RoomId <= 0 ||
            state.RoomGeneration != row.RoomGeneration)
        {
            throw new InvalidOperationException("The selected user is no longer in the current room.");
        }
        return (application, state);
    }

    private void OpenPersonProfile(object sender, RoutedEventArgs e)
    {
        if (OnePerson() is { } row)
            _game?.People.OpenProfile(row.EntityId);
    }

    /// <summary>
    /// Copies part of what somebody else is doing onto yourself.
    /// </summary>
    /// <remarks>
    /// Which part comes from the menu entry rather than from a dialog, because every one of these is
    /// a single thing to do and putting a window in front of it would cost more clicks than the act
    /// itself. What actually went out is reported back: a person standing still has no dance to
    /// copy, and saying so is better than a menu that appears to do nothing.
    /// </remarks>
    private void MimicPerson(object sender, RoutedEventArgs e)
    {
        if (OnePerson()?.Person is not { } avatar || _game is not { } game)
            return;
        if (sender is not FrameworkElement { Tag: string part })
            return;

        MimicParts wanted = part switch
        {
            "figure" => MimicParts.Figure,
            "motto" => MimicParts.Motto,
            "dance" => MimicParts.Dance,
            "sign" => MimicParts.Sign,
            "effect" => MimicParts.Effect,
            "direction" => MimicParts.Direction,
            "walk" => MimicParts.Walk,
            _ => MimicParts.Appearance | MimicParts.Walk
        };

        MimicParts done = game.Mimic.Copy(avatar, wanted);
        FurniStatus.Text = done == MimicParts.None
            ? $"{avatar.Name} has nothing there to copy."
            : $"Copied {Describe(done)} from {avatar.Name}.";
    }

    private static string Describe(MimicParts parts)
    {
        string[] named =
        [
            .. Enum.GetValues<MimicParts>()
                .Where(part =>
                    part is not (MimicParts.None or MimicParts.Appearance or
                        MimicParts.Behaviour or MimicParts.All) &&
                    parts.HasFlag(part))
                .Select(part => part.ToString().ToLowerInvariant())
        ];
        return named.Length switch
        {
            0 => "nothing",
            1 => named[0],
            _ => $"{string.Join(", ", named[..^1])} and {named[^1]}"
        };
    }

    /// <summary>Keeps somebody else's outfit on your own shelf.</summary>
    private void AddToWardrobe(object sender, RoutedEventArgs e)
    {
        if (OnePerson()?.Person is not User user || user.Figure.Length == 0)
            return;

        OutfitStore store = OutfitStore.Shared;
        string gender = user.Gender.ToString() is { Length: > 0 } value
            ? value[..1].ToUpperInvariant()
            : "M";

        FurniStatus.Text = store.Add(new SavedOutfit(user.Figure, gender, user.Name))
            ? $"Kept {user.Name}'s outfit in your wardrobe."
            : "You are already keeping that outfit.";
    }

    private void CopyPersonField(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string field } || OnePerson() is not { } row)
            return;

        string text = field switch
        {
            "id" => row.EntityId.ToString(),
            "motto" => row.Person?.Motto ?? row.Detail,
            "figure" => row.Person?.Figure ?? "",
            _ => row.Name
        };

        if (text.Length == 0)
            return;

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Another process can hold the clipboard open. Not worth interrupting anyone over.
        }
    }

    private void MutePerson(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } || !int.TryParse(tag, out int minutes))
            return;

        foreach (RoomEntry row in PeopleSelection())
        {
            try
            {
                (IApplicationRuntime application, RoomModerationStateView state) = ModerationTarget(row);
                application.Invoke<RoomModerationMuteRequest, RoomModerationDispatchResult>(
                    ApplicationMemberIds.RoomModerationMute,
                    new RoomModerationMuteRequest(
                        row.EntityId,
                        minutes,
                        state.SessionGeneration,
                        state.RoomId,
                        row.RoomGeneration,
                        row.Index));
            }
            catch (Exception error)
            {
                Qx.Diagnostics.Diag.Warn($"Could not mute {row.Name}: {error.Message}", "ui");
            }
        }
    }

    private void KickPerson(object sender, RoutedEventArgs e)
    {
        foreach (RoomEntry row in PeopleSelection())
        {
            try
            {
                (IApplicationRuntime application, RoomModerationStateView state) = ModerationTarget(row);
                application.Invoke<RoomModerationTargetRequest, RoomModerationDispatchResult>(
                    ApplicationMemberIds.RoomModerationKick,
                    new RoomModerationTargetRequest(
                        row.EntityId,
                        state.SessionGeneration,
                        state.RoomId,
                        row.RoomGeneration,
                        row.Index));
            }
            catch (Exception error)
            {
                Qx.Diagnostics.Diag.Warn($"Could not kick {row.Name}: {error.Message}", "ui");
            }
        }
    }

    private void BanPerson(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag })
            return;

        BanLength length = tag switch
        {
            "day" => BanLength.Day,
            "perm" => BanLength.Permanent,
            _ => BanLength.Hour
        };

        foreach (RoomEntry row in PeopleSelection())
        {
            try
            {
                (IApplicationRuntime application, RoomModerationStateView state) = ModerationTarget(row);
                application.Invoke<RoomModerationBanRequest, RoomModerationDispatchResult>(
                    ApplicationMemberIds.RoomModerationBan,
                    new RoomModerationBanRequest(
                        row.EntityId,
                        length,
                        state.SessionGeneration,
                        state.RoomId,
                        row.RoomGeneration,
                        row.Index));
            }
            catch (Exception error)
            {
                Qx.Diagnostics.Diag.Warn($"Could not ban {row.Name}: {error.Message}", "ui");
            }
        }
    }

    private void BouncePerson(object sender, RoutedEventArgs e)
    {
        foreach (RoomEntry row in PeopleSelection())
        {
            try
            {
                (IApplicationRuntime application, RoomModerationStateView state) = ModerationTarget(row);
                application.Invoke<RoomModerationTargetRequest, RoomModerationDispatchResult>(
                    ApplicationMemberIds.RoomModerationBounce,
                    new RoomModerationTargetRequest(
                        row.EntityId,
                        state.SessionGeneration,
                        state.RoomId,
                        row.RoomGeneration,
                        row.Index));
            }
            catch (Exception error)
            {
                Qx.Diagnostics.Diag.Warn($"Could not bounce {row.Name}: {error.Message}", "ui");
            }
        }
    }

    private void CopyBanName(object sender, RoutedEventArgs e)
    {
        string names = string.Join(
            Environment.NewLine,
            BansList.SelectedItems.OfType<RoomEntry>().Select(row => row.Name));
        if (names.Length == 0)
            return;

        try
        {
            Clipboard.SetText(names);
        }
        catch
        {
        }
    }
}
