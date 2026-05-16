using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewValley;

namespace CutsceneMaker.Models;

public enum EditorMode
{
    Edit,
    Play
}

public sealed class EditorState
{
    public const int MaxUndoDepth = 50;

    public CutsceneData Cutscene { get; set; } = CutsceneData.CreateBlank();

    [JsonIgnore]
    public GameLocation? BootstrappedMap { get; set; }

    [JsonIgnore]
    public string MapLoadFailureMessage { get; set; } = string.Empty;

    public EditorMode Mode { get; set; } = EditorMode.Edit;

    public int SelectedCommandIndex { get; set; } = -1;

    public string SelectedSetupActorSlotId { get; set; } = string.Empty;

    public bool IsDirty { get; set; }

    [JsonIgnore]
    public Stack<string> UndoStack { get; } = new();

    [JsonIgnore]
    public Stack<string> RedoStack { get; } = new();

    public int PlaybackCommandIndex { get; set; } = -1;

    [JsonIgnore]
    public string LastSavedContentJsonPath { get; set; } = string.Empty;

    [JsonIgnore]
    public string SelectedLocationId { get; set; } = "Town";

    /// <summary>The timeline block index where playback will start from. -1 means the setup block.</summary>
    public int CommandMarkerIndex { get; set; } = -1;

    /// <summary>Actor positions accumulated by fast-track simulation. Passed to EventScriptBuilder for play-from-marker.</summary>
    [JsonIgnore]
    public Dictionary<string, Point> SimulatedActorPositions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Viewport pixel position from the last fast-tracked viewport command. -1 = unset.</summary>
    [JsonIgnore]
    public int SimulatedViewportX { get; set; } = -1;

    /// <summary>Viewport pixel position from the last fast-tracked viewport command. -1 = unset.</summary>
    [JsonIgnore]
    public int SimulatedViewportY { get; set; } = -1;

    /// <summary>Target center pixel from the last fast-tracked viewport command. -1 = unset.
    /// Used by GetInitialPlaybackViewport to compute the correct viewport position
    /// for the current game viewport dimensions at playback time.</summary>
    [JsonIgnore]
    public int SimulatedViewportCenterX { get; set; } = -1;

    [JsonIgnore]
    public int SimulatedViewportCenterY { get; set; } = -1;
}
