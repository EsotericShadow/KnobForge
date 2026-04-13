using Avalonia;
using Avalonia.Controls;
using Avalonia.Native;
using Avalonia.Platform;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using KnobForge.App.Diagnostics;

namespace KnobForge.App;

class Program
{
    private static readonly object ManagedRenderLoopGate = new();
    private static object? s_managedRenderTimer;
    private static object? s_managedRenderLoop;
    private static Harmony? s_harmony;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        WireFatalExceptionLogging();

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

        string requestedMode = (Environment.GetEnvironmentVariable("KNOBFORGE_RENDER_MODE") ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        string mode = string.IsNullOrWhiteSpace(requestedMode) ? "auto" : requestedMode;

        Console.WriteLine($">>> RenderMode={mode}");

        var appBuilder = BuildAvaloniaApp();
        if (OperatingSystem.IsMacOS())
        {
            InstallMacOsCompatibilityPatches();

            appBuilder = appBuilder
                .AfterPlatformServicesSetup(_ => InstallManagedAvaloniaRenderLoop())
                .AfterSetup(_ => InstallManagedAvaloniaRenderLoop());

            Action? originalWindowingInitializer = appBuilder.WindowingSubsystemInitializer;
            if (originalWindowingInitializer is not null)
            {
                appBuilder = appBuilder.UseWindowingSubsystem(() =>
                {
                    InstallManagedAvaloniaRenderLoop();
                    originalWindowingInitializer();
                    InstallManagedAvaloniaRenderLoop();
                }, appBuilder.WindowingSubsystemName ?? string.Empty);
            }

            AvaloniaNativePlatformOptions options = mode switch
            {
                "metal" => new AvaloniaNativePlatformOptions
                {
                    RenderingMode = new[]
                    {
                        AvaloniaNativeRenderingMode.Metal,
                        AvaloniaNativeRenderingMode.OpenGl,
                        AvaloniaNativeRenderingMode.Software
                    }
                },
                "opengl" => new AvaloniaNativePlatformOptions
                {
                    RenderingMode = new[]
                    {
                        AvaloniaNativeRenderingMode.OpenGl,
                        AvaloniaNativeRenderingMode.Software
                    }
                },
                "software" => new AvaloniaNativePlatformOptions
                {
                    RenderingMode = new[] { AvaloniaNativeRenderingMode.Software }
                },
                _ => new AvaloniaNativePlatformOptions
                {
                    // Avalonia's documented macOS default is OpenGL with Software fallback.
                    // Keep the app shell on the stable platform defaults; the heavy 3D viewport
                    // continues to use our own Metal pipeline independently.
                    RenderingMode = new[]
                    {
                        AvaloniaNativeRenderingMode.OpenGl,
                        AvaloniaNativeRenderingMode.Software
                    }
                }
            };

            appBuilder = appBuilder.With(options);
        }

        try
        {
            appBuilder.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            string message = $">>> [Fatal] Startup crash: {ex}";
            Console.Error.WriteLine(message);
            FatalLog.Append(message);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void InstallManagedAvaloniaRenderLoop()
    {
        try
        {
            lock (ManagedRenderLoopGate)
            {
                Assembly avaloniaBaseAssembly = typeof(AvaloniaObject).Assembly;
                Type locatorType = avaloniaBaseAssembly.GetType("Avalonia.AvaloniaLocator", throwOnError: true)!;
                object locator = locatorType
                    .GetProperty("CurrentMutable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
                    .GetValue(null)!;

                Type renderTimerInterfaceType = avaloniaBaseAssembly.GetType("Avalonia.Rendering.IRenderTimer", throwOnError: true)!;
                Type renderLoopInterfaceType = avaloniaBaseAssembly.GetType("Avalonia.Rendering.IRenderLoop", throwOnError: true)!;

                s_managedRenderTimer ??= Activator.CreateInstance(
                    avaloniaBaseAssembly.GetType("Avalonia.Rendering.UiThreadRenderTimer", throwOnError: true)!,
                    60)!;

                s_managedRenderLoop ??= Activator.CreateInstance(
                    avaloniaBaseAssembly.GetType("Avalonia.Rendering.RenderLoop", throwOnError: true)!,
                    s_managedRenderTimer)!;

                BindLocatorService(locatorType, locator, renderTimerInterfaceType, s_managedRenderTimer);
                BindLocatorService(locatorType, locator, renderLoopInterfaceType, s_managedRenderLoop);
            }
        }
        catch (Exception ex)
        {
            string message = $">>> [Startup] Failed to install managed Avalonia render loop fallback: {ex}";
            Console.Error.WriteLine(message);
            FatalLog.Append(message);
        }
    }

    private static void BindLocatorService(Type locatorType, object locator, Type serviceType, object implementation)
    {
        MethodInfo bindMethod = locatorType
            .GetMethod("Bind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .MakeGenericMethod(serviceType);

        object registrationHelper = bindMethod.Invoke(locator, null)!;

        MethodInfo toConstantMethod = registrationHelper.GetType()
            .GetMethod("ToConstant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .MakeGenericMethod(implementation.GetType());

        _ = toConstantMethod.Invoke(registrationHelper, new[] { implementation });
    }

    private static void InstallMacOsCompatibilityPatches()
    {
        if (s_harmony is not null)
        {
            return;
        }

        Assembly nativeAssembly = typeof(AvaloniaNativePlatformOptions).Assembly;
        Type handleType = nativeAssembly.GetType("Avalonia.Native.MacOSTopLevelHandle", throwOnError: true)!;
        Type windowBaseImplType = nativeAssembly.GetType("Avalonia.Native.WindowBaseImpl", throwOnError: true)!;
        MethodInfo initMethod = windowBaseImplType.GetMethod(
            "Init",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { handleType },
            null)
            ?? throw new InvalidOperationException("WindowBaseImpl.Init was not found.");

        MethodInfo transpilerMethod = typeof(Program).GetMethod(
            nameof(WindowBaseImplInitTranspiler),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WindowBaseImplInitTranspiler was not found.");

        s_harmony = new Harmony("com.knobforge.macos26compat");
        s_harmony.Patch(initMethod, transpiler: new HarmonyMethod(transpilerMethod));
    }

    private static IEnumerable<CodeInstruction> WindowBaseImplInitTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo enumerableFirstMethod = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(method =>
            {
                if (method.Name != nameof(Enumerable.First) || !method.IsGenericMethodDefinition)
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2;
            })
            .MakeGenericMethod(typeof(Screen));

        MethodInfo safeFirstMethod = typeof(Program).GetMethod(
            nameof(SafeFirstScreen),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SafeFirstScreen was not found.");

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(enumerableFirstMethod))
            {
                yield return new CodeInstruction(OpCodes.Call, safeFirstMethod);
                continue;
            }

            yield return instruction;
        }
    }

    private static Screen SafeFirstScreen(IEnumerable<Screen> screens, Func<Screen, bool> predicate)
    {
        Screen[] screenArray = screens.ToArray();

        Screen? match = screenArray.FirstOrDefault(predicate);
        if (match is not null)
        {
            return match;
        }

        Screen? primary = screenArray.FirstOrDefault(screen => screen.IsPrimary);
        if (primary is not null)
        {
            return primary;
        }

        if (screenArray.Length > 0)
        {
            return screenArray[0];
        }

        return new Screen(
            1.0,
            new PixelRect(0, 0, 4096, 4096),
            new PixelRect(0, 0, 4096, 4096),
            true);
    }

    private static void WireFatalExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            string details = e.ExceptionObject is Exception ex
                ? ex.ToString()
                : e.ExceptionObject?.ToString() ?? "<null>";
            FatalLog.Append($">>> [UnhandledException] IsTerminating={e.IsTerminating} {details}");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            FatalLog.Append($">>> [UnobservedTaskException] {e.Exception}");
            e.SetObserved();
        };
    }
}
