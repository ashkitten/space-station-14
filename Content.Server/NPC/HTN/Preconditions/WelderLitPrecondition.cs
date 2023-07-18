using Content.Server.IgnitionSource;
using Content.Shared.Hands.Components;

namespace Content.Server.NPC.HTN.Preconditions;

public sealed class WelderLitPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

	public override bool IsMet(NPCBlackboard blackboard)
	{
		var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<HandsComponent>(owner, out var hands) ||  hands.ActiveHandEntity == null)
            return false;

		if (!_entManager.TryGetComponent(hands.ActiveHandEntity, out IgnitionSourceComponent? ignitionSource))
			return false;

		return ignitionSource.Ignited;
	}
}
