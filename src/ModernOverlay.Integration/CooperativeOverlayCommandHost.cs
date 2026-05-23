namespace ModernOverlay.Integration;

public sealed class CooperativeOverlayCommandHost : IDisposable
{
    private readonly OverlayResourceManager resources;
    private readonly System.Threading.Lock gate = new();
    private IReadOnlyList<RealizedDrawCommand> commands = [];
    private IReadOnlyList<OverlayDrawCommand> commandDefinitions = [];
    private Dictionary<string, CachedResource> cachedResources = [];
    private bool disposed;

    public CooperativeOverlayCommandHost(OverlayResourceManager resources)
    {
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public bool IsStarted { get; private set; }

    public int CommandCount
    {
        get
        {
            lock (gate)
            {
                return commands.Count;
            }
        }
    }

    public OverlayCommandResult Handle(OverlayCommandMessage message)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            switch (message.Kind)
            {
                case OverlayIntegrationCommandKind.Start:
                    Apply(message, message.Commands);
                    IsStarted = true;
                    return OverlayCommandResult.Ok(message.Sequence);

                case OverlayIntegrationCommandKind.Stop:
                    ApplyResourceChanges(message);
                    IsStarted = false;
                    return OverlayCommandResult.Ok(message.Sequence);

                case OverlayIntegrationCommandKind.Update:
                    if (message.CommandPatches.Count > 0)
                    {
                        ApplyPatch(message);
                    }
                    else
                    {
                        Apply(message, message.Commands);
                    }

                    return OverlayCommandResult.Ok(message.Sequence);

                case OverlayIntegrationCommandKind.Clear:
                    Apply(message, []);
                    return OverlayCommandResult.Ok(message.Sequence);

                default:
                    return OverlayCommandResult.Rejected(message.Sequence, $"Unsupported overlay integration command: {message.Kind}.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return OverlayCommandResult.Rejected(message.Sequence, ex.Message);
        }
    }

    public void Render(DrawContext frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(frame);

        if (!IsStarted)
        {
            return;
        }

        IReadOnlyList<RealizedDrawCommand> snapshot;
        lock (gate)
        {
            snapshot = commands;
        }

        foreach (RealizedDrawCommand command in snapshot)
        {
            command.Apply(frame);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ReplaceCommands([]);
        ClearCachedResources();
    }

    private void ApplyResourceChanges(OverlayCommandMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateNoCommandPatches(message);
        ValidateMessageShape(message, message.Commands);

        string[] releaseIds = NormalizeResourceIds(message.ReleaseResourceIds, nameof(message.ReleaseResourceIds));
        Dictionary<string, CachedResource> additions = RealizeResourceDefinitions(message.ResourceDefinitions);
        try
        {
            List<CachedResource> retired;
            lock (gate)
            {
                Dictionary<string, CachedResource> nextResources = new(cachedResources, StringComparer.Ordinal);
                retired = ApplyResourceCacheChanges(nextResources, releaseIds, additions);
                cachedResources = nextResources;
            }

            foreach (CachedResource resource in retired)
            {
                resource.Dispose();
            }
        }
        catch
        {
            foreach (CachedResource resource in additions.Values)
            {
                resource.Dispose();
            }

            throw;
        }
    }

    private void Apply(OverlayCommandMessage message, IReadOnlyList<OverlayDrawCommand> newCommands)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(newCommands);
        ValidateNoCommandPatches(message);
        ValidateMessageShape(message, newCommands);

        string[] releaseIds = NormalizeResourceIds(message.ReleaseResourceIds, nameof(message.ReleaseResourceIds));
        Dictionary<string, CachedResource> additions = RealizeResourceDefinitions(message.ResourceDefinitions);
        try
        {
            IReadOnlyList<RealizedDrawCommand> realized;
            IReadOnlyList<RealizedDrawCommand> previous;
            List<CachedResource> retired;

            lock (gate)
            {
                Dictionary<string, CachedResource> nextResources = new(cachedResources, StringComparer.Ordinal);
                retired = ApplyResourceCacheChanges(nextResources, releaseIds, additions);
                realized = RealizeAll(newCommands, nextResources);
                previous = commands;
                commands = realized;
                commandDefinitions = newCommands.ToArray();
                cachedResources = nextResources;
            }

            foreach (RealizedDrawCommand command in previous)
            {
                command.Dispose();
            }

            foreach (CachedResource resource in retired)
            {
                resource.Dispose();
            }
        }
        catch
        {
            foreach (CachedResource resource in additions.Values)
            {
                resource.Dispose();
            }

            throw;
        }
    }

    private void ReplaceCommands(IReadOnlyList<OverlayDrawCommand> newCommands)
    {
        ArgumentNullException.ThrowIfNull(newCommands);

        IReadOnlyList<RealizedDrawCommand> realized = RealizeAll(newCommands, cachedResources);
        IReadOnlyList<RealizedDrawCommand> previous;
        lock (gate)
        {
            previous = commands;
            commands = realized;
            commandDefinitions = newCommands.ToArray();
        }

        foreach (RealizedDrawCommand command in previous)
        {
            command.Dispose();
        }
    }

    private static List<CachedResource> ApplyResourceCacheChanges(
        Dictionary<string, CachedResource> nextResources,
        IReadOnlyList<string> releaseIds,
        IReadOnlyDictionary<string, CachedResource> additions)
    {
        var retired = new List<CachedResource>();
        foreach (string releaseId in releaseIds)
        {
            if (nextResources.Remove(releaseId, out CachedResource? removed))
            {
                retired.Add(removed);
            }
        }

        foreach ((string id, CachedResource resource) in additions)
        {
            if (nextResources.TryGetValue(id, out CachedResource? replaced))
            {
                retired.Add(replaced);
            }

            nextResources[id] = resource;
        }

        return retired;
    }

    private void ApplyPatch(OverlayCommandMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidatePatchMessageShape(message);

        string[] releaseIds = NormalizeResourceIds(message.ReleaseResourceIds, nameof(message.ReleaseResourceIds));
        Dictionary<string, CachedResource> additions = RealizeResourceDefinitions(message.ResourceDefinitions);
        try
        {
            IReadOnlyList<RealizedDrawCommand> realized;
            IReadOnlyList<RealizedDrawCommand> previous;
            IReadOnlyList<OverlayDrawCommand> nextDefinitions;
            List<CachedResource> retired;

            lock (gate)
            {
                Dictionary<string, CachedResource> nextResources = new(cachedResources, StringComparer.Ordinal);
                retired = ApplyResourceCacheChanges(nextResources, releaseIds, additions);
                nextDefinitions = ApplyCommandPatches(commandDefinitions, message.CommandPatches);
                ValidateMessageShape(message, nextDefinitions);
                realized = RealizeAll(nextDefinitions, nextResources);
                previous = commands;
                commands = realized;
                commandDefinitions = nextDefinitions;
                cachedResources = nextResources;
            }

            foreach (RealizedDrawCommand command in previous)
            {
                command.Dispose();
            }

            foreach (CachedResource resource in retired)
            {
                resource.Dispose();
            }
        }
        catch
        {
            foreach (CachedResource resource in additions.Values)
            {
                resource.Dispose();
            }

            throw;
        }
    }

    private static IReadOnlyList<OverlayDrawCommand> ApplyCommandPatches(
        IReadOnlyList<OverlayDrawCommand> currentDefinitions,
        IReadOnlyList<OverlayCommandPatch> patches)
    {
        var next = currentDefinitions.ToList();
        foreach (OverlayCommandPatch patch in patches)
        {
            switch (patch.Kind)
            {
                case OverlayCommandPatchKind.Append:
                    next.Add(RequirePatchCommand(patch));
                    break;

                case OverlayCommandPatchKind.InsertBefore:
                    next.Insert(FindCommandIndex(next, RequireAnchorCommandId(patch)), RequirePatchCommand(patch));
                    break;

                case OverlayCommandPatchKind.InsertAfter:
                    next.Insert(FindCommandIndex(next, RequireAnchorCommandId(patch)) + 1, RequirePatchCommand(patch));
                    break;

                case OverlayCommandPatchKind.Replace:
                    next[FindCommandIndex(next, RequireCommandId(patch))] = RequirePatchCommand(patch);
                    break;

                case OverlayCommandPatchKind.Remove:
                    next.RemoveAt(FindCommandIndex(next, RequireCommandId(patch)));
                    break;

                case OverlayCommandPatchKind.Clear:
                    next.Clear();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(patches), $"Unsupported command patch: {patch.Kind}.");
            }
        }

        ValidateUniqueCommandIds(next);
        return next.ToArray();
    }

    private static OverlayDrawCommand RequirePatchCommand(OverlayCommandPatch patch)
    {
        if (patch.Command is not { } command)
        {
            throw new ArgumentException($"Command patch '{patch.Kind}' requires a command.", nameof(patch));
        }

        ValidateCommandId(command.CommandId, nameof(command.CommandId));
        return command;
    }

    private static string RequireCommandId(OverlayCommandPatch patch)
    {
        ValidateCommandId(patch.CommandId, nameof(patch.CommandId));
        return patch.CommandId!;
    }

    private static string RequireAnchorCommandId(OverlayCommandPatch patch)
    {
        ValidateCommandId(patch.AnchorCommandId, nameof(patch.AnchorCommandId));
        return patch.AnchorCommandId!;
    }

    private static int FindCommandIndex(IReadOnlyList<OverlayDrawCommand> commands, string commandId)
    {
        for (int i = 0; i < commands.Count; i++)
        {
            if (string.Equals(commands[i].CommandId, commandId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Unknown command id '{commandId}'.");
    }

    private Dictionary<string, CachedResource> RealizeResourceDefinitions(IReadOnlyList<OverlayResourceDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var realized = new Dictionary<string, CachedResource>(StringComparer.Ordinal);
        try
        {
            foreach (OverlayResourceDefinition definition in definitions)
            {
                ValidateResourceId(definition.Id, nameof(definition.Id));
                ValidateResourceDefinition(definition);
                if (!ids.Add(definition.Id))
                {
                    throw new ArgumentException($"Resource definition '{definition.Id}' is duplicated.", nameof(definitions));
                }

                realized.Add(definition.Id, RealizeResourceDefinition(definition));
            }
        }
        catch
        {
            foreach (CachedResource resource in realized.Values)
            {
                resource.Dispose();
            }

            throw;
        }

        return realized;
    }

    private CachedResource RealizeResourceDefinition(OverlayResourceDefinition definition)
    {
        OverlayResourceHandle handle = definition.Kind switch
        {
            OverlayResourceDefinitionKind.SolidBrush => resources.CreateSolidBrush(definition.Color),
            OverlayResourceDefinitionKind.LinearGradientBrush => definition.LinearGradient is { } gradient
                ? resources.CreateLinearGradientBrush(gradient)
                : throw new ArgumentException("Linear-gradient resources require gradient options.", nameof(definition)),
            OverlayResourceDefinitionKind.Font => resources.CreateFont(new FontOptions(definition.FontFamily, definition.FontSize)),
            OverlayResourceDefinitionKind.ImagePath => !string.IsNullOrWhiteSpace(definition.ImagePath)
                ? resources.CreateImage(definition.ImagePath)
                : throw new ArgumentException("Image path resources require a path.", nameof(definition)),
            OverlayResourceDefinitionKind.ImageBytes => definition.ImageBytes is { Length: > 0 } imageBytes
                ? resources.CreateImage(imageBytes)
                : throw new ArgumentException("Image byte resources require encoded image bytes.", nameof(definition)),
            OverlayResourceDefinitionKind.Geometry => CreateGeometry(definition.GeometryCommands),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), $"Unsupported resource definition: {definition.Kind}."),
        };

        return new CachedResource(definition.Id, definition.Kind, handle);
    }

    private static string[] NormalizeResourceIds(IReadOnlyList<string> ids, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            ValidateResourceId(id, parameterName);
            if (!unique.Add(id))
            {
                throw new ArgumentException($"Resource id '{id}' is duplicated.", parameterName);
            }
        }

        return unique.ToArray();
    }

    private static void ValidateMessageShape(OverlayCommandMessage message, IReadOnlyList<OverlayDrawCommand> newCommands)
    {
        if (newCommands.Count > OverlayCommandLimits.MaxDrawCommandsPerMessage)
        {
            throw new ArgumentException($"Overlay command messages cannot contain more than {OverlayCommandLimits.MaxDrawCommandsPerMessage} draw commands.", nameof(message));
        }

        if (message.ResourceDefinitions.Count > OverlayCommandLimits.MaxResourceDefinitionsPerMessage)
        {
            throw new ArgumentException($"Overlay command messages cannot contain more than {OverlayCommandLimits.MaxResourceDefinitionsPerMessage} resource definitions.", nameof(message));
        }

        if (message.ReleaseResourceIds.Count > OverlayCommandLimits.MaxReleaseResourceIdsPerMessage)
        {
            throw new ArgumentException($"Overlay command messages cannot release more than {OverlayCommandLimits.MaxReleaseResourceIdsPerMessage} resources.", nameof(message));
        }

        ValidateUniqueCommandIds(newCommands);
    }

    private static void ValidatePatchMessageShape(OverlayCommandMessage message)
    {
        if (message.Commands.Count > 0)
        {
            throw new ArgumentException("Patch updates cannot also carry a replacement command list.", nameof(message));
        }

        if (message.CommandPatches.Count > OverlayCommandLimits.MaxCommandPatchesPerMessage)
        {
            throw new ArgumentException($"Overlay command messages cannot contain more than {OverlayCommandLimits.MaxCommandPatchesPerMessage} command patches.", nameof(message));
        }
    }

    private static void ValidateNoCommandPatches(OverlayCommandMessage message)
    {
        if (message.CommandPatches.Count > 0)
        {
            throw new ArgumentException("Command patches are only supported on update messages.", nameof(message));
        }
    }

    private static void ValidateUniqueCommandIds(IReadOnlyList<OverlayDrawCommand> commands)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (OverlayDrawCommand command in commands)
        {
            if (command.CommandId is null)
            {
                continue;
            }

            ValidateCommandId(command.CommandId, nameof(command.CommandId));
            if (!ids.Add(command.CommandId))
            {
                throw new ArgumentException($"Command id '{command.CommandId}' is duplicated.", nameof(commands));
            }
        }
    }

    private static void ValidateResourceDefinition(OverlayResourceDefinition definition)
    {
        switch (definition.Kind)
        {
            case OverlayResourceDefinitionKind.ImageBytes:
                ValidateImageBytes(definition.ImageBytes, nameof(definition.ImageBytes));
                break;

            case OverlayResourceDefinitionKind.Geometry:
                ValidateGeometryCommands(definition.GeometryCommands);
                break;
        }
    }

    private IReadOnlyList<RealizedDrawCommand> RealizeAll(
        IReadOnlyList<OverlayDrawCommand> newCommands,
        IReadOnlyDictionary<string, CachedResource> availableResources)
    {
        var realized = new List<RealizedDrawCommand>(newCommands.Count);
        try
        {
            foreach (OverlayDrawCommand command in newCommands)
            {
                realized.Add(Realize(command, availableResources));
            }
        }
        catch
        {
            foreach (RealizedDrawCommand command in realized)
            {
                command.Dispose();
            }

            throw;
        }

        return realized;
    }

    private RealizedDrawCommand Realize(
        OverlayDrawCommand command,
        IReadOnlyDictionary<string, CachedResource> availableResources)
    {
        ValidateCommand(command);
        var owned = new List<IDisposable>();
        BrushHandle? brush = NeedsBrush(command.Kind)
            ? ResolveBrush(command, availableResources, owned)
            : null;
        FontHandle? font = command.Kind == OverlayDrawCommandKind.DrawText
            ? ResolveFont(command, availableResources, owned)
            : null;
        ImageHandle? image = command.Kind == OverlayDrawCommandKind.DrawImage
            ? ResolveImage(command, availableResources, owned)
            : null;
        GeometryPath? geometry = command.Kind is OverlayDrawCommandKind.DrawGeometry or OverlayDrawCommandKind.FillGeometry
            ? ResolveGeometry(command, availableResources, owned)
            : null;
        return new RealizedDrawCommand(command, brush, font, image, geometry, owned);
    }

    private BrushHandle ResolveBrush(
        OverlayDrawCommand command,
        IReadOnlyDictionary<string, CachedResource> availableResources,
        List<IDisposable> owned)
    {
        if (!string.IsNullOrWhiteSpace(command.BrushResourceId))
        {
            return ResolveCached<BrushHandle>(availableResources, command.BrushResourceId, "brush");
        }

        BrushHandle brush = CreateBrush(command);
        owned.Add(brush);
        return brush;
    }

    private FontHandle ResolveFont(
        OverlayDrawCommand command,
        IReadOnlyDictionary<string, CachedResource> availableResources,
        List<IDisposable> owned)
    {
        if (!string.IsNullOrWhiteSpace(command.FontResourceId))
        {
            return ResolveCached<FontHandle>(availableResources, command.FontResourceId, "font");
        }

        FontHandle font = resources.CreateFont(new FontOptions(command.FontFamily, command.FontSize));
        owned.Add(font);
        return font;
    }

    private ImageHandle ResolveImage(
        OverlayDrawCommand command,
        IReadOnlyDictionary<string, CachedResource> availableResources,
        List<IDisposable> owned)
    {
        if (!string.IsNullOrWhiteSpace(command.ImageResourceId))
        {
            return ResolveCached<ImageHandle>(availableResources, command.ImageResourceId, "image");
        }

        ImageHandle image = CreateImage(command);
        owned.Add(image);
        return image;
    }

    private GeometryPath ResolveGeometry(
        OverlayDrawCommand command,
        IReadOnlyDictionary<string, CachedResource> availableResources,
        List<IDisposable> owned)
    {
        if (!string.IsNullOrWhiteSpace(command.GeometryResourceId))
        {
            return ResolveCached<GeometryPath>(availableResources, command.GeometryResourceId, "geometry");
        }

        GeometryPath geometry = CreateGeometry(command.GeometryCommands);
        owned.Add(geometry);
        return geometry;
    }

    private static T ResolveCached<T>(
        IReadOnlyDictionary<string, CachedResource> availableResources,
        string resourceId,
        string expectedKind)
        where T : OverlayResourceHandle
    {
        ValidateResourceId(resourceId, nameof(resourceId));
        CachedResource resource = availableResources.TryGetValue(resourceId, out CachedResource? resolvedResource)
            ? resolvedResource
            : throw new InvalidOperationException($"Unknown remote {expectedKind} resource '{resourceId}'.");
        return resource.Handle as T
            ?? throw new InvalidOperationException($"Remote resource '{resourceId}' is not a {expectedKind} resource.");
    }

    private BrushHandle CreateBrush(OverlayDrawCommand command)
        => command.LinearGradient is { } gradient
            ? resources.CreateLinearGradientBrush(gradient)
            : resources.CreateSolidBrush(command.Color);

    private ImageHandle CreateImage(OverlayDrawCommand command)
        => !string.IsNullOrWhiteSpace(command.ImagePath)
            ? resources.CreateImage(command.ImagePath)
            : command.ImageBytes is { Length: > 0 } imageBytes
                ? resources.CreateImage(imageBytes)
                : throw new ArgumentException("Image commands require an image path or encoded image bytes.", nameof(command));

    private static bool NeedsBrush(OverlayDrawCommandKind kind)
        => kind is not OverlayDrawCommandKind.Clear and not OverlayDrawCommandKind.DrawImage;

    private GeometryPath CreateGeometry(IReadOnlyList<OverlayGeometryCommand> geometryCommands)
        => resources.CreateGeometry(builder =>
        {
            foreach (OverlayGeometryCommand geometryCommand in geometryCommands)
            {
                ApplyGeometryCommand(builder, geometryCommand);
            }
        });

    private static void ApplyGeometryCommand(GeometryPathBuilder builder, OverlayGeometryCommand command)
    {
        switch (command.Kind)
        {
            case OverlayGeometryCommandKind.MoveTo:
                builder.MoveTo(command.Point);
                break;

            case OverlayGeometryCommandKind.LineTo:
                builder.LineTo(command.Point);
                break;

            case OverlayGeometryCommandKind.CubicBezierTo:
                builder.BezierTo(command.ControlPoint1, command.ControlPoint2, command.Point);
                break;

            case OverlayGeometryCommandKind.QuadraticBezierTo:
                builder.QuadraticBezierTo(command.ControlPoint1, command.Point);
                break;

            case OverlayGeometryCommandKind.ArcTo:
                builder.ArcTo(command.Point, command.Size, command.RotationAngleDegrees, command.SweepDirection, command.ArcSize);
                break;

            case OverlayGeometryCommandKind.Close:
                builder.Close();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(command), $"Unsupported geometry command: {command.Kind}.");
        }
    }

    private static void ValidateCommand(OverlayDrawCommand command)
    {
        switch (command.Kind)
        {
            case OverlayDrawCommandKind.Clear:
                break;

            case OverlayDrawCommandKind.DrawLine:
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.DrawArrow:
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.HeadLength);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.HeadAngleDegrees);
                if (command.HeadAngleDegrees >= 90f)
                {
                    throw new ArgumentOutOfRangeException(nameof(command), "Arrow head angle must be less than 90 degrees.");
                }

                break;

            case OverlayDrawCommandKind.DrawRectangle:
                ValidateRect(command.Rect);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.FillRectangle:
                ValidateRect(command.Rect);
                break;

            case OverlayDrawCommandKind.DrawRoundedRectangle:
                ValidateRect(command.Rect);
                ValidateRadius(command.RadiusX, nameof(command.RadiusX));
                ValidateRadius(command.RadiusY, nameof(command.RadiusY));
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.FillRoundedRectangle:
                ValidateRect(command.Rect);
                ValidateRadius(command.RadiusX, nameof(command.RadiusX));
                ValidateRadius(command.RadiusY, nameof(command.RadiusY));
                break;

            case OverlayDrawCommandKind.DrawCircle:
                ValidateRadius(command.Radius, nameof(command.Radius));
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.FillCircle:
                ValidateRadius(command.Radius, nameof(command.Radius));
                break;

            case OverlayDrawCommandKind.DrawEllipse:
                ValidateRect(command.Rect);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.FillEllipse:
                ValidateRect(command.Rect);
                break;

            case OverlayDrawCommandKind.DrawTriangle:
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.FillTriangle:
                break;

            case OverlayDrawCommandKind.DrawCornerBox:
                ValidateRect(command.Rect);
                ValidateRadius(command.CornerLength, nameof(command.CornerLength));
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.DrawCrosshair:
                ValidateRadius(command.Size, nameof(command.Size));
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.DrawGeometry:
                ValidateGeometryReferenceOrCommands(command);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.StrokeWidth);
                break;

            case OverlayDrawCommandKind.FillGeometry:
                ValidateGeometryReferenceOrCommands(command);
                break;

            case OverlayDrawCommandKind.DrawImage:
                if (string.IsNullOrWhiteSpace(command.ImageResourceId)
                    && string.IsNullOrWhiteSpace(command.ImagePath)
                    && command.ImageBytes is not { Length: > 0 })
                {
                    throw new ArgumentException("Image commands require an image path or encoded image bytes.", nameof(command));
                }

                if (!string.IsNullOrWhiteSpace(command.ImageResourceId))
                {
                    ValidateResourceId(command.ImageResourceId, nameof(command.ImageResourceId));
                }
                else if (command.ImageBytes is not null)
                {
                    ValidateImageBytes(command.ImageBytes, nameof(command.ImageBytes));
                }

                ArgumentOutOfRangeException.ThrowIfNegative(command.FrameIndex);
                ValidateRect(command.Destination);
                if (command.Source is { } source)
                {
                    ValidateRect(source);
                }

                if (command.Opacity is < 0f or > 1f)
                {
                    throw new ArgumentOutOfRangeException(nameof(command), "Image opacity must be between 0 and 1.");
                }

                break;

            case OverlayDrawCommandKind.DrawText:
                ArgumentException.ThrowIfNullOrWhiteSpace(command.Text);
                if (string.IsNullOrWhiteSpace(command.FontResourceId))
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(command.FontFamily);
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.FontSize);
                }
                else
                {
                    ValidateResourceId(command.FontResourceId, nameof(command.FontResourceId));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(command), $"Unsupported draw command: {command.Kind}.");
        }

        if (NeedsBrush(command.Kind) && !string.IsNullOrWhiteSpace(command.BrushResourceId))
        {
            ValidateResourceId(command.BrushResourceId, nameof(command.BrushResourceId));
        }
    }

    private static void ValidateGeometryReferenceOrCommands(OverlayDrawCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.GeometryResourceId))
        {
            ValidateGeometryCommands(command.GeometryCommands);
            return;
        }

        ValidateResourceId(command.GeometryResourceId, nameof(command.GeometryResourceId));
    }

    private static void ValidateGeometryCommands(IReadOnlyList<OverlayGeometryCommand> geometryCommands)
    {
        ArgumentNullException.ThrowIfNull(geometryCommands);
        if (geometryCommands.Count == 0)
        {
            throw new ArgumentException("Geometry commands require at least one path operation.", nameof(geometryCommands));
        }

        if (geometryCommands.Count > OverlayCommandLimits.MaxGeometryCommandsPerPath)
        {
            throw new ArgumentException($"Geometry commands cannot contain more than {OverlayCommandLimits.MaxGeometryCommandsPerPath} path operations.", nameof(geometryCommands));
        }

        foreach (OverlayGeometryCommand command in geometryCommands)
        {
            if (command.Kind == OverlayGeometryCommandKind.ArcTo)
            {
                ValidateRect(new RectF(0, 0, command.Size.Width, command.Size.Height));
            }
        }
    }

    private static void ValidateImageBytes(byte[]? imageBytes, string parameterName)
    {
        if (imageBytes is not { Length: > 0 })
        {
            throw new ArgumentException("Image byte arrays cannot be empty.", parameterName);
        }

        if (imageBytes.Length > OverlayCommandLimits.MaxInlineImageBytes)
        {
            throw new ArgumentException($"Inline image payloads cannot exceed {OverlayCommandLimits.MaxInlineImageBytes} bytes.", parameterName);
        }
    }

    private static void ValidateRadius(float radius, string parameterName)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Radius and size values must be finite and positive.");
        }
    }

    private static void ValidateRect(RectF rect)
    {
        if (rect.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), "Rectangle commands require positive width and height.");
        }
    }

    private static void ValidateResourceId(string? resourceId, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Remote resource ids cannot be blank.", parameterName);
        }

        if (resourceId.Length > OverlayCommandLimits.MaxResourceIdLength)
        {
            throw new ArgumentException($"Remote resource ids cannot exceed {OverlayCommandLimits.MaxResourceIdLength} characters.", parameterName);
        }
    }

    private static void ValidateCommandId(string? commandId, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new ArgumentException("Command ids cannot be blank.", parameterName);
        }

        if (commandId.Length > OverlayCommandLimits.MaxCommandIdLength)
        {
            throw new ArgumentException($"Command ids cannot exceed {OverlayCommandLimits.MaxCommandIdLength} characters.", parameterName);
        }
    }

    private void ClearCachedResources()
    {
        Dictionary<string, CachedResource> previous;
        lock (gate)
        {
            previous = cachedResources;
            cachedResources = [];
        }

        foreach (CachedResource resource in previous.Values)
        {
            resource.Dispose();
        }
    }

    private sealed class CachedResource : IDisposable
    {
        public CachedResource(string id, OverlayResourceDefinitionKind kind, OverlayResourceHandle handle)
        {
            Id = id;
            Kind = kind;
            Handle = handle;
        }

        public string Id { get; }

        public OverlayResourceDefinitionKind Kind { get; }

        public OverlayResourceHandle Handle { get; }

        public void Dispose() => Handle.Dispose();
    }

    private sealed class RealizedDrawCommand : IDisposable
    {
        private readonly OverlayDrawCommand command;
        private readonly BrushHandle? brush;
        private readonly FontHandle? font;
        private readonly ImageHandle? image;
        private readonly GeometryPath? geometry;
        private readonly IReadOnlyList<IDisposable> ownedResources;

        public RealizedDrawCommand(
            OverlayDrawCommand command,
            BrushHandle? brush,
            FontHandle? font,
            ImageHandle? image,
            GeometryPath? geometry,
            IReadOnlyList<IDisposable> ownedResources)
        {
            this.command = command;
            this.brush = brush;
            this.font = font;
            this.image = image;
            this.geometry = geometry;
            this.ownedResources = ownedResources;
        }

        public void Apply(DrawContext frame)
        {
            switch (command.Kind)
            {
                case OverlayDrawCommandKind.Clear:
                    frame.Clear(command.Color);
                    break;

                case OverlayDrawCommandKind.DrawLine:
                    frame.Draw.Line(command.Start, command.End, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.DrawArrow:
                    frame.Draw.Arrow(command.Start, command.End, brush!, command.StrokeWidth, command.HeadLength, command.HeadAngleDegrees);
                    break;

                case OverlayDrawCommandKind.DrawRectangle:
                    frame.Draw.Rectangle(command.Rect, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.FillRectangle:
                    frame.Fill.Rectangle(command.Rect, brush!);
                    break;

                case OverlayDrawCommandKind.DrawRoundedRectangle:
                    frame.Draw.RoundedRectangle(command.Rect, command.RadiusX, command.RadiusY, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.FillRoundedRectangle:
                    frame.Fill.RoundedRectangle(command.Rect, command.RadiusX, command.RadiusY, brush!);
                    break;

                case OverlayDrawCommandKind.DrawCircle:
                    frame.Draw.Circle(command.Center, command.Radius, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.FillCircle:
                    frame.Fill.Circle(command.Center, command.Radius, brush!);
                    break;

                case OverlayDrawCommandKind.DrawEllipse:
                    frame.Draw.Ellipse(command.Rect, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.FillEllipse:
                    frame.Fill.Ellipse(command.Rect, brush!);
                    break;

                case OverlayDrawCommandKind.DrawTriangle:
                    frame.Draw.Triangle(command.A, command.B, command.C, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.FillTriangle:
                    frame.Fill.Triangle(command.A, command.B, command.C, brush!);
                    break;

                case OverlayDrawCommandKind.DrawCornerBox:
                    frame.Draw.CornerBox(command.Rect, brush!, command.CornerLength, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.DrawCrosshair:
                    frame.Draw.Crosshair(command.Center, command.Size, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.DrawGeometry:
                    frame.Draw.Geometry(geometry!, brush!, command.StrokeWidth);
                    break;

                case OverlayDrawCommandKind.FillGeometry:
                    frame.Fill.Geometry(geometry!, brush!);
                    break;

                case OverlayDrawCommandKind.DrawImage:
                    frame.Draw.Image(
                        image!,
                        command.FrameIndex,
                        command.Destination,
                        command.Source,
                        command.Opacity,
                        command.InterpolationMode);
                    break;

                case OverlayDrawCommandKind.DrawText:
                    frame.Draw.Text(command.Text!, font!, brush!, command.Origin);
                    break;
            }
        }

        public void Dispose()
        {
            foreach (IDisposable resource in ownedResources)
            {
                resource.Dispose();
            }
        }
    }
}
