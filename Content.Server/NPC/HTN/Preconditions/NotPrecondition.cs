namespace Content.Server.NPC.HTN.Preconditions;

public sealed class NotPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

	[DataField("precondition")]
	public HTNPrecondition Precondition = default!;

	public override void Initialize(IEntitySystemManager sysManager)
	{
		base.Initialize(sysManager);
		Precondition.Initialize(sysManager);
	}

	public override bool IsMet(NPCBlackboard blackboard)
	{
		return !Precondition.IsMet(blackboard);
	}
}
