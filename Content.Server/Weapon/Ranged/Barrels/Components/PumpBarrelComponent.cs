using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Weapon.Ranged.Ammunition.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Sound;
using Content.Shared.Weapons.Ranged.Barrels.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Players;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Server.Weapon.Ranged.Barrels.Components
{
    /// <summary>
    /// Bolt-action rifles
    /// </summary>
    [RegisterComponent]
    [NetworkedComponent()]
    public sealed class PumpBarrelComponent : ServerRangedBarrelComponent, IMapInit, ISerializationHooks
    {
        public override string Name => "PumpBarrel";

        public override int ShotsLeft
        {
            get
            {
                var chamberCount = _chamberContainer.ContainedEntity != null ? 1 : 0;
                return chamberCount + _spawnedAmmo.Count + _unspawnedCount;
            }
        }

        private const int DefaultCapacity = 6;
        [DataField("capacity")]
        public override int Capacity { get; } = DefaultCapacity;

        // Even a point having a chamber? I guess it makes some of the below code cleaner
        private ContainerSlot _chamberContainer = default!;
        private Stack<IEntity> _spawnedAmmo = new(DefaultCapacity - 1);
        private Container _ammoContainer = default!;

        [ViewVariables]
        [DataField("caliber")]
        private BallisticCaliber _caliber = BallisticCaliber.Unspecified;

        [ViewVariables]
        [DataField("fillPrototype")]
        private string? _fillPrototype;
        [ViewVariables]
        private int _unspawnedCount;

        [DataField("manualCycle")]
        private bool _manualCycle = true;

        private AppearanceComponent? _appearanceComponent;

        // Sounds
        [DataField("soundCycle")]
        private SoundSpecifier _soundCycle = new SoundPathSpecifier("/Audio/Weapons/Guns/Cock/sf_rifle_cock.ogg");

        [DataField("soundInsert")]
        private SoundSpecifier _soundInsert = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/bullet_insert.ogg");

        void IMapInit.MapInit()
        {
            if (_fillPrototype != null)
            {
                _unspawnedCount += Capacity - 1;
            }
            UpdateAppearance();
        }

        public override ComponentState GetComponentState(ICommonSession player)
        {
            (int, int)? count = (ShotsLeft, Capacity);
            var chamberedExists = _chamberContainer.ContainedEntity != null;
            // (Is one chambered?, is the bullet spend)
            var chamber = (chamberedExists, false);

            if (chamberedExists && _chamberContainer.ContainedEntity!.TryGetComponent<AmmoComponent>(out var ammo))
            {
                chamber.Item2 = ammo.Spent;
            }
            return new PumpBarrelComponentState(
                chamber,
                FireRateSelector,
                count,
                SoundGunshot.GetSound());
        }

        void ISerializationHooks.AfterDeserialization()
        {
            _spawnedAmmo = new Stack<IEntity>(Capacity - 1);
        }

        protected override void Initialize()
        {
            base.Initialize();

            _ammoContainer =
                ContainerHelpers.EnsureContainer<Container>(Owner, $"{Name}-ammo-container", out var existing);

            if (existing)
            {
                foreach (var entity in _ammoContainer.ContainedEntities)
                {
                    _spawnedAmmo.Push(entity);
                    _unspawnedCount--;
                }
            }

            _chamberContainer =
                ContainerHelpers.EnsureContainer<ContainerSlot>(Owner, $"{Name}-chamber-container", out existing);
            if (existing)
            {
                _unspawnedCount--;
            }

            if (Owner.TryGetComponent(out AppearanceComponent? appearanceComponent))
            {
                _appearanceComponent = appearanceComponent;
            }

            _appearanceComponent?.SetData(MagazineBarrelVisuals.MagLoaded, true);
            Dirty();
            UpdateAppearance();
        }

        private void UpdateAppearance()
        {
            _appearanceComponent?.SetData(AmmoVisuals.AmmoCount, ShotsLeft);
            _appearanceComponent?.SetData(AmmoVisuals.AmmoMax, Capacity);
        }

        public override IEntity? PeekAmmo()
        {
            return _chamberContainer.ContainedEntity;
        }

        public override IEntity? TakeProjectile(EntityCoordinates spawnAt)
        {
            var chamberEntity = _chamberContainer.ContainedEntity;
            if (!_manualCycle)
            {
                Cycle();
            }
            else
            {
                Dirty();
            }

            return chamberEntity?.GetComponentOrNull<AmmoComponent>()?.TakeBullet(spawnAt);
        }

        private void Cycle(bool manual = false)
        {
            var chamberedEntity = _chamberContainer.ContainedEntity;
            if (chamberedEntity != null)
            {
                _chamberContainer.Remove(chamberedEntity);
                var ammoComponent = chamberedEntity.GetComponent<AmmoComponent>();
                if (!ammoComponent.Caseless)
                {
                    EjectCasing(chamberedEntity);
                }
            }

            if (_spawnedAmmo.TryPop(out var next))
            {
                _ammoContainer.Remove(next);
                _chamberContainer.Insert(next);
            }

            if (_unspawnedCount > 0)
            {
                _unspawnedCount--;
                var ammoEntity = Owner.EntityManager.SpawnEntity(_fillPrototype, Owner.Transform.Coordinates);
                _chamberContainer.Insert(ammoEntity);
            }

            if (manual)
            {
                SoundSystem.Play(Filter.Pvs(Owner), _soundCycle.GetSound(), Owner, AudioParams.Default.WithVolume(-2));
            }

            Dirty();
            UpdateAppearance();
        }

        public bool TryInsertBullet(InteractUsingEventArgs eventArgs)
        {
            if (!eventArgs.Using.TryGetComponent(out AmmoComponent? ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != _caliber)
            {
                Owner.PopupMessage(eventArgs.User, Loc.GetString("pump-barrel-component-try-insert-bullet-wrong-caliber"));
                return false;
            }

            if (_ammoContainer.ContainedEntities.Count < Capacity - 1)
            {
                _ammoContainer.Insert(eventArgs.Using);
                _spawnedAmmo.Push(eventArgs.Using);
                Dirty();
                UpdateAppearance();
                SoundSystem.Play(Filter.Pvs(Owner), _soundInsert.GetSound(), Owner, AudioParams.Default.WithVolume(-2));
                return true;
            }

            Owner.PopupMessage(eventArgs.User, Loc.GetString("pump-barrel-component-try-insert-bullet-no-room"));

            return false;
        }

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            Cycle(true);
            return true;
        }

        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            return TryInsertBullet(eventArgs);
        }

        public override void Examine(FormattedMessage message, bool inDetailsRange)
        {
            base.Examine(message, inDetailsRange);

            message.AddMarkup("\n" + Loc.GetString("pump-barrel-component-on-examine", ("caliber", _caliber)));
        }
    }
}
