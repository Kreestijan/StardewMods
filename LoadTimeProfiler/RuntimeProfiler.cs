using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;

namespace LoadTimeProfiler;

public sealed class RuntimeProfiler
{
    private static readonly ConcurrentDictionary<Type, Func<object, string>> EventNameAccessors = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object>> HandlerAccessors = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object>> SourceModAccessors = new();
    private static readonly ConcurrentDictionary<Type, Func<object, string?>> MetadataDisplayNameAccessors = new();
    private static readonly ConcurrentDictionary<Type, Func<object, string?>> MetadataUniqueIdAccessors = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object?>> MetadataManifestAccessors = new();
    private static readonly ConcurrentDictionary<Type, Func<object, string?>> ManifestNameAccessors = new();
    private static readonly ConcurrentDictionary<Type, Func<object, string?>> ManifestUniqueIdAccessors = new();
    private static readonly ConcurrentDictionary<Type, Action<object, object?>> DelegateInvokers = new();

    private readonly Mod mod;
    private readonly Func<ModConfig> getConfig;
    private readonly Queue<Dictionary<string, double>> drawHistory = new();
    private readonly Queue<Dictionary<string, double>> updateHistory = new();
    private readonly Dictionary<string, double> drawRollingTotals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> updateRollingTotals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<EventContext?> eventStack = new();
    private readonly object syncRoot = new();

    private Dictionary<string, double> currentDrawFrame = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, double> currentUpdateFrame = new(StringComparer.OrdinalIgnoreCase);
    private OverlaySnapshot latestSnapshot = OverlaySnapshot.Empty;
    private Stopwatch? frameStopwatch;
    private double latestFrameTimeMs;
    private double latestFps;
    private int displayRefreshCounter;

    public RuntimeProfiler(Mod mod, Func<ModConfig> getConfig)
    {
        this.mod = mod;
        this.getConfig = getConfig;
        Instance = this;
    }

    public static RuntimeProfiler? Instance { get; private set; }

    public bool IsAvailable { get; private set; }

    public OverlaySnapshot LatestSnapshot
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.latestSnapshot;
            }
        }
    }

    public void Enable(HarmonyLib.Harmony harmony)
    {
        try
        {
            int patchCount = HarmonyPatches.ApplyRuntimeProfilerPatches(harmony);
            if (patchCount <= 0)
            {
                throw new InvalidOperationException("No SMAPI managed event methods were patched.");
            }

            this.IsAvailable = true;
        }
        catch (Exception ex)
        {
            this.mod.Monitor.Log("Load Time Profiler: Could not patch SMAPI managed events — runtime timing unavailable", LogLevel.Warn);
            this.mod.Monitor.Log(ex.ToString(), LogLevel.Trace);
            this.IsAvailable = false;
        }
    }

    public static void EnterManagedEvent(object managedEvent)
    {
        Instance?.PushEvent(managedEvent);
    }

    public static void ExitManagedEvent()
    {
        Instance?.PopEvent();
    }

    public static void InvokeManagedHandler(object handlerObject, object? args)
    {
        RuntimeProfiler? profiler = Instance;
        if (profiler is null)
        {
            InvokeRawHandler(handlerObject, args);
            return;
        }

        profiler.InvokeObservedHandler(handlerObject, args);
    }

    public void AdvanceUpdateFrame()
    {
        lock (this.syncRoot)
        {
            this.EnsureWindowSize();

            if (!this.ShouldSample())
            {
                this.currentUpdateFrame = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            this.PushFrame(this.currentUpdateFrame, this.updateHistory, this.updateRollingTotals);
            this.currentUpdateFrame = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void AdvanceDrawFrame()
    {
        lock (this.syncRoot)
        {
            this.EnsureWindowSize();
            this.UpdateFrameMetrics();

            if (!this.ShouldSample())
            {
                this.currentDrawFrame = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            this.PushFrame(this.currentDrawFrame, this.drawHistory, this.drawRollingTotals);
            this.currentDrawFrame = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            this.displayRefreshCounter++;
            if (this.displayRefreshCounter >= 60)
            {
                this.displayRefreshCounter = 0;
                this.latestSnapshot = this.BuildSnapshot();
            }
        }
    }

    public void Clear()
    {
        lock (this.syncRoot)
        {
            this.drawHistory.Clear();
            this.updateHistory.Clear();
            this.drawRollingTotals.Clear();
            this.updateRollingTotals.Clear();
            this.currentDrawFrame.Clear();
            this.currentUpdateFrame.Clear();
            this.latestSnapshot = OverlaySnapshot.Empty;
            this.eventStack.Clear();
            this.displayRefreshCounter = 0;
            this.frameStopwatch = null;
            this.latestFrameTimeMs = 0;
            this.latestFps = 0;
        }
    }

    private void PushEvent(object managedEvent)
    {
        lock (this.syncRoot)
        {
            string eventName = GetEventName(managedEvent);
            ProfileCategory? category = ClassifyEvent(eventName);
            this.eventStack.Push(category is null ? null : new EventContext(eventName, category.Value));
        }
    }

    private void PopEvent()
    {
        lock (this.syncRoot)
        {
            if (this.eventStack.Count > 0)
            {
                this.eventStack.Pop();
            }
        }
    }

    private void InvokeObservedHandler(object handlerObject, object? args)
    {
        EventContext? context;
        lock (this.syncRoot)
        {
            context = this.eventStack.Count > 0 ? this.eventStack.Peek() : null;
        }

        if (context is null)
        {
            InvokeRawHandler(handlerObject, args);
            return;
        }

        object sourceMod = GetSourceMod(handlerObject);
        string modName = GetMetadataDisplayName(sourceMod)
            ?? GetManifestName(sourceMod)
            ?? GetMetadataUniqueId(sourceMod)
            ?? GetManifestUniqueId(sourceMod)
            ?? "Unknown";
        long start = Stopwatch.GetTimestamp();

        try
        {
            InvokeRawHandler(handlerObject, args);
        }
        finally
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            lock (this.syncRoot)
            {
                Dictionary<string, double> target = context.Category == ProfileCategory.Draw
                    ? this.currentDrawFrame
                    : this.currentUpdateFrame;
                target.TryGetValue(modName, out double current);
                target[modName] = current + elapsedMs;
            }
        }
    }

    private void PushFrame(
        Dictionary<string, double> frame,
        Queue<Dictionary<string, double>> history,
        Dictionary<string, double> rollingTotals)
    {
        history.Enqueue(frame);
        foreach ((string name, double costMs) in frame)
        {
            rollingTotals.TryGetValue(name, out double currentValue);
            rollingTotals[name] = currentValue + costMs;
        }

        int windowSize = this.getConfig().OverlaySampleWindow;
        while (history.Count > windowSize)
        {
            Dictionary<string, double> removedFrame = history.Dequeue();
            foreach ((string name, double costMs) in removedFrame)
            {
                double updated = rollingTotals[name] - costMs;
                if (updated <= 0.0001)
                {
                    rollingTotals.Remove(name);
                }
                else
                {
                    rollingTotals[name] = updated;
                }
            }
        }
    }

    private OverlaySnapshot BuildSnapshot()
    {
        int drawFrameCount = Math.Max(this.drawHistory.Count, 1);
        int updateFrameCount = Math.Max(this.updateHistory.Count, 1);
        int topN = this.getConfig().OverlayTopN;

        List<OverlayRow> drawRows = this.drawRollingTotals
            .Select(pair => new OverlayRow(pair.Key, pair.Value / drawFrameCount))
            .OrderByDescending(row => row.AverageMs)
            .Take(topN)
            .ToList();

        List<OverlayRow> updateRows = this.updateRollingTotals
            .Select(pair => new OverlayRow(pair.Key, pair.Value / updateFrameCount))
            .OrderByDescending(row => row.AverageMs)
            .Take(topN)
            .ToList();

        double drawTax = this.drawRollingTotals.Sum(pair => pair.Value) / drawFrameCount;
        double updateTax = this.updateRollingTotals.Sum(pair => pair.Value) / updateFrameCount;

        return new OverlaySnapshot(this.latestFps, this.latestFrameTimeMs, drawRows, updateRows, drawTax, updateTax);
    }

    private void EnsureWindowSize()
    {
        int windowSize = this.getConfig().OverlaySampleWindow;
        TrimHistory(this.drawHistory, this.drawRollingTotals, windowSize);
        TrimHistory(this.updateHistory, this.updateRollingTotals, windowSize);
    }

    private void UpdateFrameMetrics()
    {
        if (this.frameStopwatch is null)
        {
            this.frameStopwatch = Stopwatch.StartNew();
            this.latestFrameTimeMs = 0;
            this.latestFps = 0;
            return;
        }

        this.latestFrameTimeMs = this.frameStopwatch.Elapsed.TotalMilliseconds;
        this.latestFps = this.latestFrameTimeMs <= 0.0001 ? 0 : 1000d / this.latestFrameTimeMs;
        this.frameStopwatch.Restart();
    }

    private bool ShouldSample()
    {
        return Context.IsWorldReady
            && Game1.currentLocation is not null
            && Game1.gameMode == 3
            && !Game1.paused;
    }

    private static void TrimHistory(
        Queue<Dictionary<string, double>> history,
        Dictionary<string, double> rollingTotals,
        int windowSize)
    {
        while (history.Count > windowSize)
        {
            Dictionary<string, double> removedFrame = history.Dequeue();
            foreach ((string name, double costMs) in removedFrame)
            {
                double updated = rollingTotals[name] - costMs;
                if (updated <= 0.0001)
                {
                    rollingTotals.Remove(name);
                }
                else
                {
                    rollingTotals[name] = updated;
                }
            }
        }
    }

    private static string GetEventName(object managedEvent)
    {
        Func<object, string> accessor = EventNameAccessors.GetOrAdd(managedEvent.GetType(), CreateEventNameAccessor);
        return accessor(managedEvent);
    }

    private static Func<object, string> CreateEventNameAccessor(Type managedEventType)
    {
        PropertyInfo property = managedEventType.GetProperty("EventName")
            ?? throw new MissingMemberException(managedEventType.FullName, "EventName");

        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        UnaryExpression typedInstance = Expression.Convert(instance, managedEventType);
        MethodCallExpression getter = Expression.Call(typedInstance, property.GetGetMethod(nonPublic: false)!);
        return Expression.Lambda<Func<object, string>>(getter, instance).Compile();
    }

    private static object GetSourceMod(object handlerObject)
    {
        Func<object, object> accessor = SourceModAccessors.GetOrAdd(handlerObject.GetType(), CreateSourceModAccessor);
        return accessor(handlerObject);
    }

    private static object GetHandlerDelegate(object handlerObject)
    {
        Func<object, object> accessor = HandlerAccessors.GetOrAdd(handlerObject.GetType(), CreateHandlerAccessor);
        return accessor(handlerObject);
    }

    private static void InvokeRawHandler(object handlerObject, object? args)
    {
        object handlerDelegate = GetHandlerDelegate(handlerObject);
        Action<object, object?> invoker = DelegateInvokers.GetOrAdd(handlerDelegate.GetType(), CreateDelegateInvoker);
        invoker(handlerDelegate, args);
    }

    private static Func<object, object> CreateHandlerAccessor(Type handlerType)
    {
        PropertyInfo property = handlerType.GetProperty("Handler")
            ?? throw new MissingMemberException(handlerType.FullName, "Handler");

        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        UnaryExpression typedInstance = Expression.Convert(instance, handlerType);
        MemberExpression propertyAccess = Expression.Property(typedInstance, property);
        UnaryExpression box = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<object, object>>(box, instance).Compile();
    }

    private static string? GetMetadataDisplayName(object sourceMod)
    {
        Func<object, string?> accessor = MetadataDisplayNameAccessors.GetOrAdd(sourceMod.GetType(), type => CreateOptionalStringPropertyAccessor(type, "DisplayName"));
        return accessor(sourceMod);
    }

    private static string? GetMetadataUniqueId(object sourceMod)
    {
        Func<object, string?> accessor = MetadataUniqueIdAccessors.GetOrAdd(sourceMod.GetType(), type => CreateOptionalStringPropertyAccessor(type, "UniqueID"));
        return accessor(sourceMod);
    }

    private static string? GetManifestName(object sourceMod)
    {
        object? manifest = GetManifest(sourceMod);
        if (manifest is null)
        {
            return null;
        }

        Func<object, string?> accessor = ManifestNameAccessors.GetOrAdd(manifest.GetType(), type => CreateOptionalStringPropertyAccessor(type, "Name"));
        return accessor(manifest);
    }

    private static string? GetManifestUniqueId(object sourceMod)
    {
        object? manifest = GetManifest(sourceMod);
        if (manifest is null)
        {
            return null;
        }

        Func<object, string?> accessor = ManifestUniqueIdAccessors.GetOrAdd(manifest.GetType(), type => CreateOptionalStringPropertyAccessor(type, "UniqueID"));
        return accessor(manifest);
    }

    private static Func<object, object> CreateSourceModAccessor(Type handlerType)
    {
        PropertyInfo property = handlerType.GetProperty("SourceMod")
            ?? throw new MissingMemberException(handlerType.FullName, "SourceMod");

        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        UnaryExpression typedInstance = Expression.Convert(instance, handlerType);
        MemberExpression propertyAccess = Expression.Property(typedInstance, property);
        UnaryExpression box = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<object, object>>(box, instance).Compile();
    }

    private static object? GetManifest(object sourceMod)
    {
        Func<object, object?> accessor = MetadataManifestAccessors.GetOrAdd(sourceMod.GetType(), CreateManifestAccessor);
        return accessor(sourceMod);
    }

    private static Func<object, object?> CreateManifestAccessor(Type objectType)
    {
        PropertyInfo? property = objectType.GetProperty("Manifest");
        if (property is null)
        {
            return _ => null;
        }

        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        UnaryExpression typedInstance = Expression.Convert(instance, objectType);
        MemberExpression propertyAccess = Expression.Property(typedInstance, property);
        UnaryExpression box = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<object, object?>>(box, instance).Compile();
    }

    private static Func<object, string?> CreateOptionalStringPropertyAccessor(Type objectType, string propertyName)
    {
        PropertyInfo? property = objectType.GetProperty(propertyName);
        if (property is null)
        {
            return _ => null;
        }

        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        UnaryExpression typedInstance = Expression.Convert(instance, objectType);
        MemberExpression propertyAccess = Expression.Property(typedInstance, property);
        return Expression.Lambda<Func<object, string?>>(propertyAccess, instance).Compile();
    }

    private static Action<object, object?> CreateDelegateInvoker(Type delegateType)
    {
        MethodInfo invokeMethod = delegateType.GetMethod("Invoke")
            ?? throw new MissingMethodException(delegateType.FullName, "Invoke");
        Type argsType = invokeMethod.GetParameters()[1].ParameterType;

        ParameterExpression handler = Expression.Parameter(typeof(object), "handler");
        ParameterExpression args = Expression.Parameter(typeof(object), "args");
        UnaryExpression typedHandler = Expression.Convert(handler, delegateType);
        UnaryExpression typedArgs = Expression.Convert(args, argsType);
        ConstantExpression sender = Expression.Constant(null, typeof(object));
        MethodCallExpression invoke = Expression.Call(typedHandler, invokeMethod, sender, typedArgs);
        return Expression.Lambda<Action<object, object?>>(invoke, handler, args).Compile();
    }

    private static ProfileCategory? ClassifyEvent(string eventName)
    {
        if (eventName.StartsWith("Display.", StringComparison.Ordinal))
        {
            return ProfileCategory.Draw;
        }

        if (eventName.StartsWith("GameLoop.", StringComparison.Ordinal)
            || eventName.StartsWith("Input.", StringComparison.Ordinal)
            || eventName.StartsWith("World.", StringComparison.Ordinal)
            || eventName.StartsWith("Player.", StringComparison.Ordinal)
            || eventName.StartsWith("Multiplayer.", StringComparison.Ordinal))
        {
            return ProfileCategory.Update;
        }

        return null;
    }

    private sealed record EventContext(string EventName, ProfileCategory Category);

    public sealed record OverlayRow(string Name, double AverageMs);

    public sealed record OverlaySnapshot(
        double Fps,
        double FrameTimeMs,
        IReadOnlyList<OverlayRow> DrawRows,
        IReadOnlyList<OverlayRow> UpdateRows,
        double DrawTaxMs,
        double UpdateTaxMs
    )
    {
        public static OverlaySnapshot Empty { get; } = new(
            0,
            0,
            Array.Empty<OverlayRow>(),
            Array.Empty<OverlayRow>(),
            0,
            0
        );
    }

    public enum ProfileCategory
    {
        Draw,
        Update
    }
}
