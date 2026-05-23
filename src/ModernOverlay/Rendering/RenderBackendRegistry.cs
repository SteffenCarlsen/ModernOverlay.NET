using System.Reflection;

namespace ModernOverlay.Rendering;

internal interface IRenderBackendProvider
{
    IRenderBackend CreateBackend(OverlayWindowOptions options);
}

internal static class RenderBackendRegistry
{
    private const string DefaultBackendAssemblyName = "ModernOverlay.Direct2D";
    private const string DefaultBackendRegistrationTypeName = "ModernOverlay.Direct2D.Direct2DOverlayBackend";

    private static readonly System.Threading.Lock Gate = new();
    private static IRenderBackendProvider provider = new NullRenderBackendProvider();
    private static bool defaultBackendLoadAttempted;

    public static IRenderBackend CreateBackend(OverlayWindowOptions options)
    {
        IRenderBackendProvider currentProvider;
        lock (Gate)
        {
            currentProvider = provider;
        }

        if (currentProvider is NullRenderBackendProvider && TryRegisterDefaultBackend())
        {
            lock (Gate)
            {
                currentProvider = provider;
            }
        }

        return currentProvider.CreateBackend(options);
    }

    public static void Register(IRenderBackendProvider renderBackendProvider)
    {
        ArgumentNullException.ThrowIfNull(renderBackendProvider);
        lock (Gate)
        {
            provider = renderBackendProvider;
        }
    }

    internal static IDisposable RegisterForScope(IRenderBackendProvider renderBackendProvider)
    {
        ArgumentNullException.ThrowIfNull(renderBackendProvider);
        lock (Gate)
        {
            IRenderBackendProvider previous = provider;
            provider = renderBackendProvider;
            return new RestoreScope(previous);
        }
    }

    internal static IDisposable UseNullBackendForScope()
    {
        lock (Gate)
        {
            IRenderBackendProvider previous = provider;
            provider = new NullRenderBackendProvider();
            defaultBackendLoadAttempted = false;
            return new RestoreScope(previous);
        }
    }

    private static bool TryRegisterDefaultBackend()
    {
        lock (Gate)
        {
            if (defaultBackendLoadAttempted)
            {
                return provider is not NullRenderBackendProvider;
            }

            defaultBackendLoadAttempted = true;
        }

        Assembly assembly;
        try
        {
            assembly = Assembly.Load(DefaultBackendAssemblyName);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }

        Type registrationType = assembly.GetType(DefaultBackendRegistrationTypeName, throwOnError: false)
            ?? throw new InvalidOperationException($"Default backend assembly '{DefaultBackendAssemblyName}' does not contain '{DefaultBackendRegistrationTypeName}'.");
        MethodInfo registerMethod = registrationType.GetMethod(
            "Register",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)
            ?? throw new InvalidOperationException($"Default backend type '{DefaultBackendRegistrationTypeName}' does not expose a public static Register method.");

        try
        {
            registerMethod.Invoke(null, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new InvalidOperationException("Default Direct2D backend registration failed.", exception.InnerException);
        }

        lock (Gate)
        {
            return provider is not NullRenderBackendProvider;
        }
    }

    private sealed class NullRenderBackendProvider : IRenderBackendProvider
    {
        public IRenderBackend CreateBackend(OverlayWindowOptions options) => new NullRenderBackend();
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly IRenderBackendProvider previous;
        private bool disposed;

        public RestoreScope(IRenderBackendProvider previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            lock (Gate)
            {
                provider = previous;
                defaultBackendLoadAttempted = false;
            }
        }
    }
}
