using System.Linq;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.Examine;
using Content.Server.Fluids.EntitySystems;
using Content.Server.NPC.Queries;
using Content.Server.NPC.Queries.Considerations;
using Content.Server.NPC.Queries.Curves;
using Content.Server.NPC.Queries.Queries;
using Content.Server.Nutrition.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Storage.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.SubFloor;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles utility queries for NPCs.
/// </summary>
public sealed class NPCUtilitySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FactionSystem _faction = default!;
    [Dependency] private readonly FoodSystem _food = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SolutionContainerSystem _solutions = default!;

    /// <summary>
    /// Runs the UtilityQueryPrototype and returns the best-matching entities.
    /// </summary>
    /// <param name="bestOnly">Should we only return the entity with the best score.</param>
    public UtilityResult GetEntities(
        NPCBlackboard blackboard,
        string proto,
        bool bestOnly = true)
    {
        // TODO: PickHostilesop or whatever needs to juse be UtilityQueryOperator

        var weh = _proto.Index<UtilityQueryPrototype>(proto);
        var ents = new HashSet<EntityUid>();

        foreach (var query in weh.Query)
        {
            switch (query)
            {
                case UtilityQueryFilter filter:
                    Filter(blackboard, ents, filter);
                    break;
                default:
                    Add(blackboard, ents, query);
                    break;
            }
        }

        if (ents.Count == 0)
            return UtilityResult.Empty;

        var results = new Dictionary<EntityUid, float>();
        var highestScore = 0f;

        foreach (var ent in ents)
        {
            if (results.Count > weh.Limit)
                break;

            var score = 1f;

            foreach (var con in weh.Considerations)
            {
                var conScore = GetScore(blackboard, ent, con);
                var curve = con.Curve;
                var curveScore = GetScore(curve, conScore);

                var adjusted = GetAdjustedScore(curveScore, weh.Considerations.Count);
                score *= adjusted;

                // If the score is too low OR we only care about best entity then early out.
                // Due to the adjusted score only being able to decrease it can never exceed the highest from here.
                if (score <= 0f || bestOnly && score <= highestScore)
                {
                    break;
                }
            }

            if (score <= 0f)
                continue;

            highestScore = MathF.Max(score, highestScore);
            results.Add(ent, score);
        }

        var result = new UtilityResult(results);
        blackboard.Remove<EntityUid>(NPCBlackboard.UtilityTarget);
        return result;
    }

    private float GetScore(IUtilityCurve curve, float conScore)
    {
        switch (curve)
        {
            case BoolCurve:
                return conScore > 0f ? 1f : 0f;
            case InverseBoolCurve:
                return conScore.Equals(0f) ? 1f : 0f;
            case PresetCurve presetCurve:
                return GetScore(_proto.Index<UtilityCurvePresetPrototype>(presetCurve.Preset).Curve, conScore);
            case QuadraticCurve quadraticCurve:
                return Math.Clamp(quadraticCurve.Slope * (float) Math.Pow(conScore - quadraticCurve.XOffset, quadraticCurve.Exponent) + quadraticCurve.YOffset, 0f, 1f);
            default:
                throw new NotImplementedException();
        }
    }

    private float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        switch (consideration)
        {
            case FoodValueCon:
            {
                if (!TryComp<FoodComponent>(targetUid, out var food))
                    return 0f;

                if (!_food.IsDigestibleBy(owner, targetUid, food))
                    return 0f;

                // no mouse don't eat the uranium-235
                var avoidBadFood = !HasComp<IgnoreBadFoodComponent>(owner);
                if (avoidBadFood && HasComp<BadFoodComponent>(targetUid))
                    return 0f;

                return 1f;
            }
            case TargetAccessibleCon:
            {
                if (TryComp<SubFloorHideComponent>(targetUid, out SubFloorHideComponent? subFloor))
                {
                    if (subFloor.IsUnderCover && subFloor.BlockInteractions)
                    {
                        return 0.0f;
                    }
                }

                if (_container.TryGetContainingContainer(targetUid, out var container))
                {
                    if (TryComp<EntityStorageComponent>(container.Owner, out var storageComponent))
                    {
                        if (storageComponent is { IsWeldedShut: true, Open: false })
                        {
                            return 0.0f;
                        }
                    }
                    else
                    {
                        // If we're in a container (e.g. held or whatever) then we probably can't get it. Only exception
                        // Is a locker / crate
                        // TODO: Some mobs can break it so consider that.
                        return 0.0f;
                    }
                }

                // TODO: Pathfind there, though probably do it in a separate con.
                return 1f;
            }
            case TargetAmmoMatchesCon:
            {
                if (!blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? activeHand, EntityManager) ||
                    !TryComp<BallisticAmmoProviderComponent>(activeHand.HeldEntity, out var heldGun))
                {
                    return 0f;
                }

                if (heldGun.Whitelist?.IsValid(targetUid, EntityManager) != true)
                {
                    return 0f;
                }

                return 1f;
            }
            case TargetDistanceCon:
            {
                var radius = blackboard.GetValueOrDefault<float>(NPCBlackboard.VisionRadius, EntityManager);

                if (!TryComp<TransformComponent>(targetUid, out var targetXform) ||
                    !TryComp<TransformComponent>(owner, out var xform))
                {
                    return 0f;
                }

                if (!targetXform.Coordinates.TryDistance(EntityManager, _transform, xform.Coordinates,
                        out var distance))
                {
                    return 0f;
                }

                return Math.Clamp(distance / radius, 0f, 1f);
            }
            case TargetHasAmmoCon:
            {
                if (!HasComp<GunComponent>(targetUid))
                    return 0f;

                var ev = new GetAmmoCountEvent();
                RaiseLocalEvent(targetUid, ref ev);

                if (ev.Count == 0)
                    return 0f;

                // Wat
                if (ev.Capacity == 0)
                    return 1f;

                return (float) ev.Count / ev.Capacity;
            }
            case TargetHealthCon:
            {
                return 0f;
            }
            case TargetInLOSCon:
            {
                var radius = blackboard.GetValueOrDefault<float>(NPCBlackboard.VisionRadius, EntityManager);

                return ExamineSystemShared.InRangeUnOccluded(owner, targetUid, radius + 0.5f, null) ? 1f : 0f;
            }
            case TargetInLOSOrCurrentCon:
            {
                var radius = blackboard.GetValueOrDefault<float>(NPCBlackboard.VisionRadius, EntityManager);
                const float bufferRange = 0.5f;

                if (blackboard.TryGetValue<EntityUid>("Target", out var currentTarget, EntityManager) &&
                    currentTarget == targetUid &&
                    TryComp<TransformComponent>(owner, out var xform) &&
                    TryComp<TransformComponent>(targetUid, out var targetXform) &&
                    xform.Coordinates.TryDistance(EntityManager, _transform, targetXform.Coordinates, out var distance) &&
                    distance <= radius + bufferRange)
                {
                    return 1f;
                }

                return ExamineSystemShared.InRangeUnOccluded(owner, targetUid, radius + bufferRange, null) ? 1f : 0f;
            }
            case TargetIsAliveCon:
            {
                return _mobState.IsAlive(targetUid) ? 1f : 0f;
            }
            case TargetIsCritCon:
            {
                return _mobState.IsCritical(targetUid) ? 1f : 0f;
            }
            case TargetIsDeadCon:
            {
                return _mobState.IsDead(targetUid) ? 1f : 0f;
            }
            case RandomCon:
            {
                return _random.NextFloat();
            }
            default:
                throw new NotImplementedException();
        }
    }

    private float GetAdjustedScore(float score, int considerations)
    {
        /*
        * Now using the geometric mean
        * for n scores you take the n-th root of the scores multiplied
        * e.g. a, b, c scores you take Math.Pow(a * b * c, 1/3)
        * To get the ACTUAL geometric mean at any one stage you'd need to divide by the running consideration count
        * however, the downside to this is it will fluctuate up and down over time.
        * For our purposes if we go below the minimum threshold we want to cut it off, thus we take a
        * "running geometric mean" which can only ever go down (and by the final value will equal the actual geometric mean).
        */

        var adjusted = MathF.Pow(score, 1 / (float) considerations);
        return Math.Clamp(adjusted, 0f, 1f);
    }

    private void Add(NPCBlackboard blackboard, HashSet<EntityUid> entities, UtilityQuery query)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var vision = blackboard.GetValueOrDefault<float>(NPCBlackboard.VisionRadius, EntityManager);
        var reach = blackboard.GetValueOrDefault<float>("InteractRange", EntityManager);

        switch (query)
        {
            case ComponentQuery compQuery:
                var mapPos = Transform(owner).MapPosition;
                var comps = compQuery.Components.Values.ToList();
                var compZero = comps[0];
                comps.RemoveAt(0);

                foreach (var comp in _lookup.GetComponentsInRange(compZero.Component.GetType(), mapPos, vision))
                {
                    var ent = comp.Owner;

                    if (ent == owner)
                        continue;

                    var othersFound = true;

                    foreach (var compOther in comps)
                    {
                        if (!HasComp(ent, compOther.Component.GetType()))
                        {
                            othersFound = false;
                            break;
                        }
                    }

                    if (!othersFound)
                        continue;

                    entities.Add(ent);
                }

                break;
            case NearbyHostilesQuery:
                foreach (var ent in _faction.GetNearbyHostiles(owner, vision))
                {
                    entities.Add(ent);
                }
                break;
            case InReachQuery:
                foreach (var ent in _lookup.GetEntitiesInRange(owner, reach, LookupFlags.Dynamic | LookupFlags.Static))
                {
                    if (!_interactionSystem.InRangeUnobstructed(owner, ent, reach, CollisionGroup.MobMask))
                        continue;
                    if (EntityManager.TryGetComponent(ent, out VisibilityComponent? vis) && vis.Layer != 1)
                        continue;
                    if (EntityManager.TryGetComponent(ent, out SubFloorHideComponent? subFloor) && subFloor.IsUnderCover)
                        continue;

                    entities.Add(ent);
                }
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private void Filter(NPCBlackboard blackboard, HashSet<EntityUid> entities, UtilityQueryFilter filter)
    {
        var toRemove = new ValueList<EntityUid>();

        switch (filter)
        {
            case PuddleFilter:
            {
                var puddleQuery = GetEntityQuery<PuddleComponent>();

                foreach (var ent in entities)
                {
                    if (!puddleQuery.TryGetComponent(ent, out var puddleComp) ||
                        !_solutions.TryGetSolution(ent, puddleComp.SolutionName, out Solution? sol) ||
                        _puddle.CanFullyEvaporate(sol))
                    {
                        toRemove.Add(ent);
                    }
                }

                break;
            }
            case SolutionFilter:
            {
                foreach (var ent in entities)
                {
                    if (!_solutions.TryGetSolution(ent, ((SolutionFilter) filter).Solution, out Solution? sol))
                    {
                        toRemove.Add(ent);
                        continue;
                    }

                    foreach (string reagent in ((SolutionFilter) filter).Reagents)
                    {
                        if (!sol.ContainsReagent(reagent))
                        {
                            toRemove.Add(ent);
                            continue;
                        }
                    }
                }
                break;
            }
            default:
                throw new NotImplementedException();
        }

        foreach (EntityUid ent in toRemove)
        {
            entities.Remove(ent);
        }
    }
}

public readonly record struct UtilityResult(Dictionary<EntityUid, float> Entities)
{
    public static readonly UtilityResult Empty = new(new Dictionary<EntityUid, float>());

    public readonly Dictionary<EntityUid, float> Entities = Entities;

    /// <summary>
    /// Returns the entity with the highest score.
    /// </summary>
    public EntityUid GetHighest()
    {
        if (Entities.Count == 0)
            return EntityUid.Invalid;

        return Entities.MaxBy(x => x.Value).Key;
    }

    /// <summary>
    /// Returns the entity with the lowest score. This does not consider entities with a 0 (invalid) score.
    /// </summary>
    public EntityUid GetLowest()
    {
        if (Entities.Count == 0)
            return EntityUid.Invalid;

        return Entities.MinBy(x => x.Value).Key;
    }
}
