using Content.Server.Construction.Components;
using Content.Server.Stack;
using Content.Shared._CorvaxNext.Skills;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Random;
using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;

namespace Content.Server.Construction
{
    /// <summary>
    /// The server-side implementation of the construction system, which is used for constructing entities in game.
    /// </summary>
    [UsedImplicitly]
    public sealed partial class ConstructionSystem : SharedConstructionSystem
    {
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly ContainerSystem _container = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly SharedToolSystem _toolSystem = default!;
        [Dependency] private readonly SharedSkillsSystem _skills = default!;

        // Corvax-Next-Skills-Start
        private const float DelayModifierWithoutSkill = 30;

        private readonly HashSet<string> _advancedMaterials = ["Plasteel", "ReinforcedGlass", "ReinforcedPlasmaGlass", "ReinforcedUraniumGlass"];

        private readonly HashSet<string> _advancedConstructions = ["ComputerFrame", "MachineFrame"];
        // Corvax-Next-Skills-End

        public override void Initialize()
        {
            base.Initialize();

            InitializeComputer();
            InitializeGraphs();
            InitializeGuided();
            InitializeInteractions();
            InitializeInitial();
            InitializeMachines();

            SubscribeLocalEvent<ConstructionComponent, ComponentInit>(OnConstructionInit);
            SubscribeLocalEvent<ConstructionComponent, ComponentStartup>(OnConstructionStartup);
        }

        private void OnConstructionInit(Entity<ConstructionComponent> ent, ref ComponentInit args)
        {
            var construction = ent.Comp;
            if (GetCurrentGraph(ent, construction) is not {} graph)
            {
                Log.Warning($"Prototype {Comp<MetaDataComponent>(ent).EntityPrototype?.ID}'s construction component has an invalid graph specified.");
                return;
            }

            if (GetNodeFromGraph(graph, construction.Node) is not {} node)
            {
                Log.Warning($"Prototype {Comp<MetaDataComponent>(ent).EntityPrototype?.ID}'s construction component has an invalid node specified.");
                return;
            }

            ConstructionGraphEdge? edge = null;
            if (construction.EdgeIndex is {} edgeIndex)
            {
                if (GetEdgeFromNode(node, edgeIndex) is not {} currentEdge)
                {
                    Log.Warning($"Prototype {Comp<MetaDataComponent>(ent).EntityPrototype?.ID}'s construction component has an invalid edge index specified.");
                    return;
                }

                edge = currentEdge;
            }

            if (construction.TargetNode is {} targetNodeId)
            {
                if (GetNodeFromGraph(graph, targetNodeId) is not { } targetNode)
                {
                    Log.Warning($"Prototype {Comp<MetaDataComponent>(ent).EntityPrototype?.ID}'s construction component has an invalid target node specified.");
                    return;
                }

                UpdatePathfinding(ent, graph, node, targetNode, edge, construction);
            }
        }

        private void OnConstructionStartup(EntityUid uid, ConstructionComponent construction, ComponentStartup args)
        {
            if (GetCurrentNode(uid, construction) is not {} node)
                return;

            PerformActions(uid, null, node.Actions);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            UpdateInteractions();
        }

        // Corvax-Next-Skills-Start
        private bool IsAdvancedMaterial(EntityUid entity)
        {
            return TryComp<StackComponent>(entity, out var stack) && _advancedMaterials.Contains(stack.StackTypeId);
        }

        private bool IsAdvancedConstruction(EntityUid entity)
        {
            var prototype = Prototype(entity);

            return prototype is not null && _advancedConstructions.Contains(prototype.ID);
        }
        // Corvax-Next-Skills-End
    }
}
