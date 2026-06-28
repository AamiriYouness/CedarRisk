namespace CedarRisk.Domain.Common;

/// <summary>Represents a command result with no meaningful value — analogous to void in Result&lt;T&gt;.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

