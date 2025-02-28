﻿using Content.Shared.Singularity;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Client.Singularity.Visualizers
{
    [UsedImplicitly]
    public class SingularityVisualizer : AppearanceVisualizer
    {
        [DataField("layer")]
        private int Layer { get; } = 0;

        public override void InitializeEntity(IEntity entity)
        {
            base.InitializeEntity(entity);

            entity.GetComponentOrNull<SpriteComponent>()?.LayerMapReserveBlank(Layer);
        }

        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);

            if (!component.Owner.TryGetComponent(out SpriteComponent? sprite))
            {
                return;
            }

            if (!component.TryGetData(SingularityVisuals.Level, out int level))
            {
                return;
            }

            sprite.LayerSetSprite(Layer, new SpriteSpecifier.Rsi(new ResourcePath("Structures/Power/Generation/Singularity/singularity_" + level + ".rsi"), "singularity_" + level));
        }
    }
}
