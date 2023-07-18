using Content.Shared.DoAfter;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Waits for all DoAfters to be complete.
/// </summary>
public sealed class WaitDoAfterOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
        {
			if (doAfter.DoAfters.Count > 0) {
				return HTNOperatorStatus.Continuing;
			}
		}

		return HTNOperatorStatus.Finished;
    }
}
