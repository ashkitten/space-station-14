using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Hands.Components;

namespace Content.Server.NPC.HTN.Preconditions
{
    public sealed class HoldingReagentsPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        [DataField("solution")]
        public string SolutionName = "default";

        [DataField("reagents")]
        public List<string> Reagents = default!;

        public override bool IsMet(NPCBlackboard blackboard)
        {
            EntityUid owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            SolutionContainerSystem solutionContainerSys = _entManager.System<SolutionContainerSystem>();

            if (!_entManager.TryGetComponent(owner, out HandsComponent? hands) || hands.ActiveHandEntity == null)
                return false;

            if (!solutionContainerSys.TryGetSolution(hands.ActiveHandEntity, SolutionName, out Solution? solution))
                return false;

            foreach (string reagentId in Reagents)
            {
                if (!solution.ContainsReagent(reagentId))
                    return false;
            }

            return true;
        }
    }
}
