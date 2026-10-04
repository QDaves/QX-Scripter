namespace Qx.Model.Wired;

/// <summary>The role of a practical Wired usage rule.</summary>
public enum WiredUsageTopic
{
    /// <summary>Required placement or companion furniture.</summary>
    Setup,
    /// <summary>Target selection and execution context.</summary>
    Inputs,
    /// <summary>What the combination does.</summary>
    Behavior,
    /// <summary>A common configuration mistake or limitation.</summary>
    Pitfall
}

/// <summary>The evidence supporting a usage rule; configuration evidence does not prove server execution.</summary>
public enum WiredUsageEvidence
{
    /// <summary>Supported by the analyzed client controls or protocol models.</summary>
    ClientConfiguration,
    /// <summary>Described by the hotel's furniture metadata.</summary>
    HotelDescription,
    /// <summary>Practical reference advice not independently verified as server behavior.</summary>
    CommunityReference
}

/// <summary>A concise practical rule with explicit evidence provenance.</summary>
/// <param name="Topic">How the rule helps configure the Wired.</param>
/// <param name="Text">The English rule, including relevant conditions.</param>
/// <param name="Evidence">The kind of evidence, not a guarantee of server behavior.</param>
/// <param name="Reference">A client concept, furniture identifier or reference line range.</param>
public sealed record WiredUsageRule(WiredUsageTopic Topic, string Text, WiredUsageEvidence Evidence, string Reference);

/// <summary>Practical knowledge attached to a Wired form, separate from its wire layout.</summary>
/// <param name="Purpose">The form's purpose in plain English.</param>
/// <param name="Rules">Applicable setup, targeting and compatibility rules.</param>
/// <param name="RelatedFurniture">Class identifiers to look up for companion furniture; these are alternatives and related tools, not a mandatory shopping list.</param>
public sealed record WiredFormUsage(string Purpose, IReadOnlyList<WiredUsageRule> Rules, IReadOnlyList<string> RelatedFurniture);

internal static partial class WiredFormKnowledge
{
    private static readonly IReadOnlyDictionary<(WiredFormCategory, int), WiredFormUsage> entries = create_entries();

    internal static WiredFormUsage? Find(WiredFormCategory category, int code) => entries.GetValueOrDefault((category, code));

    private static WiredFormUsage usage(WiredFormCategory category, int code, string purpose)
    {
        var rules = new List<WiredUsageRule>();
        var related = new HashSet<string>(StringComparer.Ordinal);

        void client(WiredUsageTopic topic, string text, string reference) =>
            rules.Add(new(topic, text, WiredUsageEvidence.ClientConfiguration, reference));
        void reference(WiredUsageTopic topic, string text, string lines) =>
            rules.Add(new(topic, text, WiredUsageEvidence.CommunityReference, "reference:" + lines));
        void hotel(WiredUsageTopic topic, string text, string identifier) =>
            rules.Add(new(topic, text, WiredUsageEvidence.HotelDescription, "furnidata:" + identifier));
        void companions(params string[] identifiers) => related.UnionWith(identifiers);

        client(WiredUsageTopic.Inputs,
            "Read the placed Wired before configuring it. Its allowed source lists, selection limits, current values and server defaults are authoritative for that instance; do not assume every source exists or hard-code a furniture limit.",
            "WiredConfig.InputSources; WiredConfig.FurniLimit; WiredConfig.DefaultIntParams");

        switch (category)
        {
            case WiredFormCategory.Trigger:
                reference(WiredUsageTopic.Setup, "Place the trigger and its effects on the same room tile to form an event-driven stack. A trigger detects an event; it does not perform an effect by itself.", "253;9233-9235");
                break;
            case WiredFormCategory.Action:
                reference(WiredUsageTopic.Setup, "Place the effect in the stack that will execute it, normally with a trigger. A called stack can also execute effects without its own trigger firing.", "9233-9235;9259-9266");
                reference(WiredUsageTopic.Pitfall, "Do not rely on the physical order of effects unless Execute In Order is configured. Action delay and movement animation duration are separate settings.", "9255-9257;2536-2538");
                companions("wf_xtra_exec_in_order");
                break;
            case WiredFormCategory.Condition:
                reference(WiredUsageTopic.Setup, "A condition gates execution; it does not trigger the stack. Ordinary stacks require all conditions to pass unless a condition-evaluation add-on changes that rule.", "948;963-987");
                client(WiredUsageTopic.Inputs, "Choose both the entities to test and the condition quantifier. A negative furniture variant and the quantifier are distinct controls; inspect the received definition rather than treating every match as all users or all furniture.", "WiredConditionConfig.QuantifierCode; QuantifierType; DefinitionIsInvert");
                companions("wf_xtra_or_eval");
                break;
            case WiredFormCategory.Selector:
                reference(WiredUsageTopic.Setup, "Put the selector in the stack that needs its result. Selectors choose targets; an effect must still consume those targets.", "4583-4600;33-92");
                client(WiredUsageTopic.Inputs, "Set the consuming effect or condition's matching user/furniture source to Selector where the received form allows it. Adding a selector does not change a consumer that still uses Triggering or Picked.", "InputSourcesConf; WiredUserSource.Selector; WiredFurnitureSource.Selector");
                reference(WiredUsageTopic.Behavior, "Ordinary selectors add to a selection. Filter mode keeps matching entries from the existing selection; inversion negates the selector's criterion. Filter and inversion are independent.", "4602-4634");
                break;
            case WiredFormCategory.Addon when code is >= 1000 and < 2000:
                reference(WiredUsageTopic.Setup, "Place this variable add-on on the same tile as the variable definition it should extend, rather than only beside the trigger/effect stack that changes the value.", "2522-2528;5384-5398");
                break;
            case WiredFormCategory.Addon:
                reference(WiredUsageTopic.Setup, "An add-on modifies compatible Wired components; it is not an event trigger or an independently running effect. Check the specific companions and inputs below.", "2504-2528");
                break;
            case WiredFormCategory.Variable:
                reference(WiredUsageTopic.Setup, "A variable definition can be placed separately from the stacks that read or change it. Put variable add-ons with the definition they extend.", "5374-5398");
                client(WiredUsageTopic.Pitfall, "Use the variable identifier and target domain from the room's variable context. A display name is not a variable ID, and defining a variable does not imply that every user or furniture item already holds it.", "WiredVariable; WiredVariableHolder; WiredSourceDomain");
                companions("wf_act_give_var", "wf_act_change_var_val", "wf_cnd_has_var");
                break;
        }

        if (category == WiredFormCategory.Trigger && code is 3 or 6 or 12 or 19)
        {
            reference(WiredUsageTopic.Inputs, "A timer event has no user who caused it. For user-targeted effects, select an explicit supported user source such as Selector or Room instead of assuming a triggering user exists.", "259-266;9604");
            client(WiredUsageTopic.Pitfall, "Use this form's documented time unit. Short-period milliseconds and ordinary half-second pulses are not interchangeable.", "WiredFormFieldDefinition.Values");
            companions("wf_slc_users_area", "wf_slc_users_bytype");
        }

        if ((category == WiredFormCategory.Trigger && code == 3) || (category == WiredFormCategory.Condition && code is 3 or 4) || (category == WiredFormCategory.Action && code == 1))
        {
            hotel(WiredUsageTopic.Behavior, "Elapsed-time triggers and conditions use the Wired timer reset by Reset Timers. This is separate from controlling a selected clock-counter furniture item.", "wf_act_reset_timers;wf_trg_at_given_time;wf_cnd_time_less_than;wf_cnd_time_more_than");
            companions("wf_act_reset_timers", "wf_trg_at_given_time");
        }

        if ((category == WiredFormCategory.Trigger && code == 17) || (category == WiredFormCategory.Action && code == 30) || (category == WiredFormCategory.Selector && code is 5 or 10))
        {
            reference(WiredUsageTopic.Setup, "For signal routing, use Send Signal, a signal antenna and Receive Signal. Configure the sending and receiving furniture selections to refer to the intended antenna; merely placing the two Wireds in different stacks does not connect them.", "370-372;9266");
            client(WiredUsageTopic.Inputs, "Configure the users and furniture carried by Send Signal separately from its antenna selection. On the receiving side, use the Signal source or a From Signal selector when that payload should be consumed.", "SendSignal source controls; WiredFurnitureSource.Signal; WiredUserSource.Signal");
            reference(WiredUsageTopic.Behavior, "A received signal starts a new activation, allowing the receiving stack's selectors and conditions to be evaluated for that activation.", "9259-9266");
            companions("wf_act_send_signal", "wf_trg_recv_signal", "wf_antenna1", "wf_antenna2", "wf_slc_furni_signal", "wf_slc_users_signal");
        }

        if (category == WiredFormCategory.Action && code is 18 or 30 or 49)
            reference(WiredUsageTopic.Pitfall, "The negative-action furniture variant is a condition-failure branch, not the inverse of the positive effect. Check the received variant code before choosing which branch to build.", "1890-1894;9474-9478");

        if (category == WiredFormCategory.Action && code == 18)
        {
            hotel(WiredUsageTopic.Pitfall, "Call Stacks executes selected stacks regardless of their own triggers and conditions. Do not use it as a substitute for Receive Signal when the target stack must check its own conditions.", "wf_act_call_stacks");
            reference(WiredUsageTopic.Behavior, "The reference describes called-stack selectors as prepared during the original activation; use signal routing when a later independent activation is required. This timing detail has not been independently verified.", "9259-9266");
            companions("wf_act_send_signal", "wf_trg_recv_signal", "wf_antenna1");
        }

        if ((category == WiredFormCategory.Action && code is 11 or 12 or 13) || (category == WiredFormCategory.Trigger && code == 11))
        {
            hotel(WiredUsageTopic.Setup, "Use the collision trigger with movement effects that generate user/furniture collisions, including Move To Direction, Chase and Flee. A click on stationary furniture is a different event.", "wf_trg_collision;wf_act_move_to_dir;wf_act_chase;wf_act_flee");
            companions("wf_trg_collision", "wf_act_move_to_dir", "wf_act_chase", "wf_act_flee");
        }

        if (category == WiredFormCategory.Trigger && code is 4 or 18 or 20 or 21 or 24)
            client(WiredUsageTopic.Pitfall, "Choose the exact event: furniture click, furniture use, furniture state change, tile click and user click have different trigger forms. They are not interchangeable synonyms.", "UseStuff; AvatarClicksFurni; StateChange; ClickTile; UserClicksUser");

        if ((category == WiredFormCategory.Trigger && code is 13 or 14) || (category == WiredFormCategory.Action && code is >= 21 and <= 27))
        {
            client(WiredUsageTopic.Inputs, "Choose an existing bot through the form's supported bot source or bot-name control. Distinguish that bot from the reached, triggering or selected user and from destination furniture.", "Bot source controls; WiredUserSource.NamedBot; WiredUserSource.Reached");
            companions("wf_act_bot_move", "wf_trg_bot_reached_stf", "wf_trg_bot_reached_avtr");
        }

        if (category == WiredFormCategory.Trigger && code == 24)
            client(WiredUsageTopic.Inputs, "The clicking user and the clicked user are different targets. Use the Clicked user source when the action should affect the person who was clicked.", "WiredUserSource.Clicked; UserClicksUser");

        if ((category == WiredFormCategory.Trigger && code == 15) || (category == WiredFormCategory.Action && code is 28 or 38) || (category == WiredFormCategory.Condition && code == 35))
        {
            client(WiredUsageTopic.Setup, "Select compatible clock-counter furniture for clock operations. Reset Timers controls the Wired timer and is not a replacement for selecting and controlling a clock item.", "ClockReachTime; ControlClock; AdjustClock; ClockTimeMatches");
            companions("wf_game_upcounter1", "wf_game_upcounter2", "wf_upcounter1", "wf_upcounter2", "wf_act_control_clock");
        }

        if ((category == WiredFormCategory.Action && code == 3) || (category == WiredFormCategory.Condition && code == 0))
        {
            client(WiredUsageTopic.Setup, "Select the reference furniture and capture the intended state, position and direction before changing the room. Enable only the snapshot components that should be restored or compared.", "MatchSnapshot; StatesMatch; ApplySnapshot");
            companions("wf_act_match_to_sshot", "wf_cnd_match_snapshot");
        }

        if (category == WiredFormCategory.Action && code is 4 or 13 or 16 or 29 or 33 or 34 or 35 or 42 or 43 or 57)
        {
            client(WiredUsageTopic.Inputs, "Keep the moving entities separate from destination/reference entities. Where the form exposes a secondary furniture selection, it is a separate input, not additional primary targets.", "WiredConfig.StuffIds2; InputSourcesConf");
            reference(WiredUsageTopic.Setup, "Movement add-ons belong with the movement stack. Carry Users, Movement Physics and Animation Time change different aspects of movement and should only be added when needed.", "2536-2580");
            companions("wf_xtra_mov_physics", "wf_xtra_mov_carry_users", "wf_xtra_anim_time");
        }

        if (category == WiredFormCategory.Action && code is 7 or 8 or 9 or 10 or 19 or 20 or 31 or 32 or 42 or 43 or 44 or 52 or 54)
            reference(WiredUsageTopic.Inputs, "Ensure the selected user source supplies the intended recipients. A timer-driven stack needs an explicit user selection, and a furniture selection is not a substitute for a user target.", "259-266;33-92;9604");

        if (category == WiredFormCategory.Action && code == 8)
        {
            hotel(WiredUsageTopic.Inputs, "Select destination furniture independently of the users being teleported. With multiple destinations the hotel description specifies a random destination.", "wf_act_teleport_to");
            companions("wf_slc_users_area");
        }

        if ((category == WiredFormCategory.Action && code is 6 or 9 or 10 or 14) || (category == WiredFormCategory.Condition && code is 6 or 31 or 34) || (category == WiredFormCategory.Selector && code == 3))
        {
            client(WiredUsageTopic.Inputs, "Game teams are separate from room groups. Use the configured team choice and the appropriate user/team source; group membership is a different condition.", "ActorIsInTeam; ActorIsGroupMember; JoinTeam; GiveScoreToPredefinedTeam");
            companions("wf_act_join_team", "wf_act_give_score_tm", "wf_cnd_actor_in_team");
        }

        if (category == WiredFormCategory.Condition && code == 10)
            hotel(WiredUsageTopic.Setup, "This condition checks membership of the room's group. It requires a group room; it is not an arbitrary group-ID lookup.", "wf_cnd_actor_in_group");

        if (category == WiredFormCategory.Selector && code is 6 or 12)
            client(WiredUsageTopic.Inputs, "The neighborhood is a mask of tile offsets relative to the configured reference entity. Choose that reference source as well as the offsets; it is not a fixed room rectangle.", "WiredTileOffset; FurnitureInNeighborhood; UsersInNeighborhood");

        if (category == WiredFormCategory.Selector && code is 7 or 13)
            client(WiredUsageTopic.Inputs, "Configure the room rectangle with x, y, width and height. These are absolute room coordinates, unlike neighborhood offsets.", "FurnitureInArea; UsersInArea");

        if (category == WiredFormCategory.Selector && code == 19)
        {
            reference(WiredUsageTopic.Setup, "Remote selection uses a selection defined by another stack or configured source. It is separate from sending a signal payload.", "4702-4706;4850-4852");
            client(WiredUsageTopic.Inputs, "Choose union or intersection for combining remote selections. A random stack count of zero means all configured stacks; a positive count limits the number of randomly selected stacks.", "RemoteSelector.SetOperation; RemoteSelector.RandomStackCount");
            companions("wf_slc_furni_area", "wf_slc_users_area");
        }

        if (category == WiredFormCategory.Addon && code is >= 10 and <= 13)
        {
            reference(WiredUsageTopic.Setup, "This filter needs an existing selection of the matching entity kind. Add a furniture or user selector first; filtering an empty selection does not create targets.", "4636-4648;4778-4800");
            companions(code is 10 or 12 ? "wf_slc_furni_area" : "wf_slc_users_area");
        }

        if (category == WiredFormCategory.Addon && code is 14 or 15 or 16 or 19)
        {
            reference(WiredUsageTopic.Setup, "Place text add-ons with the stack whose text is being read or written. Output substitution uses a matching placeholder in an output field; input capture needs a matching marker in the incoming text pattern.", "1417-1447;2606-2628");
            client(WiredUsageTopic.Inputs, "Configure the placeholder name and its supported source or variable. A text marker alone does not select a user, furniture item or variable.", "UsernamePlaceholder; VariablePlaceholder; VariableCapturer; FurniNamePlaceholder");
            companions("wf_trg_says_something", "wf_act_show_message", "wf_xtra_text_output_variable", "wf_xtra_text_input_variable");
        }

        if ((category == WiredFormCategory.Action && code is 39 or 40 or 41) || (category == WiredFormCategory.Condition && code is 40 or 42 or 43) || (category == WiredFormCategory.Selector && code is 17 or 18) || (category == WiredFormCategory.Trigger && code == 22))
        {
            client(WiredUsageTopic.Inputs, "Choose a variable with the required target domain and capabilities. Presence, numeric value, write permission and creation/deletion support are separate properties; a variable can exist without carrying a numeric value.", "WiredVariable.HasValue; CanWriteValue; CanCreateAndDelete; VariableTarget");
            companions("wf_var_user", "wf_var_furni", "wf_var_room", "wf_cnd_has_var", "wf_act_give_var");
        }

        if ((category == WiredFormCategory.Action && code is >= 45 and <= 48) || (category == WiredFormCategory.Condition && code is 45 or 46) || (category == WiredFormCategory.Trigger && code is 25 or 26) || (category == WiredFormCategory.Addon && code is 18 or 20))
        {
            reference(WiredUsageTopic.Setup, "Contracts define what a transaction requires or rewards; chests hold the actual resources. Select the intended contract/chest inputs and ensure the relevant chest has the required resources and permissions.", "1768-1790;4004-4062");
            client(WiredUsageTopic.Pitfall, "Contract furniture uses the contract-content workflow, not the six-category Wired form editor. Starting a transaction is separate from accepting and confirming its reviewed offer.", "WiredOpenContract; WiredContractContents; WiredTradeInitiate; WiredTradeAccept");
            companions("wf_act_init_transaction", "wf_contract_payment", "wf_contract_trade", "wf_contract_reward", "wf_storage_furni1", "wf_storage_coins1", "wf_trg_transaction_complete", "wf_trg_transaction_fail");
        }

        if (category == WiredFormCategory.Addon && code == 0)
            client(WiredUsageTopic.Pitfall, "Configure the condition evaluation mode/count; the furniture name is not proof that every setup simply means one condition must pass.", "ConditionEvaluation");

        if (category == WiredFormCategory.Addon && code is 6 or 7 or 8 or 9 or 21 or 22)
        {
            reference(WiredUsageTopic.Setup, "Use this with compatible movement effects in the same stack. It changes movement behavior and does not move furniture on its own.", "2536-2580;2594-2598");
            companions("wf_act_move_rotate", "wf_act_move_to_dir", "wf_act_rel_mov");
        }

        if (category == WiredFormCategory.Addon && code is 1000 or 1001 or 1002)
        {
            client(WiredUsageTopic.Inputs, "Configure the derived outputs you actually need and keep their names distinct. A derived output is separate from the source variable and should be referenced through the room's returned variable metadata.", "VariableTextConverter; VariableLevelUp; VariableTimeUtil; WiredVariable");
            companions("wf_var_user", "wf_var_furni", "wf_var_room", "wf_var_context");
        }

        if (category == WiredFormCategory.Addon && code == 1001)
        {
            reference(WiredUsageTopic.Setup, "Use a numeric source variable containing accumulated experience. The level add-on derives level/progress information; it does not award experience by itself.", "5570-5587;5743-5859");
            companions("wf_act_change_var_val", "wf_xtra_varfx_levelling");
        }

        if (category == WiredFormCategory.Addon && code is >= 1200 and <= 1205)
        {
            reference(WiredUsageTopic.Setup, "Place the FX add-on with a user or furniture variable definition and assign that variable a numeric value on the target. An unassigned variable has no target value to display.", "5392-5398;6103-6114");
            client(WiredUsageTopic.Inputs, "Match source_type to the variable's target kind. Owner-only and same-team audiences require user targets; furniture targets use a compatible audience such as Everyone or a user-variable audience filter.", "VariableFx source_type and audience controls");
            client(WiredUsageTopic.Pitfall, "Use GetWiredFxStyles for valid style/color/width/renderer combinations. Visibility mode, update mask, hover behavior and duration decide when the display appears; changing a style does not assign or update the variable.", "WiredFxStyles; VariableFx show_mode/update_mask/show_on_hover/duration_milliseconds");
            companions("wf_var_user", "wf_var_furni", "wf_act_give_var", "wf_act_change_var_val");
            if (code == 1202)
            {
                reference(WiredUsageTopic.Setup, "For level progress, pair the source variable with a Level Up add-on so the configured experience-to-level progression is available.", "7014-7090");
                companions("wf_xtra_var_lvlup_system");
            }
        }

        if (category == WiredFormCategory.Addon && code == 2002)
            client(WiredUsageTopic.Pitfall, "Read and write Web API keys are distinct credentials tied to the selected Wired. Do not treat opening this form as permission to generate or publish either key.", "WiredWebApiKeyRequest; WiredWebApiKey");

        if (category == WiredFormCategory.Variable && code is 0 or 1 or 2 or 3)
            client(WiredUsageTopic.Inputs, "Choose user, furniture, room/global or execution-context scope to match the value being stored. Availability and persistence are controlled by the variable definition; do not infer them solely from its name.", "VariableTarget; AvailabilityType; UserVariable; Furniture; GlobalVariable; ContextVariable");

        if (category == WiredFormCategory.Variable && code == 4)
            client(WiredUsageTopic.Setup, "Select an available shared variable from the referenced room and respect its returned permissions. A cross-room reference is not a copy of the source variable.", "ReferenceVariable; WiredSharedVariable");

        return new(purpose, rules.AsReadOnly(), Array.AsReadOnly(related.Order(StringComparer.Ordinal).ToArray()));
    }
}
