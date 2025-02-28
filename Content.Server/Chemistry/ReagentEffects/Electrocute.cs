using System;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Electrocution;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Chemistry.ReagentEffects;

public class Electrocute : ReagentEffect
{
    [DataField("electrocuteTime")] public int ElectrocuteTime = 2;

    [DataField("electrocuteDamageScale")] public int ElectrocuteDamageScale = 5;

    public override void Effect(ReagentEffectArgs args)
    {
        EntitySystem.Get<ElectrocutionSystem>().TryDoElectrocution(args.SolutionEntity, null,
            Math.Max((args.Quantity * ElectrocuteDamageScale).Int(), 1), TimeSpan.FromSeconds(ElectrocuteTime));

        args.Source?.RemoveReagent(args.Reagent.ID, args.Quantity);
    }
}
