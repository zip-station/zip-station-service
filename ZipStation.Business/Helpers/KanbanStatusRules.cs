using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Business.Helpers;

/// Pure rules describing how a story's <see cref="KanbanStoryStatus"/> lifecycle interacts with the
/// board's columns. Kept free of I/O so it can be unit-tested directly; callers feed in the board
/// and the "now" timestamp and apply the returned plan (fetching column positions themselves).
public static class KanbanStatusRules
{
    /// What the backlog grid shows when the caller doesn't specify a status filter. Includes
    /// Unreviewed so freshly-imported (e.g. Discord) intake is visible by default and doesn't look
    /// like it never arrived — the triage queue is front-and-center, not hidden behind a chip.
    public static readonly IReadOnlyList<KanbanStoryStatus> DefaultGridStatuses =
        new[] { KanbanStoryStatus.Unreviewed, KanbanStoryStatus.Backlog, KanbanStoryStatus.Committed };

    /// Statuses the kanban board (columns) renders. Everything else lives only in the grid.
    public static readonly IReadOnlyList<KanbanStoryStatus> BoardStatuses =
        new[] { KanbanStoryStatus.Committed, KanbanStoryStatus.Resolved };

    public static bool IsOnBoard(KanbanStoryStatus status) =>
        status is KanbanStoryStatus.Committed or KanbanStoryStatus.Resolved;

    /// The column a story enters when it's committed onto the board: the lowest-position column
    /// that isn't the resolved/done column. Falls back to the lowest-position column if every
    /// column is the resolved one (degenerate single-column board). Null only if the board has no
    /// columns, which the caller must guard against.
    public static string? ResolveCommitColumnId(KanbanBoard board)
    {
        var entry = board.Columns
            .Where(c => c.Id != board.ResolvedColumnId)
            .OrderBy(c => c.Position)
            .FirstOrDefault();
        entry ??= board.Columns.OrderBy(c => c.Position).FirstOrDefault();
        return entry?.Id;
    }

    /// Parse a caller-supplied status filter (names, case-insensitive). Unknown names are dropped.
    /// Empty/absent input yields <see cref="DefaultGridStatuses"/>.
    public static List<KanbanStoryStatus> ParseStatusFilter(IEnumerable<string>? raw)
    {
        var values = (raw ?? Enumerable.Empty<string>())
            .SelectMany(s => (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(s => Enum.TryParse<KanbanStoryStatus>(s, ignoreCase: true, out var v) ? (KanbanStoryStatus?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        return values.Count > 0 ? values : DefaultGridStatuses.ToList();
    }

    /// Plan an explicit status transition (Commit / Obsolete / Archive / Resolve / send-to-Backlog /
    /// mark-Unreviewed). Returns how the card's column / position / resolved-timestamp should change;
    /// the caller mutates the card and resolves the board-entry position from the repository.
    public static StatusChangePlan PlanStatusChange(KanbanCard card, KanbanStoryStatus target, KanbanBoard board, long nowUnixMs)
    {
        switch (target)
        {
            case KanbanStoryStatus.Committed:
                // Drop onto the board's first workflow column; clear any prior resolution.
                return new StatusChangePlan
                {
                    Status = target,
                    MoveToColumnId = ResolveCommitColumnId(board),
                    PlaceAtBoardEntry = true,
                    ResolvedChanged = card.ResolvedOnDateTime != 0,
                    ResolvedOnDateTime = 0,
                };

            case KanbanStoryStatus.Resolved:
                // Snap to the done column (if configured) and stamp the resolution time.
                var resolvedColumn = string.IsNullOrEmpty(board.ResolvedColumnId) ? null : board.ResolvedColumnId;
                return new StatusChangePlan
                {
                    Status = target,
                    MoveToColumnId = resolvedColumn,
                    PlaceAtBoardEntry = resolvedColumn != null,
                    ResolvedChanged = true,
                    ResolvedOnDateTime = nowUnixMs,
                };

            case KanbanStoryStatus.Archived:
                // Filed away — off the board, so it no longer belongs to a column. Stamp the
                // resolution time if it wasn't already (manual archive of something that never
                // passed through the resolved column).
                return new StatusChangePlan
                {
                    Status = target,
                    MoveToColumnId = null,
                    ClearColumn = true,
                    PlaceAtBoardEntry = false,
                    ResolvedChanged = card.ResolvedOnDateTime == 0,
                    ResolvedOnDateTime = nowUnixMs,
                };

            // Backlog, Unreviewed, Obsolete — all leave the board; clear the column and resolution.
            default:
                return new StatusChangePlan
                {
                    Status = target,
                    MoveToColumnId = null,
                    ClearColumn = true,
                    PlaceAtBoardEntry = false,
                    ResolvedChanged = card.ResolvedOnDateTime != 0,
                    ResolvedOnDateTime = 0,
                };
        }
    }

    /// Keep status in sync when a board card is dragged between columns (no explicit status given).
    /// Moving into the resolved column resolves it; moving back out re-commits it. Mutates the card.
    public static void SyncStatusForColumnMove(KanbanCard card, string previousColumnId, KanbanBoard board, long nowUnixMs)
    {
        if (string.IsNullOrEmpty(board.ResolvedColumnId)) return;

        var movedIntoResolved = previousColumnId != board.ResolvedColumnId && card.ColumnId == board.ResolvedColumnId;
        var movedOutOfResolved = previousColumnId == board.ResolvedColumnId && card.ColumnId != board.ResolvedColumnId;

        if (movedIntoResolved)
        {
            card.Status = KanbanStoryStatus.Resolved;
            card.ResolvedOnDateTime = nowUnixMs;
        }
        else if (movedOutOfResolved)
        {
            card.Status = KanbanStoryStatus.Committed;
            card.ResolvedOnDateTime = 0;
        }
    }
}

public sealed class StatusChangePlan
{
    public KanbanStoryStatus Status { get; init; }

    /// Column to move the card into, or null to leave it where it is.
    public string? MoveToColumnId { get; init; }

    /// When true the card is leaving the board, so the caller clears its <c>ColumnId</c> — off-board
    /// statuses (Unreviewed / Backlog / Archived / Obsolete) don't belong to any column. Takes
    /// precedence over <see cref="MoveToColumnId"/> (which stays null in that case).
    public bool ClearColumn { get; init; }

    /// When true the caller assigns a fresh board position (max-in-column + step) after the move.
    public bool PlaceAtBoardEntry { get; init; }

    /// Whether <see cref="ResolvedOnDateTime"/> should be written.
    public bool ResolvedChanged { get; init; }

    public long ResolvedOnDateTime { get; init; }
}
