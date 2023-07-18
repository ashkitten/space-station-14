using Content.Shared.Hands.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.HTN.Preconditions;

public sealed class HoldingComponentsPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

	[DataField("components", required: true)]
	public ComponentRegistry Components = default!;

	public override bool IsMet(NPCBlackboard blackboard)
	{
		var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<HandsComponent>(owner, out var hands) ||  hands.ActiveHandEntity == null)
            return false;

		foreach (var compReg in Components.Values)
		{
			if (!_entManager.HasComponent(hands.ActiveHandEntity, compReg.Component.GetType()))
				return false;
		}

		return true;
	}
}
