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

    public EditorMode Mode { get; set; } = EditorMode.Edit;

    public int SelectedCommandIndex { get; set; } = -1;

    public bool IsDirty { get; set; }

    [JsonIgnore]
    public Stack<string> UndoStack { get; } = new();

    [JsonIgnore]
    public Stack<string> RedoStack { get; } = new();

    public int PlaybackCommandIndex { get; set; } = -1;

    [JsonIgnore]
    public List<PreviewEmote> PreviewEmotes { get; } = new();

    [JsonIgnore]
    public string LastSavedContentJsonPath { get; set; } = string.Empty;
}
