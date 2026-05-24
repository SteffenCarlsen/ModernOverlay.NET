using ModernOverlay.Integration;
using ModernOverlay.Rendering;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class IntegrationCommandTests
{
    private static readonly long[] ConcurrentClientSequences = [21L, 22L];

    [TestMethod]
    public void CommandProtocolRoundTripsMessage()
    {
        OverlayCommandMessage message = OverlayCommandMessage.Update(
            [
                OverlayDrawCommand.Clear(ColorRgba.Transparent),
                OverlayDrawCommand.TextRun("owned host", new PointF(12, 24), ColorRgba.White)
                    .WithCommandId("title")
                    .WithBrushResource("hud-brush")
                    .WithFontResource("hud-font"),
                OverlayDrawCommand.RoundedRectangle(new RectF(4, 5, 30, 20), 3, 4, ColorRgba.White, 2),
                OverlayDrawCommand.Circle(new PointF(40, 40), 12, ColorRgba.White),
                OverlayDrawCommand.CornerBox(new RectF(10, 20, 80, 40), 12, ColorRgba.White),
                OverlayDrawCommand.ImageFromBytes([1, 2, 3, 4], new RectF(100, 20, 32, 32), frameIndex: 1, opacity: 0.5f, interpolationMode: ImageInterpolationMode.NearestNeighbor),
                OverlayDrawCommand.FilledRectangle(new RectF(12, 64, 40, 20), ColorRgba.White)
                    .WithLinearGradient(CreateGradient()),
                OverlayDrawCommand.Geometry(
                    [
                        OverlayGeometryCommand.MoveTo(new PointF(1, 2)),
                        OverlayGeometryCommand.LineTo(new PointF(3, 4)),
                        OverlayGeometryCommand.Close(),
                    ],
                    ColorRgba.White,
                    strokeWidth: 2),
            ])
            with
        {
            Sequence = 42,
            ResourceDefinitions =
            [
                OverlayResourceDefinition.SolidBrush("hud-brush", ColorRgba.White),
                OverlayResourceDefinition.Font("hud-font", "Segoe UI", 18),
            ],
            ReleaseResourceIds = ["old-brush"],
        };

        string json = OverlayCommandProtocol.SerializeMessage(message);
        OverlayCommandMessage roundTrip = OverlayCommandProtocol.DeserializeMessage(json);

        Assert.AreEqual(OverlayIntegrationCommandKind.Update, roundTrip.Kind);
        Assert.AreEqual(42, roundTrip.Sequence);
        Assert.AreEqual(2, roundTrip.ResourceDefinitions.Count);
        Assert.AreEqual("hud-brush", roundTrip.ResourceDefinitions[0].Id);
        Assert.AreEqual("old-brush", roundTrip.ReleaseResourceIds[0]);
        Assert.AreEqual(8, roundTrip.Commands.Count);
        Assert.AreEqual(OverlayDrawCommandKind.DrawText, roundTrip.Commands[1].Kind);
        Assert.AreEqual("title", roundTrip.Commands[1].CommandId);
        Assert.AreEqual("owned host", roundTrip.Commands[1].Text);
        Assert.AreEqual("hud-brush", roundTrip.Commands[1].BrushResourceId);
        Assert.AreEqual("hud-font", roundTrip.Commands[1].FontResourceId);
        Assert.AreEqual(OverlayDrawCommandKind.DrawCornerBox, roundTrip.Commands[4].Kind);
        Assert.AreEqual(12, roundTrip.Commands[4].CornerLength);
        Assert.AreEqual(OverlayDrawCommandKind.DrawImage, roundTrip.Commands[5].Kind);
        Assert.AreEqual(1, roundTrip.Commands[5].FrameIndex);
        Assert.AreEqual(0.5f, roundTrip.Commands[5].Opacity);
        Assert.AreEqual(ImageInterpolationMode.NearestNeighbor, roundTrip.Commands[5].InterpolationMode);
        Assert.AreEqual(OverlayDrawCommandKind.FillRectangle, roundTrip.Commands[6].Kind);
        Assert.IsNotNull(roundTrip.Commands[6].LinearGradient);
        LinearGradientBrushOptions gradient = roundTrip.Commands[6].LinearGradient!;
        Assert.AreEqual(2, gradient.Stops.Count);
        Assert.AreEqual(OverlayDrawCommandKind.DrawGeometry, roundTrip.Commands[7].Kind);
        Assert.AreEqual(3, roundTrip.Commands[7].GeometryCommands.Count);
        Assert.AreEqual(OverlayGeometryCommandKind.LineTo, roundTrip.Commands[7].GeometryCommands[1].Kind);
    }

    [TestMethod]
    public void CooperativeHostUpdatesClearsAndDisposesRealizedResources()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult start = host.Handle(OverlayCommandMessage.Start(
            [
                OverlayDrawCommand.Rectangle(new RectF(10, 10, 50, 30), ColorRgba.White),
                OverlayDrawCommand.TextRun("ready", new PointF(12, 16), ColorRgba.White),
            ]));

        Assert.IsTrue(start.Accepted);
        Assert.IsTrue(host.IsStarted);
        Assert.AreEqual(2, host.CommandCount);
        Assert.AreEqual(3, resources.GetLiveResources().Count);

        OverlayCommandResult clear = host.Handle(OverlayCommandMessage.Clear());

        Assert.IsTrue(clear.Accepted);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostReusesRemoteResourcesUntilReleased()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult start = host.Handle(OverlayCommandMessage.Start(
            [
                OverlayDrawCommand.Rectangle(new RectF(10, 10, 50, 30), ColorRgba.White)
                    .WithBrushResource("hud-brush"),
                OverlayDrawCommand.TextRun("ready", new PointF(12, 16), ColorRgba.White)
                    .WithBrushResource("hud-brush")
                    .WithFontResource("hud-font"),
            ])
            with
        {
            ResourceDefinitions =
            [
                OverlayResourceDefinition.SolidBrush("hud-brush", ColorRgba.White),
                OverlayResourceDefinition.Font("hud-font", "Segoe UI", 16),
            ],
        });

        Assert.IsTrue(start.Accepted);
        Assert.AreEqual(2, host.CommandCount);
        Assert.AreEqual(2, resources.GetLiveResources().Count);

        OverlayCommandResult update = host.Handle(OverlayCommandMessage.Update(
            [
                OverlayDrawCommand.TextRun("still ready", new PointF(12, 16), ColorRgba.White)
                    .WithBrushResource("hud-brush")
                    .WithFontResource("hud-font"),
            ]));

        Assert.IsTrue(update.Accepted);
        Assert.AreEqual(1, host.CommandCount);
        Assert.AreEqual(2, resources.GetLiveResources().Count);

        OverlayCommandResult clear = host.Handle(OverlayCommandMessage.Clear());

        Assert.IsTrue(clear.Accepted);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(2, resources.GetLiveResources().Count);

        OverlayCommandResult release = host.Handle(OverlayCommandMessage.Clear() with
        {
            ReleaseResourceIds = ["hud-brush", "hud-font"],
        });

        Assert.IsTrue(release.Accepted);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostRejectsMissingRemoteResourceWithoutMutatingState()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult start = host.Handle(OverlayCommandMessage.Start(
            [
                OverlayDrawCommand.Rectangle(new RectF(10, 10, 50, 30), ColorRgba.White)
                    .WithBrushResource("hud-brush"),
            ])
            with
        {
            ResourceDefinitions = [OverlayResourceDefinition.SolidBrush("hud-brush", ColorRgba.White)],
        });

        OverlayCommandResult rejected = host.Handle(OverlayCommandMessage.Update(
            [
                OverlayDrawCommand.TextRun("missing font", new PointF(12, 16), ColorRgba.White)
                    .WithBrushResource("hud-brush")
                    .WithFontResource("missing-font"),
            ]));

        Assert.IsTrue(start.Accepted);
        Assert.IsFalse(rejected.Accepted);
        Assert.IsNotNull(rejected.Error);
        Assert.Contains("missing-font", rejected.Error);
        Assert.AreEqual(1, host.CommandCount);
        Assert.AreEqual(1, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostDisposesNewRemoteResourcesWhenUpdateIsRejected()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult result = host.Handle(OverlayCommandMessage.Update(
            [
                OverlayDrawCommand.TextRun("missing font", new PointF(12, 16), ColorRgba.White)
                    .WithBrushResource("new-brush")
                    .WithFontResource("missing-font"),
            ])
            with
        {
            ResourceDefinitions = [OverlayResourceDefinition.SolidBrush("new-brush", ColorRgba.White)],
        });

        Assert.IsFalse(result.Accepted);
        Assert.IsNotNull(result.Error);
        Assert.Contains("missing-font", result.Error);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostRejectsOversizedInlineImagePayload()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult result = host.Handle(OverlayCommandMessage.Update(
            [
                new OverlayDrawCommand
                {
                    Kind = OverlayDrawCommandKind.DrawImage,
                    Destination = new RectF(1, 2, 10, 10),
                    ImageBytes = new byte[OverlayCommandLimits.MaxInlineImageBytes + 1],
                },
            ]));

        Assert.IsFalse(result.Accepted);
        Assert.IsNotNull(result.Error);
        Assert.Contains("Inline image payloads", result.Error);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostRejectsOversizedRemoteGeometryPayload()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);
        OverlayGeometryCommand[] commands = Enumerable
            .Range(0, OverlayCommandLimits.MaxGeometryCommandsPerPath + 1)
            .Select(index => index == 0
                ? OverlayGeometryCommand.MoveTo(new PointF(index, index))
                : OverlayGeometryCommand.LineTo(new PointF(index, index)))
            .ToArray();

        OverlayCommandResult result = host.Handle(OverlayCommandMessage.Update([]) with
        {
            ResourceDefinitions =
            [
                new OverlayResourceDefinition
                {
                    Id = "large-geometry",
                    Kind = OverlayResourceDefinitionKind.Geometry,
                    GeometryCommands = commands,
                },
            ],
        });

        Assert.IsFalse(result.Accepted);
        Assert.IsNotNull(result.Error);
        Assert.Contains("Geometry commands", result.Error);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostAppliesCommandPatchesTransactionally()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink);

        OverlayCommandResult start = host.Handle(OverlayCommandMessage.Start(
            [
                OverlayDrawCommand.TextRun("old", new PointF(12, 16), ColorRgba.White).WithCommandId("title"),
                OverlayDrawCommand.Rectangle(new RectF(10, 10, 50, 30), ColorRgba.White).WithCommandId("box"),
            ]));

        OverlayCommandResult patch = host.Handle(OverlayCommandMessage.Update([]) with
        {
            CommandPatches =
            [
                OverlayCommandPatch.Replace("title", OverlayDrawCommand.TextRun("new", new PointF(12, 16), ColorRgba.White).WithCommandId("title")),
                OverlayCommandPatch.Remove("box"),
                OverlayCommandPatch.Append(OverlayDrawCommand.Line(new PointF(0, 0), new PointF(10, 10), ColorRgba.White).WithCommandId("line")),
            ],
        });

        host.Render(context);

        Assert.IsTrue(start.Accepted);
        Assert.IsTrue(patch.Accepted);
        Assert.AreEqual(2, host.CommandCount);
        Assert.Contains(nameof(IDrawCommandSink.DrawText), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.DrawLine), sink.Commands);
        Assert.DoesNotContain(nameof(IDrawCommandSink.DrawRectangle), sink.Commands);
    }

    [TestMethod]
    public void CooperativeHostRejectsInvalidCommandPatchWithoutMutatingState()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink);

        OverlayCommandResult start = host.Handle(OverlayCommandMessage.Start(
            [OverlayDrawCommand.TextRun("stable", new PointF(12, 16), ColorRgba.White).WithCommandId("title")]));

        OverlayCommandResult rejected = host.Handle(OverlayCommandMessage.Update([]) with
        {
            CommandPatches =
            [
                OverlayCommandPatch.InsertAfter("missing", OverlayDrawCommand.Line(new PointF(0, 0), new PointF(10, 10), ColorRgba.White).WithCommandId("line")),
            ],
        });

        host.Render(context);

        Assert.IsTrue(start.Accepted);
        Assert.IsFalse(rejected.Accepted);
        Assert.IsNotNull(rejected.Error);
        Assert.Contains("missing", rejected.Error);
        Assert.AreEqual(1, host.CommandCount);
        Assert.Contains(nameof(IDrawCommandSink.DrawText), sink.Commands);
        Assert.DoesNotContain(nameof(IDrawCommandSink.DrawLine), sink.Commands);
    }

    [TestMethod]
    public void CooperativeHostRendersExpandedShapeCommands()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);
        var sink = new RecordingDrawCommandSink();
        var context = new DrawContext(sink);

        OverlayCommandResult start = host.Handle(OverlayCommandMessage.Start(
            [
                OverlayDrawCommand.Clear(ColorRgba.Transparent),
                OverlayDrawCommand.RoundedRectangle(new RectF(10, 10, 50, 30), 4, 5, ColorRgba.White, 2),
                OverlayDrawCommand.FilledRoundedRectangle(new RectF(20, 20, 40, 20), 3, 3, ColorRgba.White),
                OverlayDrawCommand.Circle(new PointF(80, 50), 12, ColorRgba.White, 2),
                OverlayDrawCommand.FilledCircle(new PointF(110, 50), 10, ColorRgba.White),
                OverlayDrawCommand.Ellipse(new RectF(130, 30, 40, 24), ColorRgba.White, 2),
                OverlayDrawCommand.FilledEllipse(new RectF(180, 30, 40, 24), ColorRgba.White),
                OverlayDrawCommand.Triangle(new PointF(20, 90), new PointF(50, 130), new PointF(80, 90), ColorRgba.White, 2),
                OverlayDrawCommand.FilledTriangle(new PointF(100, 90), new PointF(130, 130), new PointF(160, 90), ColorRgba.White),
                OverlayDrawCommand.Arrow(new PointF(190, 90), new PointF(240, 90), ColorRgba.White, 2),
                OverlayDrawCommand.CornerBox(new RectF(260, 80, 60, 40), 12, ColorRgba.White, 2),
                OverlayDrawCommand.Crosshair(new PointF(360, 100), 14, ColorRgba.White, 2),
                OverlayDrawCommand.ImageFromBytes([1, 2, 3, 4], new RectF(390, 80, 48, 48), frameIndex: 0, opacity: 0.75f),
                OverlayDrawCommand.Geometry(CreateTrianglePath(), ColorRgba.White, 2),
                OverlayDrawCommand.FilledGeometry(CreateTrianglePath(), ColorRgba.White),
                OverlayDrawCommand.FilledRectangle(new RectF(450, 80, 48, 48), ColorRgba.White)
                    .WithLinearGradient(CreateGradient()),
            ]));

        host.Render(context);

        Assert.IsTrue(start.Accepted);
        Assert.Contains(nameof(IDrawCommandSink.Clear), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.DrawRoundedRectangle), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.FillRoundedRectangle), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.DrawCircle), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.FillCircle), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.DrawEllipse), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.FillEllipse), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.DrawTriangle), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.FillTriangle), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.DrawImage), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.DrawGeometry), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.FillGeometry), sink.Commands);
        Assert.Contains(nameof(IDrawCommandSink.FillRectangle), sink.Commands);
        Assert.AreEqual(13, sink.LineCount);
    }

    [TestMethod]
    public void CooperativeHostDisposesPartiallyRealizedResourcesWhenCommandBatchIsRejected()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult result = host.Handle(OverlayCommandMessage.Update(
            [
                OverlayDrawCommand.Line(new PointF(0, 0), new PointF(1, 1), ColorRgba.White),
                OverlayDrawCommand.FilledRectangle(new RectF(1, 1, 10, 10), ColorRgba.White)
                    .WithLinearGradient(new LinearGradientBrushOptions(
                        new PointF(0, 0),
                        new PointF(1, 1),
                        [new GradientStop(0, ColorRgba.White)])),
            ]));

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostRejectsInvalidShapeCommands()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult result = host.Handle(OverlayCommandMessage.Update(
            [OverlayDrawCommand.Circle(new PointF(1, 2), radius: 0, ColorRgba.White)]));

        Assert.IsFalse(result.Accepted);
        Assert.IsNotNull(result.Error);
        Assert.Contains("Radius", result.Error);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostRejectsInvalidImageCommands()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult result = host.Handle(new OverlayCommandMessage(
            OverlayIntegrationCommandKind.Update,
            [
                new OverlayDrawCommand
                {
                    Kind = OverlayDrawCommandKind.DrawImage,
                    Destination = new RectF(1, 2, 10, 10),
                },
            ]));

        Assert.IsFalse(result.Accepted);
        Assert.IsNotNull(result.Error);
        Assert.Contains("Image commands require", result.Error);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public void CooperativeHostRejectsInvalidGeometryCommands()
    {
        var resources = new OverlayResourceManager();
        using var host = new CooperativeOverlayCommandHost(resources);

        OverlayCommandResult result = host.Handle(OverlayCommandMessage.Update(
            [OverlayDrawCommand.Geometry([], ColorRgba.White)]));

        Assert.IsFalse(result.Accepted);
        Assert.IsNotNull(result.Error);
        Assert.Contains("Geometry commands require", result.Error);
        Assert.AreEqual(0, host.CommandCount);
        Assert.AreEqual(0, resources.GetLiveResources().Count);
    }

    [TestMethod]
    public async Task NamedPipeTransportSendsCommandAndReturnsResult()
    {
        string pipeName = $"ModernOverlay.Tests.{Guid.NewGuid():N}";
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var server = new NamedPipeOverlayCommandServer(
            pipeName,
            (message, _) => ValueTask.FromResult(OverlayCommandResult.Ok(message.Sequence)));

        Task serverTask = server.RunAsync(cts.Token);
        var client = new NamedPipeOverlayCommandClient(pipeName);

        OverlayCommandResult result = await client.SendAsync(
            OverlayCommandMessage.Update([OverlayDrawCommand.Clear(ColorRgba.Transparent)]) with { Sequence = 7 },
            cts.Token);

        await cts.CancelAsync();
        await serverTask;

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(7, result.Sequence);
    }

    [TestMethod]
    public async Task NamedPipeTransportAcceptsMatchingCommandToken()
    {
        string pipeName = $"ModernOverlay.Tests.{Guid.NewGuid():N}";
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        OverlayCommandMessage? handledMessage = null;
        var server = new NamedPipeOverlayCommandServer(
            pipeName,
            (message, _) =>
            {
                handledMessage = message;
                return ValueTask.FromResult(OverlayCommandResult.Ok(message.Sequence));
            },
            NamedPipeOverlayCommandSecurity.RequireCommandToken("local-token"));

        Task serverTask = server.RunAsync(cts.Token);
        var client = new NamedPipeOverlayCommandClient(pipeName, commandToken: "local-token");

        OverlayCommandResult result = await client.SendAsync(
            OverlayCommandMessage.Update([OverlayDrawCommand.Clear(ColorRgba.Transparent)]) with { Sequence = 11 },
            cts.Token);

        await cts.CancelAsync();
        await serverTask;

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(11, result.Sequence);
        Assert.IsNotNull(handledMessage);
        Assert.IsNull(handledMessage.CommandToken);
    }

    [TestMethod]
    public async Task NamedPipeTransportRejectsMissingOrInvalidCommandTokenBeforeHandler()
    {
        string pipeName = $"ModernOverlay.Tests.{Guid.NewGuid():N}";
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        int handledCount = 0;
        var server = new NamedPipeOverlayCommandServer(
            pipeName,
            (message, _) =>
            {
                handledCount++;
                return ValueTask.FromResult(OverlayCommandResult.Ok(message.Sequence));
            },
            NamedPipeOverlayCommandSecurity.RequireCommandToken("local-token"));

        Task serverTask = server.RunAsync(cts.Token);
        var missingTokenClient = new NamedPipeOverlayCommandClient(pipeName);
        var invalidTokenClient = new NamedPipeOverlayCommandClient(pipeName, commandToken: "wrong-token");

        OverlayCommandResult missingTokenResult = await missingTokenClient.SendAsync(
            OverlayCommandMessage.Update([OverlayDrawCommand.Clear(ColorRgba.Transparent)]) with { Sequence = 12 },
            cts.Token);
        OverlayCommandResult invalidTokenResult = await invalidTokenClient.SendAsync(
            OverlayCommandMessage.Update([OverlayDrawCommand.Clear(ColorRgba.Transparent)]) with { Sequence = 13 },
            cts.Token);

        await cts.CancelAsync();
        await serverTask;

        Assert.IsFalse(missingTokenResult.Accepted);
        Assert.AreEqual(12, missingTokenResult.Sequence);
        Assert.IsNotNull(missingTokenResult.Error);
        Assert.Contains("token", missingTokenResult.Error);
        Assert.IsFalse(invalidTokenResult.Accepted);
        Assert.AreEqual(13, invalidTokenResult.Sequence);
        Assert.IsNotNull(invalidTokenResult.Error);
        Assert.Contains("token", invalidTokenResult.Error);
        Assert.AreEqual(0, handledCount);
    }

    [TestMethod]
    public async Task NamedPipeTransportSupportsCurrentUserAclAndCommandToken()
    {
        string pipeName = $"ModernOverlay.Tests.{Guid.NewGuid():N}";
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var server = new NamedPipeOverlayCommandServer(
            pipeName,
            (message, _) => ValueTask.FromResult(OverlayCommandResult.Ok(message.Sequence)),
            NamedPipeOverlayCommandSecurity.CurrentUserOnly("local-token"));

        Task serverTask = server.RunAsync(cts.Token);
        var client = new NamedPipeOverlayCommandClient(pipeName, commandToken: "local-token");

        OverlayCommandResult result = await client.SendAsync(
            OverlayCommandMessage.Update([OverlayDrawCommand.Clear(ColorRgba.Transparent)]) with { Sequence = 15 },
            cts.Token);

        await cts.CancelAsync();
        await serverTask;

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(15, result.Sequence);
    }

    [TestMethod]
    public void NamedPipeSecurityAcceptsCustomPipeSecurity()
    {
        SecurityIdentifier? currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is null)
        {
            Assert.Inconclusive("The current Windows identity does not expose a user security identifier.");
        }

        PipeSecurity pipeSecurity = new();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            currentUser,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        var security = NamedPipeOverlayCommandSecurity.WithPipeSecurity(
            pipeSecurity,
            "local-token");

        Assert.AreSame(pipeSecurity, security.PipeSecurity);
        Assert.IsTrue(security.RequiresCommandToken);
        Assert.ThrowsExactly<ArgumentException>(() => NamedPipeOverlayCommandSecurity.WithPipeSecurity(pipeSecurity, string.Empty));
    }

    [TestMethod]
    public async Task NamedPipeTransportHandlesMultipleClientsConcurrently()
    {
        string pipeName = $"ModernOverlay.Tests.{Guid.NewGuid():N}";
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var bothHandlersActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int activeHandlers = 0;
        int handledCount = 0;
        var server = new NamedPipeOverlayCommandServer(
            pipeName,
            async (message, cancellationToken) =>
            {
                if (Interlocked.Increment(ref activeHandlers) == 2)
                {
                    bothHandlersActive.SetResult();
                }

                await bothHandlersActive.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Decrement(ref activeHandlers);
                Interlocked.Increment(ref handledCount);
                return OverlayCommandResult.Ok(message.Sequence);
            },
            maxConcurrentConnections: 2);

        Task serverTask = server.RunAsync(cts.Token);
        var firstClient = new NamedPipeOverlayCommandClient(pipeName);
        var secondClient = new NamedPipeOverlayCommandClient(pipeName);

        ValueTask<OverlayCommandResult> firstResultTask = firstClient.SendAsync(
            OverlayCommandMessage.Update([OverlayDrawCommand.Clear(ColorRgba.Transparent)]) with { Sequence = 21 },
            cts.Token);
        ValueTask<OverlayCommandResult> secondResultTask = secondClient.SendAsync(
            OverlayCommandMessage.Update([OverlayDrawCommand.Clear(ColorRgba.Transparent)]) with { Sequence = 22 },
            cts.Token);

        OverlayCommandResult[] results = await Task.WhenAll(firstResultTask.AsTask(), secondResultTask.AsTask()).WaitAsync(cts.Token);

        await cts.CancelAsync();
        await serverTask;

        Assert.AreEqual(2, handledCount);
        Assert.IsTrue(results[0].Accepted);
        Assert.IsTrue(results[1].Accepted);
        CollectionAssert.AreEquivalent(ConcurrentClientSequences, results.Select(result => result.Sequence).ToArray());
    }

    private static OverlayGeometryCommand[] CreateTrianglePath()
        =>
        [
            OverlayGeometryCommand.MoveTo(new PointF(10, 10)),
            OverlayGeometryCommand.LineTo(new PointF(30, 40)),
            OverlayGeometryCommand.LineTo(new PointF(50, 10)),
            OverlayGeometryCommand.Close(),
        ];

    private static LinearGradientBrushOptions CreateGradient()
        => new(
            new PointF(0, 0),
            new PointF(32, 32),
            [
                new GradientStop(0, ColorRgba.White),
                new GradientStop(1, ColorRgba.FromBytes(80, 180, 255)),
            ]);

    private sealed class RecordingDrawCommandSink : IDrawCommandSink
    {
        public List<string> Commands { get; } = [];

        public int LineCount { get; private set; }

        public int CommandCount => Commands.Count;

        public int PrimitiveCount => Commands.Count(command => command.StartsWith("Draw", StringComparison.Ordinal) || command.StartsWith("Fill", StringComparison.Ordinal));

        public int TransientTextLayoutCount { get; }

        public int NativeResourceCount => 0;

        public void Clear(ColorRgba color) => Commands.Add(nameof(Clear));

        public void PushClip(RectF clip) => Commands.Add(nameof(PushClip));

        public void PopClip() => Commands.Add(nameof(PopClip));

        public void PushTransform(Matrix3x2F transform) => Commands.Add(nameof(PushTransform));

        public void PopTransform() => Commands.Add(nameof(PopTransform));

        public void DrawLine(PointF start, PointF end, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
        {
            LineCount++;
            Commands.Add(nameof(DrawLine));
        }

        public void DrawRectangle(RectF rect, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawRectangle));

        public void FillRectangle(RectF rect, BrushHandle brush)
            => Commands.Add(nameof(FillRectangle));

        public void DrawRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawRoundedRectangle));

        public void FillRoundedRectangle(RectF rect, float radiusX, float radiusY, BrushHandle brush)
            => Commands.Add(nameof(FillRoundedRectangle));

        public void DrawCircle(PointF center, float radius, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawCircle));

        public void FillCircle(PointF center, float radius, BrushHandle brush)
            => Commands.Add(nameof(FillCircle));

        public void DrawEllipse(RectF bounds, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawEllipse));

        public void FillEllipse(RectF bounds, BrushHandle brush)
            => Commands.Add(nameof(FillEllipse));

        public void DrawTriangle(PointF a, PointF b, PointF c, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawTriangle));

        public void FillTriangle(PointF a, PointF b, PointF c, BrushHandle brush)
            => Commands.Add(nameof(FillTriangle));

        public void DrawGeometry(GeometryPath geometry, BrushHandle brush, float strokeWidth, StrokeStyleHandle? strokeStyle)
            => Commands.Add(nameof(DrawGeometry));

        public void FillGeometry(GeometryPath geometry, BrushHandle brush)
            => Commands.Add(nameof(FillGeometry));

        public void DrawImage(ImageHandle image, int frameIndex, RectF destination, RectF? source, float opacity, ImageInterpolationMode interpolationMode)
            => Commands.Add(nameof(DrawImage));

        public void DrawText(string text, FontHandle font, BrushHandle brush, PointF origin)
            => Commands.Add(nameof(DrawText));

        public void DrawTextLayout(TextLayoutHandle layout, BrushHandle brush, PointF origin)
            => Commands.Add(nameof(DrawTextLayout));

        public SizeF MeasureText(string text, FontHandle font) => new(text.Length, font.Options.Size);

        public SizeF MeasureTextLayout(TextLayoutHandle layout) => new(layout.Text.Length, layout.Font.Options.Size);
    }
}
