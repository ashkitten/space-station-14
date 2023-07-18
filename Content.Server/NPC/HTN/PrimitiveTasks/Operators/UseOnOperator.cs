using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Timing;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

public sealed class UseOnOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = "CombatTarget";

    /// <summary>
    /// If this interaction started a do_after where does the key get stored.
    /// </summary>
    [DataField("idleKey")]
    public string IdleKey = "IdleTime";

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        return new(true, new Dictionary<string, object>()
        {
            { IdleKey, 1f }
        });
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        EntityUid owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        EntityUid target = blackboard.GetValue<EntityUid>(TargetKey);
        SharedInteractionSystem intSys = _entManager.System<SharedInteractionSystem>();
        int count = 0;

        IoCManager.Resolve<ILogManager>().RootSawmill.Debug($"UseOnOperator init, target {_entManager.ToPrettyString(target)}");

        if (!_entManager.TryGetComponent(owner, out HandsComponent? hands) || hands.ActiveHandEntity == null)
        {
            return HTNOperatorStatus.Failed;
        }

        if (_entManager.System<UseDelaySystem>().ActiveDelay(owner) ||
            !blackboard.TryGetValue(TargetKey, out EntityUid moveTarget, _entManager) ||
            !_entManager.TryGetComponent(moveTarget, out TransformComponent? targetXform))
        {
            return HTNOperatorStatus.Continuing;
        }

        if (_entManager.TryGetComponent(owner, out DoAfterComponent? doAfter))
        {
            count = doAfter.DoAfters.Count;
        }

        intSys.InteractUsing(owner, hands.ActiveHandEntity.Value, moveTarget, targetXform.Coordinates);

        _entManager.TryGetComponent<DoAfterComponent>(owner, out doAfter);

        // Interaction started a doafter so set the idle time to it.
        if (doAfter != null && count != doAfter.DoAfters.Count)
        {
            var wait = doAfter.DoAfters.Max(x => x.Value.Args.Delay);
            blackboard.SetValue(IdleKey, (float) wait.TotalSeconds + 0.5f);
        }
        else
        {
            blackboard.SetValue(IdleKey, 1f);
        }

        return HTNOperatorStatus.Finished;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<EntityUid>(TargetKey);
    }
}
