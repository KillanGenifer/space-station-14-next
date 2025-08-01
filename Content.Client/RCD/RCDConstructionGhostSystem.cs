using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Client.Hands.Systems;
using Content.Shared.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

using Robust.Shared.Prototypes;
using Content.Shared.RCD.Systems;

namespace Content.Client.RCD;

/// <summary>
/// System for handling structure ghost placement in places where RCD can create objects.
/// </summary>
public sealed class RCDConstructionGhostSystem : EntitySystem
{
    private const string PlacementMode = nameof(AlignRCDConstruction);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlacementManager _placementManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly RCDSystem _rcdSystem = default!;

    private string _placementMode = typeof(AlignRCDConstruction).Name;
    [Dependency] private readonly HandsSystem _hands = default!;

    private Direction _placementDirection = default;
    private bool _useMirrorPrototype = false;
    public event EventHandler? FlipConstructionPrototype;

    public override void Initialize()
    {
        base.Initialize();

        // bind key
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.EditorFlipObject,
                new PointerInputCmdHandler(HandleFlip, outsidePrediction: true))
            .Register<RCDConstructionGhostSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<RCDConstructionGhostSystem>();
        base.Shutdown();
    }

    private bool HandleFlip(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State == BoundKeyState.Down)
        {
            if (!_placementManager.IsActive || _placementManager.Eraser)
                return false;

            var placerEntity = _placementManager.CurrentPermission?.MobUid;

            if(!TryComp<RCDComponent>(placerEntity, out var rcd) ||
                string.IsNullOrEmpty(rcd.CachedPrototype.MirrorPrototype))
                return false;

            _useMirrorPrototype = !rcd.UseMirrorPrototype;

            var useProto = _useMirrorPrototype ? rcd.CachedPrototype.MirrorPrototype : rcd.CachedPrototype.Prototype;
            CreatePlacer(placerEntity.Value, rcd, useProto);

            // tell the server

            RaiseNetworkEvent(new RCDConstructionGhostFlipEvent(GetNetEntity(placerEntity.Value), _useMirrorPrototype));
        }

        return true;
    }


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Get current placer data
        var placerEntity = _placementManager.CurrentPermission?.MobUid;
        var placerProto = _placementManager.CurrentPermission?.EntityType;
        var placerIsRCD = HasComp<RCDComponent>(placerEntity);

        // Exit if erasing or the current placer is not an RCD (build mode is active)
        if (_placementManager.Eraser || (placerEntity != null && !placerIsRCD))
            return;

        // Determine if player is carrying an RCD in their active hand
        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return;

        var heldEntity = _hands.GetActiveItem(player);

        if (!TryComp<RCDComponent>(heldEntity, out var rcd))
        {
            // If the player was holding an RCD, but is no longer, cancel placement
            if (placerIsRCD)
                _placementManager.Clear();

            return;
        }

        // Update the direction the RCD prototype based on the placer direction
        if (_placementDirection != _placementManager.Direction)
        {
            _placementDirection = _placementManager.Direction;
            RaiseNetworkEvent(new RCDConstructionGhostRotationEvent(GetNetEntity(heldEntity.Value), _placementDirection));
        }

        // If the placer has not changed build it.
        _rcdSystem.UpdateCachedPrototype(heldEntity.Value, rcd);
        var useProto = (_useMirrorPrototype && !string.IsNullOrEmpty(rcd.CachedPrototype.MirrorPrototype)) ? rcd.CachedPrototype.MirrorPrototype : rcd.CachedPrototype.Prototype;

        if (heldEntity != placerEntity || useProto != placerProto)
        {
            CreatePlacer(heldEntity.Value, rcd, useProto);
        }


    }

    private void CreatePlacer(EntityUid uid, RCDComponent component, string? prototype)
    {
        // Create a new placer
        var newObjInfo = new PlacementInformation
        {
            MobUid = uid,
            PlacementOption = PlacementMode,
            EntityType = prototype,
            Range = (int) Math.Ceiling(SharedInteractionSystem.InteractionRange),
            IsTile = (component.CachedPrototype.Mode == RcdMode.ConstructTile),
            UseEditorContext = false,
        };

        _placementManager.Clear();
        _placementManager.BeginPlacing(newObjInfo);
    }
}
