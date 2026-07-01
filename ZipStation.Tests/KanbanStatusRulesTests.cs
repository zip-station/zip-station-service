using ZipStation.Business.Helpers;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;
using Xunit;

namespace ZipStation.Tests;

public class KanbanStatusRulesTests
{
    private const long Now = 1_700_000_000_000;

    private static KanbanBoard BoardWith3Columns()
    {
        // todo (pos 1), doing (pos 2), done (pos 0 but resolved)
        var todo = new KanbanColumn { Id = "todo", Name = "To Do", Position = 1 };
        var doing = new KanbanColumn { Id = "doing", Name = "Doing", Position = 2 };
        var done = new KanbanColumn { Id = "done", Name = "Done", Position = 3 };
        return new KanbanBoard
        {
            Columns = new List<KanbanColumn> { done, todo, doing },
            ResolvedColumnId = "done",
        };
    }

    // ---- ParseStatusFilter ----

    [Fact]
    public void ParseStatusFilter_Empty_ReturnsDefault()
    {
        Assert.Equal(KanbanStatusRules.DefaultGridStatuses, KanbanStatusRules.ParseStatusFilter(null));
        Assert.Equal(KanbanStatusRules.DefaultGridStatuses, KanbanStatusRules.ParseStatusFilter(new List<string>()));
        Assert.Equal(
            new[] { KanbanStoryStatus.Unreviewed, KanbanStoryStatus.Backlog, KanbanStoryStatus.Committed },
            KanbanStatusRules.ParseStatusFilter(null));
    }

    [Fact]
    public void ParseStatusFilter_ParsesNamesCaseInsensitively_AndDedupes()
    {
        var result = KanbanStatusRules.ParseStatusFilter(new[] { "obsolete", "ARCHIVED", "Archived" });
        Assert.Equal(new[] { KanbanStoryStatus.Obsolete, KanbanStoryStatus.Archived }, result);
    }

    [Fact]
    public void ParseStatusFilter_SplitsCommaSeparatedValues()
    {
        var result = KanbanStatusRules.ParseStatusFilter(new[] { "Backlog,Committed", "Resolved" });
        Assert.Equal(
            new[] { KanbanStoryStatus.Backlog, KanbanStoryStatus.Committed, KanbanStoryStatus.Resolved },
            result);
    }

    [Fact]
    public void ParseStatusFilter_UnknownNames_FallBackToDefaultWhenNothingValid()
    {
        var result = KanbanStatusRules.ParseStatusFilter(new[] { "bogus", "nope" });
        Assert.Equal(KanbanStatusRules.DefaultGridStatuses, result);
    }

    [Fact]
    public void ParseStatusFilter_DropsUnknownButKeepsValid()
    {
        var result = KanbanStatusRules.ParseStatusFilter(new[] { "bogus", "Committed" });
        Assert.Equal(new[] { KanbanStoryStatus.Committed }, result);
    }

    // ---- ResolveCommitColumnId ----

    [Fact]
    public void ResolveCommitColumnId_PicksLowestPositionNonResolvedColumn()
    {
        var board = BoardWith3Columns();
        Assert.Equal("todo", KanbanStatusRules.ResolveCommitColumnId(board));
    }

    [Fact]
    public void ResolveCommitColumnId_FallsBackToLowestColumnWhenAllResolved()
    {
        var only = new KanbanColumn { Id = "done", Name = "Done", Position = 0 };
        var board = new KanbanBoard { Columns = new List<KanbanColumn> { only }, ResolvedColumnId = "done" };
        Assert.Equal("done", KanbanStatusRules.ResolveCommitColumnId(board));
    }

    [Fact]
    public void ResolveCommitColumnId_NullWhenNoColumns()
    {
        Assert.Null(KanbanStatusRules.ResolveCommitColumnId(new KanbanBoard()));
    }

    // ---- BoardStatuses semantics ----

    [Fact]
    public void IsOnBoard_OnlyCommittedAndResolved()
    {
        Assert.True(KanbanStatusRules.IsOnBoard(KanbanStoryStatus.Committed));
        Assert.True(KanbanStatusRules.IsOnBoard(KanbanStoryStatus.Resolved));
        Assert.False(KanbanStatusRules.IsOnBoard(KanbanStoryStatus.Backlog));
        Assert.False(KanbanStatusRules.IsOnBoard(KanbanStoryStatus.Unreviewed));
        Assert.False(KanbanStatusRules.IsOnBoard(KanbanStoryStatus.Archived));
        Assert.False(KanbanStatusRules.IsOnBoard(KanbanStoryStatus.Obsolete));
    }

    // ---- PlanStatusChange ----

    [Fact]
    public void PlanStatusChange_Commit_MovesToEntryColumnAndClearsResolved()
    {
        var board = BoardWith3Columns();
        var card = new KanbanCard { Status = KanbanStoryStatus.Backlog, ColumnId = "x", ResolvedOnDateTime = 555 };

        var plan = KanbanStatusRules.PlanStatusChange(card, KanbanStoryStatus.Committed, board, Now);

        Assert.Equal(KanbanStoryStatus.Committed, plan.Status);
        Assert.Equal("todo", plan.MoveToColumnId);
        Assert.True(plan.PlaceAtBoardEntry);
        Assert.True(plan.ResolvedChanged);
        Assert.Equal(0, plan.ResolvedOnDateTime);
    }

    [Fact]
    public void PlanStatusChange_Resolve_SnapsToResolvedColumnAndStampsTime()
    {
        var board = BoardWith3Columns();
        var card = new KanbanCard { Status = KanbanStoryStatus.Committed, ColumnId = "doing" };

        var plan = KanbanStatusRules.PlanStatusChange(card, KanbanStoryStatus.Resolved, board, Now);

        Assert.Equal(KanbanStoryStatus.Resolved, plan.Status);
        Assert.Equal("done", plan.MoveToColumnId);
        Assert.True(plan.PlaceAtBoardEntry);
        Assert.True(plan.ResolvedChanged);
        Assert.Equal(Now, plan.ResolvedOnDateTime);
    }

    [Fact]
    public void PlanStatusChange_Archive_ClearsColumnAndStampsTimeOnlyIfUnset()
    {
        var board = BoardWith3Columns();

        var fromResolved = new KanbanCard { Status = KanbanStoryStatus.Resolved, ColumnId = "done", ResolvedOnDateTime = 999 };
        var plan1 = KanbanStatusRules.PlanStatusChange(fromResolved, KanbanStoryStatus.Archived, board, Now);
        Assert.Equal(KanbanStoryStatus.Archived, plan1.Status);
        Assert.True(plan1.ClearColumn); // off the board — drop the column
        Assert.Null(plan1.MoveToColumnId);
        Assert.False(plan1.PlaceAtBoardEntry);
        Assert.False(plan1.ResolvedChanged); // already had a resolved time — keep it

        var manual = new KanbanCard { Status = KanbanStoryStatus.Backlog, ColumnId = "todo", ResolvedOnDateTime = 0 };
        var plan2 = KanbanStatusRules.PlanStatusChange(manual, KanbanStoryStatus.Archived, board, Now);
        Assert.True(plan2.ResolvedChanged);
        Assert.Equal(Now, plan2.ResolvedOnDateTime);
    }

    [Theory]
    [InlineData(KanbanStoryStatus.Backlog)]
    [InlineData(KanbanStoryStatus.Unreviewed)]
    [InlineData(KanbanStoryStatus.Obsolete)]
    public void PlanStatusChange_OffBoardStatuses_ClearColumnAndResolvedAndDoNotMove(KanbanStoryStatus target)
    {
        var board = BoardWith3Columns();
        var card = new KanbanCard { Status = KanbanStoryStatus.Resolved, ColumnId = "done", ResolvedOnDateTime = 123 };

        var plan = KanbanStatusRules.PlanStatusChange(card, target, board, Now);

        Assert.Equal(target, plan.Status);
        Assert.True(plan.ClearColumn);
        Assert.Null(plan.MoveToColumnId);
        Assert.False(plan.PlaceAtBoardEntry);
        Assert.True(plan.ResolvedChanged);
        Assert.Equal(0, plan.ResolvedOnDateTime);
    }

    [Fact]
    public void PlanStatusChange_Commit_DoesNotClearColumn()
    {
        var board = BoardWith3Columns();
        var card = new KanbanCard { Status = KanbanStoryStatus.Backlog, ColumnId = "" };

        var plan = KanbanStatusRules.PlanStatusChange(card, KanbanStoryStatus.Committed, board, Now);

        Assert.False(plan.ClearColumn);
        Assert.Equal("todo", plan.MoveToColumnId);
    }

    // ---- SyncStatusForColumnMove ----

    [Fact]
    public void SyncStatusForColumnMove_IntoResolved_SetsResolved()
    {
        var board = BoardWith3Columns();
        var card = new KanbanCard { Status = KanbanStoryStatus.Committed, ColumnId = "done" };

        KanbanStatusRules.SyncStatusForColumnMove(card, previousColumnId: "doing", board, Now);

        Assert.Equal(KanbanStoryStatus.Resolved, card.Status);
        Assert.Equal(Now, card.ResolvedOnDateTime);
    }

    [Fact]
    public void SyncStatusForColumnMove_OutOfResolved_ReCommits()
    {
        var board = BoardWith3Columns();
        var card = new KanbanCard { Status = KanbanStoryStatus.Resolved, ColumnId = "doing", ResolvedOnDateTime = 999 };

        KanbanStatusRules.SyncStatusForColumnMove(card, previousColumnId: "done", board, Now);

        Assert.Equal(KanbanStoryStatus.Committed, card.Status);
        Assert.Equal(0, card.ResolvedOnDateTime);
    }

    [Fact]
    public void SyncStatusForColumnMove_BetweenNonResolvedColumns_LeavesStatus()
    {
        var board = BoardWith3Columns();
        var card = new KanbanCard { Status = KanbanStoryStatus.Committed, ColumnId = "doing", ResolvedOnDateTime = 0 };

        KanbanStatusRules.SyncStatusForColumnMove(card, previousColumnId: "todo", board, Now);

        Assert.Equal(KanbanStoryStatus.Committed, card.Status);
        Assert.Equal(0, card.ResolvedOnDateTime);
    }

    [Fact]
    public void SyncStatusForColumnMove_NoResolvedColumnConfigured_NoOp()
    {
        var board = new KanbanBoard
        {
            Columns = new List<KanbanColumn> { new() { Id = "a", Position = 0 } },
            ResolvedColumnId = string.Empty,
        };
        var card = new KanbanCard { Status = KanbanStoryStatus.Committed, ColumnId = "a" };

        KanbanStatusRules.SyncStatusForColumnMove(card, previousColumnId: "b", board, Now);

        Assert.Equal(KanbanStoryStatus.Committed, card.Status);
    }
}
