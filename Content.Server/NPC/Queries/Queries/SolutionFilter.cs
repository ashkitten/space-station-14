namespace Content.Server.NPC.Queries.Queries;

public sealed class SolutionFilter : UtilityQueryFilter
{
    [DataField("solution")]
    public string Solution = "default";

    [DataField("reagents")]
    public List<string> Reagents = default!;
}
