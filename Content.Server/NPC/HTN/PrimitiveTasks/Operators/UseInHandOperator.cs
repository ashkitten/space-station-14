using Content.Server.Interaction;
using Content.Shared.Hands.Components;
using Content.Shared.Timing;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators
{
    public sealed class UseInHandOperator : HTNOperator
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            EntityUid owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            InteractionSystem intSys = _entManager.System<InteractionSystem>();

            if (_entManager.System<UseDelaySystem>().ActiveDelay(owner))
            {
                return HTNOperatorStatus.Continuing;
            }

            if (!_entManager.TryGetComponent(owner, out HandsComponent? hands) || hands.ActiveHandEntity == null)
            {
                return HTNOperatorStatus.Failed;
            }

            if (!intSys.UseInHandInteraction(owner, hands.ActiveHandEntity.Value))
            {
                return HTNOperatorStatus.Failed;
            }

            return HTNOperatorStatus.Finished;
        }
    }
}