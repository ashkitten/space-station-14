using Content.Shared.Hands.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Utility;

namespace Content.Server.NPC.HTN.Preconditions
{
    public sealed class HoldingToolPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        [DataField("qualities")]
        public PrototypeFlags<ToolQualityPrototype> Qualities = new();

        public override bool IsMet(NPCBlackboard blackboard)
        {
            EntityUid owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

            if (!_entManager.TryGetComponent(owner, out HandsComponent? hands) || hands.ActiveHandEntity == null)
                return false;

            if (!_entManager.TryGetComponent(hands.ActiveHandEntity, out ToolComponent? tool))
                return false;

            return tool.Qualities.ContainsAll(Qualities);
        }
    }
}