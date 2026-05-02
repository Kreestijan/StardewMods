using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using StardewModdingAPI;

namespace LoadTimeProfiler;

public sealed class LoadProfiler
{
    private const string LogPattern = @"\]    (.+?) \d+\.\d+\.\d+.*? in (\d+)ms$";

    private static readonly object RecordLock = new();
    private static readonly List<(string ModName, long Milliseconds)> Records = new();
    private static readonly object ActiveLock = new();
    private static readonly Dictionary<object, long> ActiveEntries = new();
    private static readonly Dictionary<Type, Func<object, string?>> DisplayNameAccessors = new();
    private static readonly Dictionary<Type, Func<object, object?>> ManifestAccessors = new();
    private static readonly Dictionary<Type, Func<object, string?>> UniqueIdAccessors = new();

    private static bool bootstrapSucceeded;
    private static bool bootstrapTried;

    public LoadProfiler(Mod mod, ModConfig config)
    {
        this.Mod = mod;
        this.Config = config;
    }

    private Mod Mod { get; }

    private ModConfig Config { get; }

    [ModuleInitializer]
    public static void InitializeBootstrap()
    {
        try
        {
            Assembly? harmonyAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "0Harmony");

            if (harmonyAssembly is null)
                return;

            object harmony = Activator.CreateInstance(
                harmonyAssembly.GetType("HarmonyLib.Harmony")!,
                "Kree.LoadTimeProfiler.StartupBootstrap"
            )!;

            typeof(HarmonyPatches).GetMethod("ApplyLoadProfilerPatches", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, new[] { harmony });

            bootstrapSucceeded = true;
        }
        catch
        {
            // Bootstrap failed silently — log parser is the fallback
            bootstrapSucceeded = false;
        }
        finally
        {
            bootstrapTried = true;
        }
    }

    public static void BeforeHeuristicPush(object[] __args)
    {
        if (__args.Length == 0 || __args[0] is null)
            return;

        try
        {
            long start = Stopwatch.GetTimestamp();
            lock (ActiveLock)
            {
                ActiveEntries[__args[0]] = start;
            }
        }
        catch { }
    }

    public static void AfterHeuristicTryPop(bool __result, object[] __args)
    {
        if (!__result || __args.Length == 0 || __args[0] is null)
            return;

        try
        {
            long start;
            lock (ActiveLock)
            {
                if (!ActiveEntries.Remove(__args[0], out start))
                    return;
            }

            long elapsedMs = (long)((Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency);
            string modName = GetDisplayName(__args[0]) ?? GetUniqueId(__args[0]) ?? __args[0].GetType().Name;

            lock (RecordLock)
            {
                Records.Add((modName, elapsedMs));
            }
        }
        catch { }
    }

    public void LogResults()
    {
        if (!this.Config.LogOnStartup)
            return;

        if (bootstrapTried && bootstrapSucceeded && TryGatherRecords(out List<(string, long)> records))
        {
            this.PrintResults(records);
            return;
        }

        if (this.TryParseFromLog(out List<(string, long)> logRecords))
        {
            this.PrintResults(logRecords);
            return;
        }

        this.Mod.Monitor.Log("Load Time Profiler: No load timing data was captured.", LogLevel.Warn);
    }

    private bool TryGatherRecords(out List<(string ModName, long Milliseconds)> records)
    {
        lock (RecordLock)
        {
            if (Records.Count == 0)
            {
                records = new();
                return false;
            }

            records = Records.ToList();
            return true;
        }
    }

    private bool TryParseFromLog(out List<(string ModName, long Milliseconds)> records)
    {
        records = new();
        string logPath = Path.Combine(Constants.LogDir, "SMAPI-latest.txt");
        if (!File.Exists(logPath))
            return false;

        try
        {
            Regex regex = new(LogPattern, RegexOptions.Multiline);
            string content = File.ReadAllText(logPath);
            foreach (Match match in regex.Matches(content))
            {
                string modName = match.Groups[1].Value.Trim();
                if (long.TryParse(match.Groups[2].Value, out long ms) && !modName.Equals("SMAPI", StringComparison.OrdinalIgnoreCase))
                    records.Add((modName, ms));
            }
        }
        catch { return false; }

        return records.Count > 0;
    }

    private void PrintResults(List<(string ModName, long Milliseconds)> records)
    {
        List<(string ModName, long Milliseconds)> sorted = records
            .OrderBy(r => r.Milliseconds)
            .ThenBy(r => r.ModName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<(string ModName, long Milliseconds)> flagged = sorted
            .Where(r => r.Milliseconds >= this.Config.ThresholdMs)
            .ToList();

        long totalMs = sorted.Sum(r => r.Milliseconds);
        var displayed = this.Config.ShowAllMods ? sorted : flagged;

        this.Mod.Monitor.Log("============ MOD LOAD TIMES ============", LogLevel.Info);
        this.Mod.Monitor.Log($"(threshold: {this.Config.ThresholdMs}ms | {sorted.Count} mods total | {flagged.Count} flagged)", LogLevel.Info);
        this.Mod.Monitor.Log("----------------------------------------", LogLevel.Info);

        for (int i = 0; i < displayed.Count; i++)
        {
            var (name, ms) = displayed[i];
            bool isHeaviest = i == displayed.Count - 1;
            TimingTier tier = GetTier(ms, this.Config.ThresholdMs);
            string line = $"{GetIcon(tier, isHeaviest)}  {name,-28} {ms,5}ms{(isHeaviest ? " [HEAVIEST]" : "")}";
            this.Mod.Monitor.Log(line, GetLogLevel(tier));
        }

        this.Mod.Monitor.Log("----------------------------------------", LogLevel.Info);
        this.Mod.Monitor.Log($"Total mod load time: {totalMs}ms", LogLevel.Info);
        this.Mod.Monitor.Log("========================================", LogLevel.Info);
    }

    private static string? GetDisplayName(object metadata)
    {
        Func<object, string?> accessor;
        lock (ActiveLock)
        {
            if (!DisplayNameAccessors.TryGetValue(metadata.GetType(), out accessor!))
            {
                accessor = CreateStringPropertyAccessor(metadata.GetType(), "DisplayName");
                DisplayNameAccessors[metadata.GetType()] = accessor;
            }
        }
        return accessor(metadata);
    }

    private static string? GetUniqueId(object metadata)
    {
        object? manifest = GetManifest(metadata);
        if (manifest is null)
            return null;

        Func<object, string?> accessor;
        lock (ActiveLock)
        {
            if (!UniqueIdAccessors.TryGetValue(manifest.GetType(), out accessor!))
            {
                accessor = CreateStringPropertyAccessor(manifest.GetType(), "UniqueID");
                UniqueIdAccessors[manifest.GetType()] = accessor;
            }
        }
        return accessor(manifest);
    }

    private static object? GetManifest(object metadata)
    {
        Func<object, object?> accessor;
        lock (ActiveLock)
        {
            if (!ManifestAccessors.TryGetValue(metadata.GetType(), out accessor!))
            {
                PropertyInfo? property = metadata.GetType().GetProperty("Manifest");
                accessor = property is not null ? m => property.GetValue(m) : _ => null;
                ManifestAccessors[metadata.GetType()] = accessor;
            }
        }
        return accessor(metadata);
    }

    private static Func<object, string?> CreateStringPropertyAccessor(Type objectType, string propertyName)
    {
        PropertyInfo? property = objectType.GetProperty(propertyName);
        return property is not null ? metadata => property.GetValue(metadata) as string : _ => null;
    }

    private static TimingTier GetTier(long elapsedMs, int thresholdMs)
    {
        if (elapsedMs < thresholdMs) return TimingTier.Ok;
        if (elapsedMs < thresholdMs * 3L) return TimingTier.Slow;
        if (elapsedMs < thresholdMs * 10L) return TimingTier.Heavy;
        return TimingTier.Critical;
    }

    private static LogLevel GetLogLevel(TimingTier tier) => tier switch
    {
        TimingTier.Ok => LogLevel.Debug,
        TimingTier.Slow => LogLevel.Warn,
        TimingTier.Heavy => LogLevel.Warn,
        _ => LogLevel.Error
    };

    private static string GetIcon(TimingTier tier, bool heavy)
    {
        if (heavy) return "✖";
        return tier switch
        {
            TimingTier.Ok => "✓",
            TimingTier.Slow => "⚠",
            TimingTier.Heavy => "●",
            _ => "✖"
        };
    }

    private enum TimingTier { Ok, Slow, Heavy, Critical }
}
