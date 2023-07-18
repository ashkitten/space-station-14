using Content.Server.Hands.Systems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators
{
    public sealed class DropItemOperator : HTNOperator
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            EntityUid owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            HandsSystem handsSys = _entManager.System<HandsSystem>();

            return handsSys.TryDrop(owner) ? HTNOperatorStatus.Finished : HTNOperatorStatus.Failed;
        }
    }
}
