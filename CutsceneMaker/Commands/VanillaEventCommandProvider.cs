namespace CutsceneMaker.Commands;

public static class VanillaEventCommandProvider
{
    public const string ModId = "StardewValley";
    private const string ProviderName = "Vanilla";
    private const string Badge = "Vanilla";

    public static IEnumerable<EventCommandDefinition> GetDefinitions()
    {
        yield return Define("action", "Action", "action", Raw("action", "Action", "AddMoney 500", "Wiki syntax: action <action>. Example: AddMoney 500."));
        yield return Define("addBigProp", "Add Big Prop", "addBigProp", Integer("x", "X", "0"), Integer("y", "Y", "0"), Text("objectId", "Object ID", "0"));
        yield return Define("addConversationTopic", "Add Conversation Topic", "addConversationTopic", Text("id", "Topic ID", "example"), OptionalInteger("length", "Length"));
        yield return Define("addCookingRecipe", "Add Cooking Recipe", "addCookingRecipe", Text("recipe", "Recipe", "Fried Egg"));
        yield return Define("addCraftingRecipe", "Add Crafting Recipe", "addCraftingRecipe", Text("recipe", "Recipe", "Chest"));
        yield return Define("addFloorProp", "Add Floor Prop", "addFloorProp", Integer("prop", "Prop", "0"), Integer("x", "X", "0"), Integer("y", "Y", "0"), Raw("extra", "Optional fields"));
        yield return Define("addItem", "Add Item", "addItem", Text("itemId", "Item ID", "(O)74"), OptionalInteger("count", "Count", "1"), OptionalInteger("quality", "Quality"));
        yield return Define("addLantern", "Add Lantern", "addLantern", Integer("row", "Row", "735"), Integer("x", "X", "0"), Integer("y", "Y", "0"), Integer("radius", "Light Radius", "0"));
        yield return Define("addObject", "Add Object", "addObject", Integer("x", "X", "0"), Integer("y", "Y", "0"), Text("itemId", "Item ID", "(O)74"), Float("depth", "Layer Depth", "", optional: true));
        yield return Define("addProp", "Add Prop", "addProp", Integer("prop", "Prop", "0"), Integer("x", "X", "0"), Integer("y", "Y", "0"), Raw("extra", "Optional fields"));
        yield return Define("addQuest", "Add Quest", "addQuest", Text("questId", "Quest ID", "1"));
        yield return Define("addSpecialOrder", "Add Special Order", "addSpecialOrder", Text("orderId", "Order ID", "ExampleOrder"));
        yield return Define("addTemporaryActor", "Add Temporary Actor", "addTemporaryActor", Text("sprite", "Sprite Asset", "Ghost"), Integer("width", "Width", "16"), Integer("height", "Height", "32"), Integer("tileX", "Tile X", "0"), Integer("tileY", "Tile Y", "0"), Direction("direction", "Direction"), Raw("extra", "Optional fields"));
        yield return Define("advancedMove", "Advanced Move", "advancedMove", Actor("actor", "Actor"), Bool("loop", "Loop", "false"), Raw("steps", "Steps", "0 1 2 0"));
        yield return Define("ambientLight", "Ambient Light", "ambientLight", Integer("r", "R", "255"), Integer("g", "G", "255"), Integer("b", "B", "255"));
        yield return DefineUnsafe("animalNaming", "Animal Naming", "animalNaming");
        yield return Define("animate", "Animate", "animate", Actor("actor", "Actor"), Bool("flip", "Flip", "false"), Bool("loop", "Loop", "true"), Integer("duration", "Frame Ms", "100"), Raw("frames", "Frames", "0 1"));
        yield return Define("attachCharacterToTempSprite", "Attach Character To Temp Sprite", "attachCharacterToTempSprite", Actor("actor", "Actor"));
        yield return DefineUnsafe("awardFestivalPrize", "Award Festival Prize", "awardFestivalPrize", Raw("args", "Prize", "rod", "Wiki values include pan, sculpture, rod, sword, hero, joja, slimeegg, emilyClothes, jukebox, or a qualified item ID."));
        yield return Define("beginSimultaneousCommand", "Begin Simultaneous Command", "beginSimultaneousCommand");
        yield return Define("broadcastEvent", "Broadcast Event", "broadcastEvent", Bool("useLocalFarmer", "Use Local Farmer", "", optional: true));
        yield return DefineUnsafe("catQuestion", "Cat Question", "catQuestion");
        yield return DefineUnsafe("cave", "Farm Cave Question", "cave");
        yield return Define("changeLocation", "Change Location", "changeLocation", Text("location", "Location", "Town"));
        yield return Define("changeMapTile", "Change Map Tile", "changeMapTile", Text("layer", "Layer", "Buildings"), Integer("x", "X", "0"), Integer("y", "Y", "0"), Integer("tileIndex", "Tile Index", "0"));
        yield return Define("changeName", "Change Name", "changeName", Actor("actor", "Actor"), Text("name", "Display Name", "Name"));
        yield return Define("changePortrait", "Change Portrait", "changePortrait", Actor("actor", "NPC"), Text("portrait", "Portrait", "", optional: true));
        yield return Define("changeSprite", "Change Sprite", "changeSprite", Actor("actor", "Actor"), Text("sprite", "Sprite", "", optional: true));
        yield return Define("changeToTemporaryMap", "Change To Temporary Map", "changeToTemporaryMap", Text("map", "Map", "Maps\\Town"), Bool("clamp", "Clamp", "", optional: true));
        yield return Define("changeYSourceRectOffset", "Change Y Source Rect Offset", "changeYSourceRectOffset", Actor("actor", "NPC"), Integer("offset", "Offset", "96"));
        yield return DefineUnsafe("characterSelect", "Character Select", "characterSelect");
        yield return Define("cutscene", "Cutscene", "cutscene", Text("cutscene", "Cutscene", "greenTea"));
        yield return Define("doAction", "Do Action", "doAction", Integer("x", "X", "0"), Integer("y", "Y", "0"));
        yield return Define("dump", "Dump Group", "dump", Choice("group", "Group", "girls", "girls", "guys"));
        yield return Define("elliotbooktalk", "Elliott Book Talk", "elliotbooktalk");
        yield return Define("emote", "Emote", "emote", Actor("actor", "Actor"), Integer("emote", "Emote ID", "8", "Wiki emotes: 4 empty can, 8 question mark, 12 angry, 16 exclamation, 20 heart, 24 sleep, 28 sad, 32 happy, 36 X, 40 pause, 52 video game, 56 music note, 60 blush."), Bool("continue", "Run next command immediately", "", optional: true));
        yield return Define("end", "End", "end", Raw("mode", "Mode / args", "", "Wiki modes: bed, beginGame, credits, dialogue <NPC> \"Text\", dialogueWarpOut <NPC> \"Text\", invisible <NPC>, invisibleWarpOut <NPC>, newDay, position <x> <y>, warpOut, wedding."));
        yield return Define("endSimultaneousCommand", "End Simultaneous Command", "endSimultaneousCommand");
        yield return Define("eventSeen", "Event Seen", "eventSeen", Text("eventId", "Event ID", "Example.Event"), Bool("seen", "Seen", "", optional: true));
        yield return Define("extendSourceRect", "Extend Source Rect", "extendSourceRect", Actor("actor", "Actor"), Raw("args", "reset or dimensions", "reset", "Wiki syntax: reset OR <horizontal> <vertical> [ignoreUpdates]."));
        yield return Define("eyes", "Eyes", "eyes", Integer("eyes", "Eyes", "0"), Integer("blink", "Blink", "-1000"));
        yield return Define("faceDirection", "Face Direction", "faceDirection", Actor("actor", "Actor"), Direction("direction", "Direction"), Bool("continue", "Continue", "", optional: true));
        yield return Define("fade", "Advanced Fade", "fade", Choice("mode", "Mode", "", "", "unfade"));
        yield return Define("farmerAnimation", "Farmer Animation", "farmerAnimation", Integer("anim", "Animation", "0"));
        yield return DefineUnsafe("farmerEat", "Farmer Eat", "farmerEat", Text("objectId", "Object ID", "216"));
        yield return Define("fork", "Fork", "fork", Raw("args", "Requirement / event ID", "OtherEventId", "Wiki syntax: fork [req] <event ID>. The new script omits the three start fields."));
        yield return Define("friendship", "Friendship", "friendship", Text("npc", "NPC", "Lewis"), Integer("amount", "Amount", "250"));
        yield return Define("globalFade", "Fade Out", "globalFade", Float("speed", "Speed", "", optional: true), Bool("continue", "Run next command immediately", "", optional: true));
        yield return Define("globalFadeToClear", "Fade In", "globalFadeToClear", Float("speed", "Speed", "", optional: true), Bool("continue", "Run next command immediately", "", optional: true));
        yield return Define("glow", "Glow", "glow", Integer("r", "R", "255"), Integer("g", "G", "255"), Integer("b", "B", "255"), Bool("hold", "Hold", "false"));
        yield return Define("grandpaCandles", "Grandpa Candles", "grandpaCandles");
        yield return Define("grandpaEvaluation", "Grandpa Evaluation", "grandpaEvaluation");
        yield return Define("grandpaEvaluation2", "Grandpa Evaluation 2", "grandpaEvaluation2");
        yield return Define("halt", "Halt", "halt");
        yield return Define("hideShadow", "Hide Shadow", "hideShadow", Actor("actor", "Actor"), Bool("hide", "Hide", "true"));
        yield return DefineUnsafe("hospitaldeath", "Hospital Death", "hospitaldeath");
        yield return Define("ignoreCollisions", "Ignore Collisions", "ignoreCollisions", Actor("actor", "Actor"));
        yield return Define("ignoreEventTileOffset", "Ignore Event Tile Offset", "ignoreEventTileOffset");
        yield return Define("ignoreMovementAnimation", "Ignore Movement Animation", "ignoreMovementAnimation", Actor("actor", "Actor"), Bool("ignore", "Ignore", "", optional: true));
        yield return Define("itemAboveHead", "Item Above Head", "itemAboveHead", Raw("args", "Item / show message", "(O)74 true", "Wiki syntax: [type|item id] [show message]. Types include pan, hero, sculpture, joja, slimeEgg, rod, sword, ore."));
        yield return Define("jump", "Jump", "jump", Actor("actor", "Actor"), OptionalInteger("intensity", "Intensity", "8"));
        yield return Define("loadActors", "Load Actors", "loadActors", Text("layer", "Layer", "Paths"));
        yield return Define("makeInvisible", "Make Invisible", "makeInvisible", Integer("x", "X", "0"), Integer("y", "Y", "0"), OptionalInteger("width", "Width"), OptionalInteger("height", "Height"));
        yield return Define("mail", "Mail Tomorrow", "mail", Text("letter", "Letter ID", "exampleLetter"));
        yield return Define("mailReceived", "Mail Received", "mailReceived", Text("letter", "Letter ID", "exampleLetter"), Bool("add", "Add", "", optional: true));
        yield return Define("mailToday", "Mail Today", "mailToday", Text("letter", "Letter ID", "exampleLetter"));
        yield return Define("message", "Message", "message", Text("text", "Text", "Hello."));
        yield return DefineUnsafe("minedeath", "Mine Death", "minedeath");
        yield return Define("money", "Money", "money", Integer("amount", "Amount", "500"));
        yield return Define("move", "Move", "move", Actor("actor", "Actor"), Integer("targetX", "Target X", "0"), Integer("targetY", "Target Y", "1"), Direction("direction", "Direction"), Bool("continue", "Run next command immediately", "", optional: true));
        yield return Define("pause", "Pause", "pause", Integer("duration", "Duration", "500"));
        yield return Define("playMusic", "Play Music", "playMusic", Text("track", "Track", "none"));
        yield return Define("playSound", "Play Sound", "playSound", Text("sound", "Sound", "coin"));
        yield return Define("playerControl", "Player Control", "playerControl");
        yield return Define("positionOffset", "Position Offset", "positionOffset", Actor("actor", "Actor"), Integer("x", "Pixels X", "0"), Integer("y", "Pixels Y", "0"), Bool("continue", "Run next command immediately", "", optional: true));
        yield return Define("proceedPosition", "Proceed Position", "proceedPosition", Actor("actor", "Actor"));
        yield return Define("question", "Question", "question", Text("question", "Question", "Question?"), OptionalInteger("forkAnswer", "Fork Answer Index", "", "Optional. Blank means no fork; 0 is the first answer, 1 is the second."), Answers("answers", "Answers", "Yes\nNo"));
        yield return Define("questionAnswered", "Question Answered", "questionAnswered", Text("answerId", "Answer ID", "answer"), Bool("answered", "Answered", "", optional: true));
        yield return Define("quickQuestion", "Quick Question", "quickQuestion", Raw("args", "Arguments", "\"Question?#Yes#No\"", "Wiki syntax: <question>#<answer1>#... followed by answer scripts separated by (break)."));
        yield return Define("removeItem", "Remove Item", "removeItem", Text("objectId", "Object ID", "(O)74"), OptionalInteger("count", "Count"));
        yield return Define("removeObject", "Remove Object", "removeObject", Integer("x", "X", "0"), Integer("y", "Y", "0"));
        yield return Define("removeQuest", "Remove Quest", "removeQuest", Text("questId", "Quest ID", "1"));
        yield return Define("removeSpecialOrder", "Remove Special Order", "removeSpecialOrder", Text("orderId", "Order ID", "ExampleOrder"));
        yield return Define("removeSprite", "Remove Sprite", "removeSprite", Integer("x", "X", "0"), Integer("y", "Y", "0"));
        yield return Define("removeTemporarySprites", "Remove Temporary Sprites", "removeTemporarySprites");
        yield return Define("removeTile", "Remove Tile", "removeTile", Integer("x", "X", "0"), Integer("y", "Y", "0"), Text("layer", "Layer", "Buildings"));
        yield return Define("replaceWithClone", "Replace With Clone", "replaceWithClone", Actor("actor", "NPC"));
        yield return Define("resetVariable", "Reset Variable", "resetVariable");
        yield return Define("rustyKey", "Rusty Key", "rustyKey");
        yield return Define("screenFlash", "Screen Flash", "screenFlash", Float("alpha", "Alpha", "1"));
        yield return Define("setRunning", "Set Running", "setRunning");
        yield return Define("setSkipActions", "Set Skip Actions", "setSkipActions", Raw("actions", "Actions", "", "Wiki syntax: [actions]. Multiple trigger actions are delimited with #. Example: AddCraftingRecipe Current \"Garden Pot\"#AddItem (BC)62."));
        yield return Define("shake", "Shake", "shake", Actor("actor", "Actor"), Integer("duration", "Duration", "1000"));
        yield return Define("showFrame", "Show Frame", "showFrame", Actor("actor", "Actor"), Integer("frame", "Frame", "0"), Bool("flip", "Flip", "", optional: true));
        yield return Define("skippable", "Skippable", "skippable");
        yield return Define("speak", "Speak", "speak", Actor("actor", "Actor"), Text("text", "Text", "Hello."));
        yield return Define("specificTemporarySprite", "Specific Temporary Sprite", "specificTemporarySprite", Text("sprite", "Sprite", "pennyMess"), Raw("args", "Parameters", "", "Wiki notes that parameters depend on the sprite and are hardcoded."));
        yield return Define("speed", "Speed", "speed", Actor("actor", "Actor"), Integer("speed", "Speed", "3"));
        yield return Define("splitSpeak", "Split Speak", "splitSpeak", Actor("actor", "Actor"), Text("text", "Text", "Option A~Option B"));
        yield return Define("startJittering", "Start Jittering", "startJittering");
        yield return Define("stopAdvancedMoves", "Stop Advanced Moves", "stopAdvancedMoves");
        yield return Define("stopAnimation", "Stop Animation", "stopAnimation", Actor("actor", "Actor"), OptionalInteger("endFrame", "End Frame"));
        yield return Define("stopGlowing", "Stop Glowing", "stopGlowing");
        yield return Define("stopJittering", "Stop Jittering", "stopJittering");
        yield return Define("stopMusic", "Stop Music", "stopMusic");
        yield return Define("stopRunning", "Stop Running", "stopRunning");
        yield return Define("stopSound", "Stop Sound", "stopSound", Text("sound", "Sound", "coin"), Bool("immediate", "Immediate", "", optional: true));
        yield return Define("stopSwimming", "Stop Swimming", "stopSwimming", Actor("actor", "Actor"));
        yield return Define("swimming", "Swimming", "swimming", Actor("actor", "Actor"));
        yield return Define("switchEvent", "Switch Event", "switchEvent", Text("eventId", "Event ID", "OtherEventId"));
        yield return Define("temporaryAnimatedSprite", "Temporary Animated Sprite", "temporaryAnimatedSprite", Raw("args", "Arguments", "LooseSprites\\Cursors 0 0 16 16 100 1 1 0 0 false false 0 0 4 0 0 0", "Wiki requires 18 fields: texture, source rect, interval, frames, loops, tile, flicker, flip, sort tile Y, alpha fade, scale, scale change, rotation, rotation change. Flags include color, hold_last_frame, ping_pong, motion."));
        yield return Define("temporarySprite", "Temporary Sprite", "temporarySprite", Integer("x", "X", "0"), Integer("y", "Y", "0"), Integer("row", "Row", "0"), Integer("length", "Length", "1"), Integer("interval", "Interval", "100"), Bool("flipped", "Flipped", "false"), Float("depth", "Layer Depth", "0"));
        yield return Define("textAboveHead", "Text Above Head", "textAboveHead", Actor("actor", "Actor"), Text("text", "Text", "Text"));
        yield return Define("tossConcession", "Toss Concession", "tossConcession", Actor("actor", "Actor"), Text("concessionId", "Concession ID", "popcorn"));
        yield return Define("translateName", "Translate Name", "translateName", Actor("actor", "Actor"), Text("translationKey", "Translation Key", "Strings\\StringsFromCSFiles:Name"));
        yield return DefineUnsafe("tutorialMenu", "Tutorial Menu", "tutorialMenu");
        yield return Define("updateMinigame", "Update Minigame", "updateMinigame", Raw("data", "Event Data", "", "Wiki syntax: updateMinigame <event data>."));
        yield return Define("viewport", "Viewport", "viewport", Raw("target", "Target / args", "farmer true", "Wiki forms: move <x> <y> <duration>; <actor> [clamp] [fade]; <x> <y> [clamp] [fade] [unfreeze]. Example: move 2 -1 5000."));
        yield return Define("waitForAllStationary", "Wait For All Stationary", "waitForAllStationary");
        yield return Define("waitForOtherPlayers", "Wait For Other Players", "waitForOtherPlayers");
        yield return Define("warp", "Warp", "warp", Actor("actor", "Actor"), Integer("x", "X", "0"), Integer("y", "Y", "0"), Bool("continue", "Run next command immediately", "", optional: true));
        yield return Define("warpFarmers", "Warp Farmers", "warpFarmers", Raw("args", "Arguments", "0 0 2 down 0 0 2", "Wiki syntax: repeated <x> <y> <direction> triplets, then <default offset> <default x> <default y> <direction>."));
    }

    private static EventCommandDefinition Define(string id, string displayName, string verb, params EventCommandParameter[] parameters)
    {
        return Define(id, displayName, verb, false, parameters);
    }

    private static EventCommandDefinition DefineUnsafe(string id, string displayName, string verb, params EventCommandParameter[] parameters)
    {
        return Define(id, displayName, verb, true, parameters);
    }

    private static EventCommandDefinition Define(string id, string displayName, string verb, bool unsafeForPreview, params EventCommandParameter[] parameters)
    {
        return new EventCommandDefinition
        {
            Id = "vanilla." + id,
            ProviderModId = ModId,
            ProviderName = ProviderName,
            DisplayName = displayName,
            Verb = verb,
            Badge = Badge,
            Parameters = parameters,
            UnsafeForPreview = unsafeForPreview
        };
    }

    private static EventCommandParameter Text(string key, string label, string defaultValue, bool optional = false)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Text, DefaultValue = defaultValue, Optional = optional };
    }

    private static EventCommandParameter Integer(string key, string label, string defaultValue, string hint = "")
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Integer, DefaultValue = defaultValue, Hint = hint };
    }

    private static EventCommandParameter OptionalInteger(string key, string label, string defaultValue = "", string hint = "")
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.OptionalInteger, DefaultValue = defaultValue, Optional = true, Hint = hint };
    }

    private static EventCommandParameter Float(string key, string label, string defaultValue, bool optional = false)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Float, DefaultValue = defaultValue, Optional = optional };
    }

    private static EventCommandParameter Bool(string key, string label, string defaultValue, bool optional = false)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Boolean, DefaultValue = defaultValue, Optional = optional };
    }

    private static EventCommandParameter Choice(string key, string label, string defaultValue, params string[] choices)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Choice, DefaultValue = defaultValue, Choices = choices };
    }

    private static EventCommandParameter Direction(string key, string label)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Direction, DefaultValue = "2", Choices = new[] { "0", "1", "2", "3" } };
    }

    private static EventCommandParameter Actor(string key, string label)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Actor, DefaultValue = "farmer" };
    }

    private static EventCommandParameter Raw(string key, string label, string defaultValue = "", string hint = "")
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.RawArguments, DefaultValue = defaultValue, QuoteWhenNeeded = false, TextLimit = 300, Optional = true, Hint = hint };
    }

    private static EventCommandParameter Answers(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.AnswerList, DefaultValue = defaultValue, TextLimit = 120 };
    }
}
