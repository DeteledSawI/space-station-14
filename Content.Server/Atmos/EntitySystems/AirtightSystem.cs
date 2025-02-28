using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.Atmos.EntitySystems
{
    [UsedImplicitly]
    public class AirtightSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<AirtightComponent, ComponentInit>(OnAirtightInit);
            SubscribeLocalEvent<AirtightComponent, ComponentShutdown>(OnAirtightShutdown);
            SubscribeLocalEvent<AirtightComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<AirtightComponent, AnchorStateChangedEvent>(OnAirtightPositionChanged);
            SubscribeLocalEvent<AirtightComponent, RotateEvent>(OnAirtightRotated);
        }

        private void OnAirtightInit(EntityUid uid, AirtightComponent airtight, ComponentInit args)
        {
            if (airtight.FixAirBlockedDirectionInitialize)
            {
                var rotateEvent = new RotateEvent(airtight.Owner, Angle.Zero, airtight.Owner.Transform.WorldRotation);
                OnAirtightRotated(uid, airtight, ref rotateEvent);
            }

            // Adding this component will immediately anchor the entity, because the atmos system
            // requires airtight entities to be anchored for performance.
            airtight.Owner.Transform.Anchored = true;

            UpdatePosition(airtight);
        }

        private void OnAirtightShutdown(EntityUid uid, AirtightComponent airtight, ComponentShutdown args)
        {
            SetAirblocked(airtight, false);

            InvalidatePosition(airtight.LastPosition.Item1, airtight.LastPosition.Item2, airtight.FixVacuum);
        }

        private void OnMapInit(EntityUid uid, AirtightComponent airtight, MapInitEvent args)
        {
        }

        private void OnAirtightPositionChanged(EntityUid uid, AirtightComponent airtight, ref AnchorStateChangedEvent args)
        {
            var gridId = airtight.Owner.Transform.GridID;
            var coords = airtight.Owner.Transform.Coordinates;

            var grid = _mapManager.GetGrid(gridId);
            var tilePos = grid.TileIndicesFor(coords);

            // Update and invalidate new position.
            airtight.LastPosition = (gridId, tilePos);
            InvalidatePosition(gridId, tilePos);
        }

        private void OnAirtightRotated(EntityUid uid, AirtightComponent airtight, ref RotateEvent ev)
        {
            if (!airtight.RotateAirBlocked || airtight.InitialAirBlockedDirection == (int)AtmosDirection.Invalid)
                return;

            airtight.CurrentAirBlockedDirection = (int) Rotate((AtmosDirection)airtight.InitialAirBlockedDirection, ev.NewRotation);
            UpdatePosition(airtight);
        }

        public void SetAirblocked(AirtightComponent airtight, bool airblocked)
        {
            airtight.AirBlocked = airblocked;
            UpdatePosition(airtight);
        }

        public void UpdatePosition(AirtightComponent airtight)
        {
            if (!airtight.Owner.Transform.Anchored || !airtight.Owner.Transform.GridID.IsValid())
                return;

            var grid = _mapManager.GetGrid(airtight.Owner.Transform.GridID);
            airtight.LastPosition = (airtight.Owner.Transform.GridID, grid.TileIndicesFor(airtight.Owner.Transform.Coordinates));
            InvalidatePosition(airtight.LastPosition.Item1, airtight.LastPosition.Item2, airtight.FixVacuum && !airtight.AirBlocked);
        }

        public void InvalidatePosition(GridId gridId, Vector2i pos, bool fixVacuum = false)
        {
            if (!gridId.IsValid())
                return;

            _atmosphereSystem.UpdateAdjacent(gridId, pos);
            _atmosphereSystem.InvalidateTile(gridId, pos);

            if(fixVacuum)
                _atmosphereSystem.FixVacuum(gridId, pos);
        }

        private AtmosDirection Rotate(AtmosDirection myDirection, Angle myAngle)
        {
            var newAirBlockedDirs = AtmosDirection.Invalid;

            if (myAngle == Angle.Zero)
                return myDirection;

            // TODO ATMOS MULTIZ: When we make multiZ atmos, special case this.
            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var direction = (AtmosDirection) (1 << i);
                if (!myDirection.IsFlagSet(direction)) continue;
                var angle = direction.ToAngle();
                angle += myAngle;
                newAirBlockedDirs |= angle.ToAtmosDirectionCardinal();
            }

            return newAirBlockedDirs;
        }
    }
}
