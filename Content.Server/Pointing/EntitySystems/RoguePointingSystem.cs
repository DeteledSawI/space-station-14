using System.Linq;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Pointing.Components;
using Content.Shared.MobState.Components;
using Content.Shared.Pointing.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Server.Pointing.EntitySystems
{
    [UsedImplicitly]
    internal sealed class RoguePointingSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        [Dependency] private readonly ExplosionSystem _explosions = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RoguePointingArrowComponent, ComponentStartup>(OnStartup);
        }

        private void OnStartup(EntityUid uid, RoguePointingArrowComponent component, ComponentStartup args)
        {
            if (EntityManager.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                sprite.DrawDepth = (int) DrawDepth.Overlays;
            }
        }

        private IEntity? RandomNearbyPlayer(EntityUid uid, RoguePointingArrowComponent? component = null, TransformComponent? transform = null)
        {
            if (!Resolve(uid, ref component, ref transform))
                return null;

            var players = Filter.Empty()
                .AddPlayersByPvs(transform.MapPosition)
                .RemoveWhereAttachedEntity(euid => !EntityManager.TryGetComponent(euid, out MobStateComponent? mobStateComponent) || mobStateComponent.IsDead())
                .Recipients
                .ToArray();

            return players.Length != 0
                ? _random.Pick(players).AttachedEntity
                : null;
        }

        private void UpdateAppearance(EntityUid uid, RoguePointingArrowComponent? component = null, TransformComponent? transform = null, AppearanceComponent? appearance = null)
        {
            if (!Resolve(uid, ref component, ref transform, ref appearance) || component.Chasing == null)
                return;

            appearance.SetData(RoguePointingArrowVisuals.Rotation, transform.LocalRotation.Degrees);
        }

        public override void Update(float frameTime)
        {
            foreach (var (component, transform) in EntityManager.EntityQuery<RoguePointingArrowComponent, TransformComponent>())
            {
                var uid = component.Owner.Uid;
                component.Chasing ??= RandomNearbyPlayer(uid, component, transform);

                if (component.Chasing == null)
                {
                    EntityManager.QueueDeleteEntity(uid);
                    return;
                }

                component.TurningDelay -= frameTime;

                if (component.TurningDelay > 0)
                {
                    var difference = component.Chasing.Transform.WorldPosition - transform.WorldPosition;
                    var angle = difference.ToAngle();
                    var adjusted = angle.Degrees + 90;
                    var newAngle = Angle.FromDegrees(adjusted);

                    transform.LocalRotation = newAngle;

                    UpdateAppearance(uid, component, transform);
                    return;
                }

                transform.WorldRotation += Angle.FromDegrees(20);

                UpdateAppearance(uid, component, transform);

                var toChased = component.Chasing.Transform.WorldPosition - transform.WorldPosition;

                transform.WorldPosition += toChased * frameTime * component.ChasingSpeed;

                component.ChasingTime -= frameTime;

                if (component.ChasingTime > 0)
                {
                    return;
                }

                _explosions.SpawnExplosion(uid, 0, 2, 1, 1);
                SoundSystem.Play(Filter.Pvs(uid, entityManager: EntityManager), component.ExplosionSound.GetSound(), uid);

                EntityManager.QueueDeleteEntity(uid);
            }
        }
    }
}
