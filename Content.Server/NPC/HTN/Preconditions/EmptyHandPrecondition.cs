using Content.Shared.Hands.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.HTN.Preconditions;

public sealed class EmptyHandPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

	[DataField("components", required: true)]
	public ComponentRegistry Components = default!;

	public override bool IsMet(NPCBlackboard blackboard)
	{
		var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<HandsComponent>(owner, out var hands) || hands.ActiveHand == null)
            return false;

		return hands.ActiveHand.IsEmpty;
	}
}
