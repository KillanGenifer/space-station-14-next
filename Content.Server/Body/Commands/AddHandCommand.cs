using System.Linq;
using Content.Server.Administration;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Shared.Administration;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Commands
{
    [AdminCommand(AdminFlags.Fun)]
    sealed class AddHandCommand : IConsoleCommand
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        [Dependency] private readonly IPrototypeManager _protoManager = default!;

        private static readonly EntProtoId DefaultHandPrototype = "LeftHandHuman";
        private static int _handIdAccumulator;

        public string Command => "addhand";
        public string Description => "Adds a hand to your entity.";
        public string Help => $"Usage: {Command} <entityUid> <handPrototypeId> / {Command} <entityUid> / {Command} <handPrototypeId> / {Command}";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;

            EntityUid entity;
            EntityUid hand;

            switch (args.Length)
            {
                case 0:
                    if (player == null)
                    {
                        shell.WriteLine("Only a player can run this command without arguments.");
                        return;
                    }

                    if (player.AttachedEntity == null)
                    {
                        shell.WriteLine("You don't have an entity to add a hand to.");
                        return;
                    }

                    entity = player.AttachedEntity.Value;
                    hand = _entManager.SpawnEntity(DefaultHandPrototype, _entManager.GetComponent<TransformComponent>(entity).Coordinates);
                    break;
                case 1:
                    {
                        if (NetEntity.TryParse(args[0], out var uidNet) && _entManager.TryGetEntity(uidNet, out var uid))
                        {
                            if (!_entManager.EntityExists(uid))
                            {
                                shell.WriteLine($"No entity found with uid {uid}");
                                return;
                            }

                            entity = uid.Value;
                            hand = _entManager.SpawnEntity(DefaultHandPrototype, _entManager.GetComponent<TransformComponent>(entity).Coordinates);
                        }
                        else
                        {
                            if (player == null)
                            {
                                shell.WriteLine("You must specify an entity to add a hand to when using this command from the server terminal.");
                                return;
                            }

                            if (player.AttachedEntity == null)
                            {
                                shell.WriteLine("You don't have an entity to add a hand to.");
                                return;
                            }

                            entity = player.AttachedEntity.Value;
                            hand = _entManager.SpawnEntity(args[0], _entManager.GetComponent<TransformComponent>(entity).Coordinates);
                        }

                        break;
                    }
                case 2:
                    {
                        if (!NetEntity.TryParse(args[0], out var netEnt) || !_entManager.TryGetEntity(netEnt, out var uid))
                        {
                            shell.WriteLine($"{args[0]} is not a valid entity uid.");
                            return;
                        }

                        if (!_entManager.EntityExists(uid))
                        {
                            shell.WriteLine($"No entity exists with uid {uid}.");
                            return;
                        }

                        entity = uid.Value;

                        if (!_protoManager.HasIndex<EntityPrototype>(args[1]))
                        {
                            shell.WriteLine($"No hand entity exists with id {args[1]}.");
                            return;
                        }

                        hand = _entManager.SpawnEntity(args[1], _entManager.GetComponent<TransformComponent>(entity).Coordinates);

                        break;
                    }
                default:
                    shell.WriteLine(Help);
                    return;
            }

            if (!_entManager.TryGetComponent(entity, out BodyComponent? body) || body.RootContainer.ContainedEntity == null)
            {
                var location = _entManager.GetComponentOrNull<BodyPartComponent>(hand)?.Symmetry switch
                {
                    BodyPartSymmetry.None => HandLocation.Middle,
                    BodyPartSymmetry.Left => HandLocation.Left,
                    BodyPartSymmetry.Right => HandLocation.Right,
                    _ => HandLocation.Right
                };
                _entManager.DeleteEntity(hand);

                // You have no body and you must scream.
                _entManager.System<HandsSystem>().AddHand(entity, $"{hand}-cmd-{_handIdAccumulator++}", location);
                return;
            }

            if (!_entManager.TryGetComponent(hand, out BodyPartComponent? part))
            {
                shell.WriteLine($"Hand entity {hand} does not have a {nameof(BodyPartComponent)} component.");
                return;
            }

            var bodySystem = _entManager.System<BodySystem>();

            var attachAt = bodySystem.GetBodyChildrenOfType(entity, BodyPartType.Arm, body).FirstOrDefault();
            if (attachAt == default)
                attachAt = bodySystem.GetBodyChildren(entity, body).First();

            // Shitmed Change Start
            var slotId = $"{part.Symmetry.ToString().ToLower()} {part.GetHashCode().ToString()}";
            part.SlotId = part.GetHashCode().ToString();
            // Shitmed Change End

            if (!bodySystem.TryCreatePartSlotAndAttach(attachAt.Id, slotId, hand, BodyPartType.Hand, attachAt.Component, part))
            {
                shell.WriteError($"Couldn't create a slot with id {slotId} on entity {_entManager.ToPrettyString(entity)}");
                return;
            }

            shell.WriteLine($"Added hand to entity {_entManager.GetComponent<MetaDataComponent>(entity).EntityName}");
        }
    }
}
