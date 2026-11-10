using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Tests.Core;

public class IslandCoordinatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly IslandSettings Settings = IslandSettings.Default with { ExpandedDurationSeconds = 5 };

    private readonly IslandCoordinator _coordinator = new(() => Settings);

    private static DateTimeOffset At(double seconds) => T0.AddSeconds(seconds);

    private static IslandActivity Transient(string id, ActivityPriority priority = ActivityPriority.Normal, string module = "test") =>
        new(id, module, ActivityKind.Transient, new FakeContent()) { Priority = priority };

    private static IslandActivity Live(string id, ActivityPriority priority = ActivityPriority.Normal, bool expandOnStart = false, string module = "test") =>
        new(id, module, ActivityKind.Live, new FakeContent()) { Priority = priority, ExpandOnStart = expandOnStart };

    [Fact]
    public void Starts_compact_and_empty()
    {
        Assert.Equal(IslandSnapshot.Empty, _coordinator.Snapshot);
        Assert.Null(_coordinator.NextDeadline);
    }

    [Fact]
    public void Transient_expands_and_returns_to_compact_after_duration()
    {
        var volume = Transient("volume");

        _coordinator.Post(volume, At(0));
        Assert.Equal(IslandState.Expanded, _coordinator.Snapshot.State);
        Assert.Same(volume, _coordinator.Snapshot.Focused);
        Assert.Equal(At(5), _coordinator.NextDeadline);

        _coordinator.Advance(At(4.9));
        Assert.Equal(IslandState.Expanded, _coordinator.Snapshot.State);

        _coordinator.Advance(At(5));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
        Assert.Null(_coordinator.Snapshot.Focused);
        Assert.Null(_coordinator.NextDeadline);
    }

    [Fact]
    public void Custom_duration_overrides_setting()
    {
        _coordinator.Post(Transient("volume") with { Duration = TimeSpan.FromSeconds(1.5) }, At(0));

        Assert.Equal(At(1.5), _coordinator.NextDeadline);
    }

    [Fact]
    public void Reposting_visible_transient_updates_content_and_restarts_timer()
    {
        _coordinator.Post(Transient("volume"), At(0));
        var updated = Transient("volume");

        _coordinator.Post(updated, At(3));

        Assert.Same(updated, _coordinator.Snapshot.Focused);
        Assert.Equal(At(8), _coordinator.NextDeadline);
    }

    [Fact]
    public void Transients_queue_in_priority_then_arrival_order()
    {
        _coordinator.Post(Transient("a"), At(0));
        _coordinator.Post(Transient("b"), At(1));
        _coordinator.Post(Transient("c"), At(1));
        _coordinator.Post(Transient("low", ActivityPriority.Low), At(1));

        Assert.Equal("a", _coordinator.Snapshot.Focused?.Id);
        _coordinator.Advance(At(5));
        Assert.Equal("b", _coordinator.Snapshot.Focused?.Id);
        _coordinator.Advance(At(10));
        Assert.Equal("c", _coordinator.Snapshot.Focused?.Id);
        _coordinator.Advance(At(15));
        Assert.Equal("low", _coordinator.Snapshot.Focused?.Id);
        _coordinator.Advance(At(20));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Higher_priority_preempts_and_preempted_returns_afterwards()
    {
        _coordinator.Post(Transient("normal"), At(0));

        _coordinator.Post(Transient("urgent", ActivityPriority.High), At(1));
        Assert.Equal("urgent", _coordinator.Snapshot.Focused?.Id);

        _coordinator.Advance(At(6));
        Assert.Equal("normal", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Live_activity_becomes_compact_indicator_without_expanding()
    {
        var media = Live("media");

        _coordinator.Post(media, At(0));

        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
        Assert.Same(media, _coordinator.Snapshot.Indicator);
        Assert.Equal(1, _coordinator.Snapshot.LiveCount);
    }

    [Fact]
    public void Live_activity_with_expand_on_start_announces_once()
    {
        _coordinator.Post(Live("media", expandOnStart: true), At(0));
        Assert.Equal(IslandState.Expanded, _coordinator.Snapshot.State);

        _coordinator.Advance(At(5));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);

        // Atualizações (ex.: progresso da música) não expandem de novo.
        _coordinator.Post(Live("media", expandOnStart: true), At(6));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
        Assert.Equal("media", _coordinator.Snapshot.Indicator?.Id);
    }

    [Fact]
    public void Indicator_is_highest_priority_then_most_recent_live_activity()
    {
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));
        _coordinator.Post(Live("download"), At(1));
        _coordinator.Post(Live("media"), At(2));

        Assert.Equal("media", _coordinator.Snapshot.Indicator?.Id);

        _coordinator.Remove("media", At(3));
        Assert.Equal("download", _coordinator.Snapshot.Indicator?.Id);
    }

    [Fact]
    public void Click_opens_interactive_on_top_live_activity_and_click_again_closes()
    {
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));

        _coordinator.ToggleInteractive(At(1));
        Assert.Equal(IslandState.Interactive, _coordinator.Snapshot.State);
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(IslandLayout.Interactive, _coordinator.Snapshot.Dimensions);

        _coordinator.ToggleInteractive(At(2));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Click_with_nothing_to_show_does_nothing()
    {
        _coordinator.ToggleInteractive(At(0));

        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Click_during_transient_makes_it_interactive_and_it_is_discarded_on_close()
    {
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));
        _coordinator.Post(Transient("notification"), At(0));

        _coordinator.ToggleInteractive(At(1));
        Assert.Equal("notification", _coordinator.Snapshot.Focused?.Id);

        // Não expira no tempo do transitório: o usuário está interagindo.
        _coordinator.Advance(At(5.5));
        Assert.Equal(IslandState.Interactive, _coordinator.Snapshot.State);

        _coordinator.ToggleInteractive(At(6));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);

        _coordinator.ToggleInteractive(At(7));
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Interactive_auto_collapses_after_inactivity()
    {
        _coordinator.Post(Live("clock"), At(0));
        _coordinator.ToggleInteractive(At(0));

        _coordinator.Advance(At(5));

        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Nothing_expires_while_pointer_is_over_and_grace_applies_on_exit()
    {
        _coordinator.Post(Transient("volume"), At(0));
        _coordinator.PointerEntered();

        Assert.Null(_coordinator.NextDeadline);
        _coordinator.Advance(At(30));
        Assert.Equal(IslandState.Expanded, _coordinator.Snapshot.State);

        _coordinator.PointerExited(At(30));
        Assert.Equal(At(32), _coordinator.NextDeadline);
        _coordinator.Advance(At(32));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Pointer_exit_restarts_interactive_collapse_timer()
    {
        _coordinator.Post(Live("clock"), At(0));
        _coordinator.PointerEntered();
        _coordinator.ToggleInteractive(At(0));
        Assert.Null(_coordinator.NextDeadline);

        _coordinator.PointerExited(At(20));

        Assert.Equal(At(25), _coordinator.NextDeadline);
    }

    [Fact]
    public void Normal_transient_waits_while_user_interacts_then_shows()
    {
        _coordinator.Post(Live("clock"), At(0));
        _coordinator.ToggleInteractive(At(0));

        _coordinator.Post(Transient("notification"), At(1));
        Assert.Equal(IslandState.Interactive, _coordinator.Snapshot.State);
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);

        _coordinator.ToggleInteractive(At(2));
        Assert.Equal(IslandState.Expanded, _coordinator.Snapshot.State);
        Assert.Equal("notification", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void High_priority_interrupts_interactive()
    {
        _coordinator.Post(Live("clock"), At(0));
        _coordinator.ToggleInteractive(At(0));

        _coordinator.Post(Transient("battery-low", ActivityPriority.High), At(1));

        Assert.Equal(IslandState.Expanded, _coordinator.Snapshot.State);
        Assert.Equal("battery-low", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Cycle_switches_between_live_activities_in_interactive()
    {
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));
        _coordinator.Post(Live("media"), At(0));
        _coordinator.ToggleInteractive(At(1));
        Assert.Equal("media", _coordinator.Snapshot.Focused?.Id);

        _coordinator.Cycle(1, At(2));
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(At(7), _coordinator.NextDeadline);

        _coordinator.Cycle(1, At(3));
        Assert.Equal("media", _coordinator.Snapshot.Focused?.Id);

        _coordinator.Cycle(-1, At(4));
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Interactive_snapshot_exposes_pages_and_select_page_jumps_directly()
    {
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));
        _coordinator.Post(Live("media"), At(0));
        _coordinator.Post(Live("timer"), At(0));

        Assert.Equal(0, _coordinator.Snapshot.PageCount);

        _coordinator.ToggleInteractive(At(1));
        Assert.Equal(3, _coordinator.Snapshot.PageCount);
        Assert.Equal(0, _coordinator.Snapshot.PageIndex);
        Assert.Equal("timer", _coordinator.Snapshot.Focused?.Id);

        _coordinator.SelectPage(2, At(2));
        Assert.Equal(2, _coordinator.Snapshot.PageIndex);
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(At(7), _coordinator.NextDeadline);

        _coordinator.SelectPage(9, At(3));
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Open_panel_counts_as_first_page()
    {
        _coordinator.Post(Live("media"), At(0));
        _coordinator.Post(Transient("system.panel") with { OpenInteractive = true }, At(0));

        Assert.Equal(2, _coordinator.Snapshot.PageCount);
        Assert.Equal(0, _coordinator.Snapshot.PageIndex);

        _coordinator.Cycle(1, At(1));
        Assert.Equal("media", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Removing_focused_live_activity_moves_focus_or_collapses()
    {
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));
        _coordinator.Post(Live("media"), At(0));
        _coordinator.ToggleInteractive(At(0));

        _coordinator.Remove("media", At(1));
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);

        _coordinator.Remove("clock", At(2));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Removing_visible_transient_shows_next_in_queue()
    {
        _coordinator.Post(Transient("a"), At(0));
        _coordinator.Post(Transient("b"), At(0));

        _coordinator.Remove("a", At(1));

        Assert.Equal("b", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(At(6), _coordinator.NextDeadline);
    }

    [Fact]
    public void Removing_module_clears_all_its_activities()
    {
        _coordinator.Post(Live("media", module: "media"), At(0));
        _coordinator.Post(Transient("media-changed", module: "media"), At(0));
        _coordinator.Post(Transient("volume", module: "volume"), At(0));
        _coordinator.Post(Live("clock", ActivityPriority.Low, module: "clock"), At(0));

        _coordinator.RemoveModule("media", At(1));

        Assert.Equal("volume", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal("clock", _coordinator.Snapshot.Indicator?.Id);
        Assert.Equal(1, _coordinator.Snapshot.LiveCount);
    }

    [Fact]
    public void Hiding_drops_low_priority_transients_with_record_and_keeps_important_ones()
    {
        _coordinator.Post(Transient("a"), At(0));
        _coordinator.Post(Transient("b"), At(0));
        _coordinator.Post(Transient("alarm", ActivityPriority.High), At(0));
        _coordinator.Post(Live("clock"), At(0));

        _coordinator.SetHidden(true, At(1));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
        Assert.Null(_coordinator.NextDeadline);
        Assert.Contains(_coordinator.History, r => r.Id == "b" && r.State == ActivityRecordState.Dropped);

        // Enquanto oculta, nada expande; ao voltar, o importante aparece.
        _coordinator.Post(Transient("c"), At(2));
        _coordinator.Advance(At(30));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);

        _coordinator.SetHidden(false, At(40));
        Assert.Equal("alarm", _coordinator.Snapshot.Focused?.Id);
        _coordinator.Advance(At(46));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
        Assert.Equal("clock", _coordinator.Snapshot.Indicator?.Id);
        Assert.Contains(_coordinator.History, r => r.Id == "c" && r.State == ActivityRecordState.Dropped);
    }

    [Fact]
    public void Transients_with_same_group_are_merged_and_counted()
    {
        _coordinator.Post(Transient("n1") with { GroupKey = "app" }, At(0));
        _coordinator.Post(Transient("n2") with { GroupKey = "app" }, At(1));
        _coordinator.Post(Transient("other"), At(1));
        _coordinator.Post(Transient("n3") with { GroupKey = "app" }, At(2));

        Assert.Equal("n3", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(3, _coordinator.Snapshot.FocusedGroupCount);
        Assert.Equal(At(7), _coordinator.NextDeadline);

        _coordinator.Advance(At(7));
        Assert.Equal("other", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(1, _coordinator.Snapshot.FocusedGroupCount);
        Assert.Contains(_coordinator.History, r => r.Id == "n1" && r.State == ActivityRecordState.Grouped);
    }

    [Fact]
    public void Queued_transients_of_a_group_collapse_into_one()
    {
        _coordinator.Post(Transient("volume"), At(0));
        _coordinator.Post(Transient("m1") with { GroupKey = "chat" }, At(1));
        _coordinator.Post(Transient("m2") with { GroupKey = "chat" }, At(2));

        _coordinator.Advance(At(5));

        Assert.Equal("m2", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(2, _coordinator.Snapshot.FocusedGroupCount);
        _coordinator.Advance(At(10));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Dismissing_live_activity_hides_it_until_module_removes_it()
    {
        _coordinator.Post(Live("media", ActivityPriority.Normal), At(0));
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));
        _coordinator.ToggleInteractive(At(0));

        var dismissed = _coordinator.DismissFocused(At(1));

        Assert.Equal("media", dismissed?.Id);
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
        _coordinator.Post(Live("media", ActivityPriority.Normal), At(2));
        Assert.Equal(1, _coordinator.Snapshot.LiveCount);

        _coordinator.Remove("media", At(3));
        _coordinator.Post(Live("media", ActivityPriority.Normal), At(4));
        Assert.Equal(2, _coordinator.Snapshot.LiveCount);
        Assert.Contains(_coordinator.History, r => r.Id == "media" && r.State == ActivityRecordState.Shown);
    }

    [Fact]
    public void Dismissing_transient_shows_next()
    {
        _coordinator.Post(Transient("a"), At(0));
        _coordinator.Post(Transient("b"), At(0));
        _coordinator.ToggleInteractive(At(1));

        Assert.Equal("a", _coordinator.DismissFocused(At(1))?.Id);
        Assert.Equal("b", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(IslandState.Expanded, _coordinator.Snapshot.State);
    }

    [Fact]
    public void Activity_that_cannot_be_dismissed_stays()
    {
        _coordinator.Post(Live("rec") with { CanDismiss = false }, At(0));
        _coordinator.ToggleInteractive(At(0));

        Assert.Null(_coordinator.DismissFocused(At(1)));
        Assert.Equal("rec", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Pinned_module_is_always_the_compact_indicator()
    {
        var settings = Settings with { PinnedModuleId = "timer" };
        var coordinator = new IslandCoordinator(() => settings);

        coordinator.Post(Live("timer.countdown", ActivityPriority.Low, module: "timer"), At(0));
        coordinator.Post(Live("media", ActivityPriority.High, module: "media"), At(1));

        Assert.Equal("timer.countdown", coordinator.Snapshot.Indicator?.Id);
        Assert.True(coordinator.Snapshot.IndicatorPinned);
    }

    [Fact]
    public void Collapse_interactive_returns_to_compact()
    {
        _coordinator.Post(Live("clock"), At(0));
        _coordinator.ToggleInteractive(At(0));

        _coordinator.CollapseInteractive(At(1));

        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
    }

    [Fact]
    public void History_is_bounded_and_newest_first()
    {
        for (var i = 0; i < IslandCoordinator.HistoryCapacity + 20; i++)
        {
            _coordinator.Post(Transient($"t{i}"), At(i * 10));
            _coordinator.Advance(At(i * 10 + 6));
        }

        Assert.Equal(IslandCoordinator.HistoryCapacity, _coordinator.History.Count);
        Assert.Equal($"t{IslandCoordinator.HistoryCapacity + 19}", _coordinator.History[0].Id);
        Assert.Equal(ActivityRecordState.Completed, _coordinator.History[0].State);
    }

    [Fact]
    public void Open_interactive_request_goes_straight_to_interactive_and_is_discarded_on_close()
    {
        _coordinator.Post(Live("clock", ActivityPriority.Low), At(0));
        _coordinator.ToggleInteractive(At(0));

        _coordinator.Post(Transient("system.panel") with { OpenInteractive = true }, At(1));
        Assert.Equal(IslandState.Interactive, _coordinator.Snapshot.State);
        Assert.Equal("system.panel", _coordinator.Snapshot.Focused?.Id);
        Assert.Equal(At(6), _coordinator.NextDeadline);

        _coordinator.ToggleInteractive(At(2));
        Assert.Equal(IslandState.Compact, _coordinator.Snapshot.State);
        _coordinator.ToggleInteractive(At(3));
        Assert.Equal("clock", _coordinator.Snapshot.Focused?.Id);
    }

    [Fact]
    public void Content_preferred_dimensions_are_used_and_clamped()
    {
        var wide = new FakeContent(new IslandDimensions(9999, 60, 99));

        _coordinator.Post(new IslandActivity("wide", "test", ActivityKind.Transient, wide), At(0));

        Assert.Equal(new IslandDimensions(IslandLayout.Maximum.Width, 60, 30), _coordinator.Snapshot.Dimensions);
    }

    [Fact]
    public void Changed_is_raised_only_when_snapshot_changes()
    {
        var count = 0;
        _coordinator.Changed += (_, _) => count++;

        _coordinator.Post(Transient("a"), At(0));
        _coordinator.Advance(At(1));
        _coordinator.Advance(At(2));

        Assert.Equal(1, count);
    }

    private sealed class FakeContent(IslandDimensions? preferred = null) : IActivityContent
    {
        public IslandDimensions? GetPreferredDimensions(IslandState state) => preferred;
    }
}
