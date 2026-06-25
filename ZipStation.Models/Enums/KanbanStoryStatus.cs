namespace ZipStation.Models.Enums;

/// The lifecycle bucket a kanban story lives in. This is orthogonal to the board's columns:
/// columns describe the in-progress workflow lanes for <see cref="Committed"/> work, while the
/// status describes where the story sits in its overall lifecycle (triage → backlog → board →
/// done → filed away / scrapped).
///
/// Serialized to BSON as an int (the codebase convention — see <c>TicketPriority</c>). Do not
/// reorder existing members; append new ones with explicit values. Legacy cards written before
/// this field existed have no value stored; they read back as <see cref="Committed"/> (the entity
/// initializer default) and are flipped to a real stored value by the startup migration.
public enum KanbanStoryStatus
{
    /// Auto-created from an external source (Discord, etc.) and not yet triaged by a human.
    Unreviewed = 0,

    /// Reviewed and accepted, prioritized in the backlog, but not yet committed to the board.
    Backlog = 1,

    /// Actively being worked — this is what the kanban board (columns) renders.
    Committed = 2,

    /// Committed work that reached the board's resolved column. Still recent/visible; the worker
    /// archives it after the project's KanbanArchiveDays.
    Resolved = 3,

    /// Done and filed away — either auto-archived after KanbanArchiveDays or archived manually.
    /// Reversible. Distinct from <see cref="Obsolete"/>: archived work WAS handled.
    Archived = 4,

    /// Scrapped / won't-do — a deliberate decision that the work should never happen. Manual only.
    /// Distinct from <see cref="Archived"/>: obsolete work was NOT completed.
    Obsolete = 5,
}
