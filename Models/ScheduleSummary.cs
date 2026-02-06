using System;
using System.Collections.Generic;

namespace vkine.Models;

public sealed class ScheduleSummary
{
    public int MovieId { get; init; }
    public string MovieTitle { get; init; } = string.Empty;
    public DateOnly? FirstDate { get; init; }
    public IReadOnlyList<ScheduleSummaryBadge> Badges { get; init; } = Array.Empty<ScheduleSummaryBadge>();
    public IReadOnlyList<ScheduleBadgeSignature> ShowtimeBadgeSignatures { get; init; } = Array.Empty<ScheduleBadgeSignature>();
}

public sealed class ScheduleSummaryBadge
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class ScheduleBadgeSignature
{
    public IReadOnlyList<string> Badges { get; init; } = Array.Empty<string>();
}
