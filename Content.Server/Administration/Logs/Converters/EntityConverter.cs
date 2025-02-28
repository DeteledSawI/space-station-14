﻿using System.Text.Json;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Administration.Logs.Converters;

[AdminLogConverter]
public class EntityConverter : AdminLogConverter<Entity>
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public override void Write(Utf8JsonWriter writer, Entity value, JsonSerializerOptions options)
    {
        EntityUidConverter.Write(writer, value.Uid, options, _entities);
    }
}
