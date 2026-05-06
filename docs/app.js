const defaultSourcePath = "fishes.json";
const handleDbName = "tof-editor";
const handleStoreName = "handles";
const handleKey = "last-source";
const snapshotKey = "last-source-snapshot";
const openPickerId = "tof-fishes-json";
const localSnapshotKey = "tof-last-source-snapshot";
const localSnapshotNameKey = "tof-last-source-name";
const ObjectSpriteWidth = 32;
const ObjectSpriteHeight = 32;
const AquariumSpriteWidth = 24;
const AquariumSpriteHeight = 24;
const SpriteEditorCellSize = 12;
const ObjectSpriteEditorCanvasWidth = ObjectSpriteWidth * SpriteEditorCellSize;
const ObjectSpriteEditorCanvasHeight = ObjectSpriteHeight * SpriteEditorCellSize;
const ObjectSpriteAtlasTileWidth = 32;
const ObjectSpriteAtlasTileHeight = 32;
const AquariumSpriteAtlasTileWidth = 24;
const AquariumSpriteAtlasTileHeight = 48;
const AquariumAtlasColumns = 12;
const commonContextTags = [
  "alcohol_item",
  "algae_item",
  "ancient_item",
  "beach_item",
  "bomb_item",
  "bone_item",
  "book_item",
  "campfire_item",
  "ceramic_item",
  "chicken_item",
  "color_black",
  "color_blue",
  "color_brown",
  "color_dark_gray",
  "color_gray",
  "color_green",
  "color_iron",
  "color_orange",
  "color_pink",
  "color_prismatic",
  "color_purple",
  "color_red",
  "color_silver",
  "color_white",
  "color_yellow",
  "cooking_item",
  "cow_milk_item",
  "cowboy_item",
  "crop_year_2",
  "crystalarium_banned",
  "crow_scare",
  "dinosaur_item",
  "doll_item",
  "drink_item",
  "dwarvish_item",
  "dye_medium",
  "dye_strong",
  "egg_item",
  "elvish_item",
  "fertilizer_item",
  "fish_bug_lair",
  "fish_carnivorous",
  "fish_crab_pot",
  "fish_desert",
  "fish_freshwater",
  "fish_has_roe",
  "fish_lake",
  "fish_legendary",
  "fish_legendary_family",
  "fish_mines",
  "fish_night_market",
  "fish_nonfish",
  "fish_ocean",
  "fish_pond_ignore",
  "fish_river",
  "fish_secret_pond",
  "fish_semi_rare",
  "fish_sewers",
  "fish_swamp",
  "fish_talk_demanding",
  "fish_talk_rude",
  "fish_talk_stiff",
  "fish_upright",
  "flower_item",
  "food_bakery",
  "food_breakfast",
  "food_cake",
  "food_party",
  "food_pasta",
  "food_salad",
  "food_sauce",
  "food_seafood",
  "food_soup",
  "food_spicy",
  "food_sushi",
  "food_sweet",
  "forage_item",
  "forage_item_beach",
  "forage_item_cave",
  "forage_item_desert",
  "forage_item_mines",
  "forage_item_secret",
  "fossil_item",
  "fruit_item",
  "fruit_tree_item",
  "furnace_item",
  "geode",
  "geode_crusher_ignored",
  "ginger_item",
  "goat_milk_item",
  "golden_relic_item",
  "honey_item",
  "hunting_item",
  "instrument_item",
  "is_machine",
  "jelly_item",
  "juice_item",
  "keg_juice",
  "keg_wine",
  "large_egg_item",
  "large_milk_item",
  "light_source",
  "machine_input",
  "machine_output",
  "machine_item",
  "marine_item",
  "mayo_item",
  "medicine_item",
  "milk_item",
  "museum_donatable",
  "noble_item",
  "not_giftable",
  "not_museum_donatable",
  "not_placeable",
  "ore_item",
  "pickle_item",
  "placeable",
  "potion_item",
  "prehistoric_item",
  "preserves_jelly",
  "preserves_pickle",
  "prevent_loss_on_death",
  "quality_fertilizer_item",
  "quality_gold",
  "quality_iridium",
  "quality_none",
  "quality_qi",
  "quality_silver",
  "scroll_item",
  "season_all",
  "season_fall",
  "season_spring",
  "season_summer",
  "season_winter",
  "seedmaker_banned",
  "sign_item",
  "slime_egg_item",
  "slime_item",
  "statue_item",
  "strange_doll_1",
  "strange_doll_2",
  "syrup_item",
  "tapper_item",
  "torch_item",
  "totem_item",
  "toy_item",
  "trash_item",
  "tree_seed_item",
  "wine_item",
  "wood_item"
];

const state = {
  source: null,
  selectedIndex: 0,
  sourceHandle: null,
  activeLocationInput: null,
  sourceName: "fishes.json",
  spriteMode: "item",
  aquariumFrameIndex: 0,
  spriteTool: "paint",
  spriteBackground: "light",
  spriteDraft: createBlankSpritePixels(ObjectSpriteWidth, ObjectSpriteHeight),
  spriteDraftToken: "",
  spriteUndoStack: [],
  spriteMoveScaleMode: false,
  spriteTransformMode: false,
  spriteTransformAction: null,
  spriteTransformBasePixels: null,
  spriteTransformBounds: null,
  spriteTransformStartPoint: null,
  spriteRotationBasePixels: null,
  spriteRotationStartAngle: 0
};

const vanillaObjectNames = window.TOF_VANILLA_OBJECT_NAMES ?? {};
const vanillaObjectIdsByName = Object.fromEntries(
  Object.entries(vanillaObjectNames).map(([id, name]) => [normalizeObjectLookupName(name), id])
);

const pondProductCatalog = Object.keys(vanillaObjectNames).map(id => `(O)${id}`);

const locationLibrary = [
  { name: "Beach", source: "Vanilla", note: "Ocean shore" },
  { name: "Mountain", source: "Vanilla", note: "Mountain lake" },
  { name: "Forest", source: "Vanilla", note: "Cindersap river and pond areas" },
  { name: "Backwoods", source: "Vanilla", note: "Backwoods river" },
  { name: "Town", source: "Vanilla", note: "Pelican Town river" },
  { name: "Railroad", source: "Vanilla", note: "Railroad lake" },
  { name: "Desert", source: "Vanilla", note: "Calico Desert pond" },
  { name: "Sewer", source: "Vanilla", note: "The Sewers" },
  { name: "Mine", source: "Vanilla", note: "Mine floors with water" },
  { name: "IslandSouth", source: "Vanilla", note: "Ginger Island south ocean" },
  { name: "IslandWest", source: "Vanilla", note: "Island west freshwater" },
  { name: "IslandNorth", source: "Vanilla", note: "Island north river area" }
];

const presets = {
  easyLake: {
    price: 90,
    edibility: 12,
    contextTags: ["fish_has_roe"],
    catch: {
      difficulty: 35,
      behavior: "smooth",
      minSize: 8,
      maxSize: 28,
      timeStart: 600,
      timeEnd: 2200,
      seasons: ["spring", "summer"],
      weather: "both",
      waterCode: "682 .2",
      maxDepth: 1,
      baseChance: 0.28,
      depthMultiplier: 0.1,
      minFishingLevel: 0
    },
    locations: [
      { location: "Mountain", chance: 0.2, minDistanceFromShore: 1, maxDistanceFromShore: -1, precedence: 0 },
      { location: "Forest", chance: 0.14, minDistanceFromShore: 1, maxDistanceFromShore: -1, precedence: 0 }
    ],
    pond: {
      spawnTime: -1,
      waterColor: "190 220 235",
      waterMinPopulation: 2,
      products: [{ requiredPopulation: 0, chance: 0.5, itemId: "(O)812", minQuantity: 1, maxQuantity: 1 }],
      populationGates: { "4": [], "8": [] }
    }
  },
  rareOcean: {
    price: 240,
    edibility: 20,
    contextTags: ["fish_has_roe"],
    catch: {
      difficulty: 88,
      behavior: "mixed",
      minSize: 20,
      maxSize: 60,
      timeStart: 900,
      timeEnd: 2600,
      seasons: ["spring", "fall"],
      weather: "rainy",
      waterCode: "685 .35",
      maxDepth: 3,
      baseChance: 0.08,
      depthMultiplier: 0.25,
      minFishingLevel: 5
    },
    locations: [
      { location: "Beach", chance: 0.09, minDistanceFromShore: 3, maxDistanceFromShore: -1, precedence: 0 },
      { location: "IslandSouth", chance: 0.06, minDistanceFromShore: 3, maxDistanceFromShore: -1, precedence: 0 }
    ],
    pond: {
      spawnTime: -1,
      waterColor: "120 160 205",
      waterMinPopulation: 2,
      products: [
        { requiredPopulation: 0, chance: 0.5, itemId: "(O)812", minQuantity: 1, maxQuantity: 1 },
        { requiredPopulation: 5, chance: 0.08, itemId: "(O)797", minQuantity: 1, maxQuantity: 1 }
      ],
      populationGates: { "4": ["(O)334 2"], "8": ["(O)336 1"] }
    }
  },
  nightEel: {
    price: 180,
    edibility: 18,
    contextTags: ["fish_has_roe"],
    catch: {
      difficulty: 75,
      behavior: "sinker",
      minSize: 16,
      maxSize: 42,
      timeStart: 1800,
      timeEnd: 2600,
      seasons: ["fall", "winter"],
      weather: "both",
      waterCode: "685 .35",
      maxDepth: 2,
      baseChance: 0.13,
      depthMultiplier: 0.2,
      minFishingLevel: 3
    },
    locations: [
      { location: "Beach", chance: 0.12, minDistanceFromShore: 2, maxDistanceFromShore: -1, precedence: 0 }
    ],
    pond: {
      spawnTime: -1,
      waterColor: "110 140 180",
      waterMinPopulation: 2,
      products: [
        { requiredPopulation: 0, chance: 0.5, itemId: "(O)812", minQuantity: 1, maxQuantity: 1 },
        { requiredPopulation: 4, chance: 0.15, itemId: "(O)338", minQuantity: 1, maxQuantity: 1 }
      ],
      populationGates: { "4": ["(O)685 5"], "8": ["(O)336 1"] }
    }
  },
  rainRiver: {
    price: 140,
    edibility: 15,
    contextTags: ["fish_has_roe"],
    catch: {
      difficulty: 58,
      behavior: "mixed",
      minSize: 10,
      maxSize: 34,
      timeStart: 600,
      timeEnd: 2400,
      seasons: ["spring", "summer", "fall"],
      weather: "rainy",
      waterCode: "682 .2",
      maxDepth: 2,
      baseChance: 0.17,
      depthMultiplier: 0.18,
      minFishingLevel: 1
    },
    locations: [
      { location: "Town", chance: 0.15, minDistanceFromShore: 1, maxDistanceFromShore: -1, precedence: 0 },
      { location: "Forest", chance: 0.15, minDistanceFromShore: 1, maxDistanceFromShore: -1, precedence: 0 },
      { location: "Backwoods", chance: 0.12, minDistanceFromShore: 1, maxDistanceFromShore: -1, precedence: 0 }
    ],
    pond: {
      spawnTime: -1,
      waterColor: "145 185 195",
      waterMinPopulation: 2,
      products: [
        { requiredPopulation: 0, chance: 0.5, itemId: "(O)812", minQuantity: 1, maxQuantity: 1 },
        { requiredPopulation: 4, chance: 0.1, itemId: "(O)684", minQuantity: 1, maxQuantity: 2 }
      ],
      populationGates: { "4": ["(O)685 10"], "8": ["(O)334 1"] }
    }
  },
  pondFriendly: {
    price: 110,
    edibility: 13,
    contextTags: ["fish_has_roe"],
    catch: {
      difficulty: 42,
      behavior: "mixed",
      minSize: 8,
      maxSize: 26,
      timeStart: 600,
      timeEnd: 2600,
      seasons: ["spring", "summer", "fall", "winter"],
      weather: "both",
      waterCode: "682 .2",
      maxDepth: 1,
      baseChance: 0.18,
      depthMultiplier: 0.1,
      minFishingLevel: 0
    },
    locations: [
      { location: "Mountain", chance: 0.14, minDistanceFromShore: 1, maxDistanceFromShore: -1, precedence: 0 }
    ],
    pond: {
      spawnTime: -1,
      waterColor: "180 220 210",
      waterMinPopulation: 1,
      products: [
        { requiredPopulation: 0, chance: 0.55, itemId: "(O)812", minQuantity: 1, maxQuantity: 1 },
        { requiredPopulation: 5, chance: 0.12, itemId: "(O)392", minQuantity: 1, maxQuantity: 2 }
      ],
      populationGates: { "4": ["(O)388 25"], "8": ["(O)390 10"] }
    }
  }
};

const ids = [
  "packName",
  "author",
  "version",
  "description",
  "uniqueId",
  "minimumApiVersion",
  "pondPrefix",
  "objectsTextureFile",
  "objectsTextureTarget",
  "slug",
  "displayName",
  "fishDescription",
  "spriteIndex",
  "price",
  "edibility",
  "enabledByDefault",
  "isLegendary",
  "contextTags",
  "difficulty",
  "behavior",
  "minSize",
  "maxSize",
  "timeStart",
  "timeEnd",
  "weather",
  "waterCode",
  "maxDepth",
  "baseChance",
  "depthMultiplier",
  "minFishingLevel",
  "pondSpawnTime",
  "pondWaterColor",
  "pondWaterColorPicker",
  "pondWaterColorPreview",
  "pondWaterMinPopulation",
  "edibilityEstimate",
  "priceEstimate",
  "spriteEditorLabel",
  "spriteAtlasMeta",
  "aquariumEnabled",
  "aquariumType",
  "aquariumField3",
  "aquariumField4",
  "aquariumField5",
  "aquariumField6",
  "aquariumStatus",
  "aquariumTypeHint",
  "aquariumFieldGuide",
  "aquariumEntryPreview",
  "spriteColor",
  "manifestPreview",
  "configPreview",
  "contentPreview",
  "i18nPreview",
  "dataPreview",
  "validationPreview"
];

const $ = Object.fromEntries(ids.map(id => [id, document.getElementById(id)]));
const fishList = document.getElementById("fish-list");
const locationsTable = document.getElementById("locations-table");
const productsTable = document.getElementById("products-table");
const locationTemplate = document.getElementById("location-row-template");
const productTemplate = document.getElementById("product-row-template");
const gateTemplate = document.getElementById("gate-row-template");
const seasonCheckboxes = [...document.querySelectorAll("[data-season]")];
const locationLibrarySearch = document.getElementById("locationLibrarySearch");
const locationLibraryList = document.getElementById("locationLibraryList");
const presetButtons = [...document.querySelectorAll("[data-preset]")];
const gate4Table = document.getElementById("gate4Table");
const gate8Table = document.getElementById("gate8Table");
const contextTagButtons = document.getElementById("contextTagButtons");
const spritePanel = document.querySelector(".sprite-panel");
const spriteEditorGrid = document.getElementById("spriteEditorGrid");
const spriteEditorCanvas = document.getElementById("spriteEditorCanvas");
const spritePreviewCanvas = document.getElementById("spritePreviewCanvas");
const spritePaintToolButton = document.getElementById("spritePaintTool");
const spriteEraseToolButton = document.getElementById("spriteEraseTool");
const spritePickColorToolButton = document.getElementById("spritePickColorTool");
const spriteDodgeToolButton = document.getElementById("spriteDodgeTool");
const spriteBurnToolButton = document.getElementById("spriteBurnTool");
const spriteBackgroundToggleButton = document.getElementById("spriteBackgroundToggle");
const spriteTransformMoveScaleButton = document.getElementById("spriteTransformMoveScale");
const spriteTransformRotateButton = document.getElementById("spriteTransformRotate");
const spritePalette = document.getElementById("spritePalette");
const spriteEditItemButton = document.getElementById("spriteEditItem");
const spriteEditAquariumButton = document.getElementById("spriteEditAquarium");
const aquariumControls = document.getElementById("aquariumControls");
const aquariumFrameList = document.getElementById("aquariumFrameList");

const aquariumTypeDocs = {
  fish: {
    hint: "Standard fish movement. Use this for most normal swimming fish.",
    guide: "For simple fish, it is normal for fields 3-6 to stay blank. Add local frame sequences only when you specifically want animated aquarium behavior."
  },
  eel: {
    hint: "Long eel-style movement.",
    guide: "Eel entries are commonly simpler. Start with blank fields and only add local frame refs if you know the sequence you want."
  },
  float: {
    hint: "Floating / bobbing aquarium movement.",
    guide: "Float entries often use several local frame sequences. Enter only local frame numbers like 0 1 1 and let the editor convert them to exported atlas indexes."
  },
  ground: {
    hint: "Bottom-dwelling movement near the tank floor.",
    guide: "Ground entries commonly use a few local frame sequences and may leave later fields blank."
  },
  static: {
    hint: "Static aquarium display with no real frame sequence.",
    guide: "Static entries usually leave fields 3-6 blank."
  },
  crawl: {
    hint: "Crawling motion for starfish, snails, and similar creatures.",
    guide: "Crawl entries often use several sequence fields. Use local frame numbers only and preview the final raw entry below."
  },
  front_crawl: {
    hint: "Front-facing crawl pattern for crabs and similar creatures.",
    guide: "Front-crawl entries often use multiple local frame sequences. Keep the frame strip in the order you want to reference."
  },
  cephalopod: {
    hint: "Cephalopod pattern for squid, jellyfish, and octopus-like movement.",
    guide: "Cephalopod entries usually use several short local frame sequences across fields 3-6."
  }
};

document.getElementById("add-fish").addEventListener("click", addFish);
document.getElementById("duplicate-fish").addEventListener("click", duplicateFish);
document.getElementById("delete-fish").addEventListener("click", deleteFish);
document.getElementById("add-location").addEventListener("click", addLocation);
document.getElementById("add-product").addEventListener("click", addProduct);
document.getElementById("add-gate4").addEventListener("click", () => addGateItem("4"));
document.getElementById("add-gate8").addEventListener("click", () => addGateItem("8"));
document.getElementById("refresh-output").addEventListener("click", refreshOutputs);
document.getElementById("open-source").addEventListener("click", openSourceFile);
document.getElementById("save-source").addEventListener("click", saveSourceFile);
document.getElementById("download-source").addEventListener("click", () => downloadText("fishes.json", stringify(state.source)));
document.getElementById("download-manifest").addEventListener("click", () => downloadGenerated("manifest.json"));
document.getElementById("download-content").addEventListener("click", () => downloadGenerated("content.json"));
document.getElementById("download-config").addEventListener("click", () => downloadGenerated("config.json"));
document.getElementById("download-i18n").addEventListener("click", () => downloadGenerated("default.json", "i18n/default.json"));
document.getElementById("download-current-fish-data").addEventListener("click", downloadCurrentFishDataFile);
document.getElementById("save-sprite").addEventListener("click", saveSpriteDraft);
document.getElementById("save-swatch").addEventListener("click", saveCurrentSpriteColorToPalette);
document.getElementById("download-atlas").addEventListener("click", downloadAtlas);
document.getElementById("download-atlas-output").addEventListener("click", downloadObjectAtlas);
document.getElementById("download-aquarium-atlas-output").addEventListener("click", downloadAquariumAtlas);
spritePaintToolButton.addEventListener("click", () => setSpriteTool("paint"));
spriteEraseToolButton.addEventListener("click", () => setSpriteTool("erase"));
spritePickColorToolButton.addEventListener("click", () => setSpriteTool("pick"));
spriteDodgeToolButton.addEventListener("click", () => setSpriteTool("dodge"));
spriteBurnToolButton.addEventListener("click", () => setSpriteTool("burn"));
spriteBackgroundToggleButton.addEventListener("click", toggleSpriteBackground);
spriteTransformMoveScaleButton.addEventListener("click", toggleSpriteMoveScaleMode);
spriteTransformRotateButton.addEventListener("click", toggleSpriteTransformMode);
spriteEditItemButton.addEventListener("click", () => setSpriteMode("item"));
spriteEditAquariumButton.addEventListener("click", () => setSpriteMode("aquarium"));
document.getElementById("mirrorSprite").addEventListener("click", mirrorSpriteDraft);
document.getElementById("addAquariumFrame").addEventListener("click", addAquariumFrame);
document.getElementById("duplicateAquariumFrame").addEventListener("click", duplicateAquariumFrame);
document.getElementById("removeAquariumFrame").addEventListener("click", removeAquariumFrame);
document.getElementById("autoFillAquariumMapping").addEventListener("click", autoFillAquariumMapping);
$.spriteColor.addEventListener("input", renderSpritePalette);
$.spriteColor.addEventListener("change", renderSpritePalette);
document.getElementById("write-pack").addEventListener("click", writePackFiles);
locationLibrarySearch.addEventListener("input", renderLocationLibrary);
presetButtons.forEach(button => {
  button.addEventListener("click", () => applyPreset(button.dataset.preset));
});
renderContextTagButtons();

[
  "packName",
  "author",
  "version",
  "description",
  "uniqueId",
  "minimumApiVersion",
  "pondPrefix",
  "objectsTextureFile",
  "objectsTextureTarget"
].forEach(bindMetaField);

[
  "slug",
  "displayName",
  "fishDescription",
  "spriteIndex",
  "price",
  "edibility",
  "enabledByDefault",
  "isLegendary",
  "contextTags",
  "difficulty",
  "behavior",
  "minSize",
  "maxSize",
  "timeStart",
  "timeEnd",
  "weather",
  "waterCode",
  "maxDepth",
  "baseChance",
  "depthMultiplier",
  "minFishingLevel",
  "pondSpawnTime",
  "pondWaterMinPopulation"
].forEach(bindFishField);

bindPondColorFields();
bindSpriteEditor();
bindAquariumFields();

bootstrap();

async function bootstrap() {
  const restored = await tryRestoreSourceHandle();
  if (restored) {
    renderAll();
    return;
  }

  const restoredSnapshot = tryRestoreLocalSnapshot();
  if (restoredSnapshot) {
    renderAll();
    return;
  }

  const restoredDbSnapshot = await tryRestoreIndexedDbSnapshot();
  if (restoredDbSnapshot) {
    renderAll();
    return;
  }

  try {
    const response = await fetch(defaultSourcePath);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    state.source = normalizeSource(await response.json());
    state.sourceName = "fishes.json";
  } catch (error) {
    state.source = createBlankSource();
    state.sourceName = "fishes.json";
    alert(`Could not auto-load fishes.json (${error.message}). If you opened this page directly from disk, that is normal. Click "Open fishes.json" to load your source file. A blank editor was opened instead.`);
  }

  renderAll();
}

function createBlankSource() {
  return normalizeSource({
    packName: "TOF",
    author: "Kree",
    version: "1.0.0",
    description: "",
    uniqueId: "Kree.TOF",
    minimumApiVersion: "4.0.0",
    pondPrefix: "tof",
    objectsTextureFile: "assets/fishes.png",
    objectsTextureTarget: "Mods/Kree.TOF/Objects",
    spritePalette: [],
    fish: [createBlankFish()]
  });
}

function createBlankFish() {
  return {
    slug: "new_fish",
    enabledByDefault: true,
    displayName: "New Fish",
    description: "",
    spriteIndex: 0,
    price: 100,
    edibility: 10,
    isLegendary: false,
    contextTags: ["fish_has_roe"],
    spritePixels: createBlankSpritePixels(ObjectSpriteWidth, ObjectSpriteHeight),
    aquarium: createBlankAquariumData(),
    catch: {
      difficulty: 40,
      behavior: "mixed",
      minSize: 5,
      maxSize: 20,
      timeStart: 600,
      timeEnd: 2600,
      seasons: ["spring"],
      weather: "both",
      waterCode: "682 .2",
      maxDepth: 1,
      baseChance: 0.2,
      depthMultiplier: 0.15,
      minFishingLevel: 0
    },
    locations: [
      {
        location: "Mountain",
        chance: 0.15,
        minDistanceFromShore: 1,
        maxDistanceFromShore: -1,
        precedence: 0
      }
    ],
    pond: {
      spawnTime: -1,
      waterColor: "200 200 255",
      waterMinPopulation: 2,
      products: [
        {
          requiredPopulation: 0,
          chance: 0.5,
          itemId: "(O)812",
          minQuantity: 1,
          maxQuantity: 1
        }
      ],
      populationGates: {
        "4": [],
        "8": []
      }
    }
  };
}

function getNextSpriteIndex() {
  if (!state.source?.fish?.length) {
    return 0;
  }

  return Math.max(...state.source.fish.map(fish => Math.max(0, readNumericInput(fish.spriteIndex)))) + 1;
}

function createBlankSpritePixels(width = ObjectSpriteWidth, height = ObjectSpriteHeight) {
  return Array.from({ length: width * height }, () => null);
}

function createBlankAquariumData() {
  return {
    enabled: true,
    slotIndex: null,
    type: "fish",
    field3: "",
    field4: "",
    field5: "",
    field6: "",
    frames: [createBlankSpritePixels(AquariumSpriteWidth, AquariumSpriteHeight)]
  };
}

function normalizeSource(source) {
  const normalized = structuredClone(source ?? {});
  normalized.packName ??= "TOF";
  normalized.author ??= "Kree";
  normalized.version ??= "1.0.0";
  normalized.description ??= "";
  normalized.uniqueId ??= "Kree.TOF";
  normalized.minimumApiVersion ??= "4.0.0";
  normalized.pondPrefix ??= "tof";
  normalized.objectsTextureFile ??= "assets/fishes.png";
  normalized.objectsTextureTarget ??= "Mods/Kree.TOF/Objects";
  normalized.spritePalette = normalizeSpritePalette(normalized.spritePalette);
  normalized.fish = Array.isArray(normalized.fish) && normalized.fish.length
    ? normalized.fish.map(normalizeFish)
    : [createBlankFish()];
  ensureAquariumSlotIndexes(normalized.fish);
  return normalized;
}

function normalizeFish(fish) {
  const normalized = structuredClone(fish ?? createBlankFish());
  normalized.slug ??= "new_fish";
  normalized.enabledByDefault ??= true;
  normalized.displayName ??= "New Fish";
  normalized.description ??= "";
    normalized.spriteIndex = readNumericInput(normalized.spriteIndex ?? 0);
    normalized.price = readNumericInput(normalized.price ?? 100);
    normalized.edibility = readNumericInput(normalized.edibility ?? 10);
    normalized.isLegendary = !!normalized.isLegendary;
    normalized.contextTags = normalizeFishContextTags(Array.isArray(normalized.contextTags) ? normalized.contextTags : ["fish_has_roe"]);
  normalized.spritePixels = normalizeSpritePixels(normalized.spritePixels, ObjectSpriteWidth, ObjectSpriteHeight);
  normalized.aquarium = normalizeAquariumData(normalized.aquarium);
  normalized.catch = { ...createBlankFish().catch, ...(normalized.catch ?? {}) };
  normalized.pond = {
    ...createBlankFish().pond,
    ...(normalized.pond ?? {}),
    products: Array.isArray(normalized.pond?.products)
      ? sortPondProducts(normalized.pond.products.map(normalizePondProduct))
      : createBlankFish().pond.products,
    populationGates: {
      "4": Array.isArray(normalized.pond?.populationGates?.["4"]) ? normalized.pond.populationGates["4"] : [],
      "8": Array.isArray(normalized.pond?.populationGates?.["8"]) ? normalized.pond.populationGates["8"] : []
    }
  };
  normalized.locations = Array.isArray(normalized.locations) && normalized.locations.length
    ? normalized.locations
    : structuredClone(createBlankFish().locations);
  return normalized;
}

function normalizeAquariumData(value) {
  const normalized = { ...createBlankAquariumData(), ...(value ?? {}) };
  normalized.enabled = value && Object.hasOwn(value, "enabled") ? !!normalized.enabled : true;
  const rawSlotIndex = Number.parseInt(normalized.slotIndex, 10);
  normalized.slotIndex = Number.isFinite(rawSlotIndex) && rawSlotIndex >= 0 ? rawSlotIndex : null;
  normalized.type = String(normalized.type ?? "fish").trim() || "fish";
  normalized.field3 = String(normalized.field3 ?? "").trim();
  normalized.field4 = String(normalized.field4 ?? "").trim();
  normalized.field5 = String(normalized.field5 ?? "").trim();
  normalized.field6 = String(normalized.field6 ?? "").trim();
  normalized.frames = Array.isArray(normalized.frames) && normalized.frames.length
    ? normalized.frames.map(frame => normalizeAquariumFramePixels(frame))
    : createBlankAquariumData().frames;
  return normalized;
}

function getAquariumFrameCount(aquarium) {
  return Math.max(1, normalizeAquariumData(aquarium).frames.length);
}

function rangesOverlap(startA, countA, startB, countB) {
  const endA = startA + countA;
  const endB = startB + countB;
  return startA < endB && startB < endA;
}

function ensureAquariumSlotIndexes(fishList) {
  let nextIndex = 0;
  const assigned = [];

  for (const fish of fishList ?? []) {
    const aquarium = normalizeAquariumData(fish.aquarium);
    const frameCount = getAquariumFrameCount(aquarium);
    let startIndex = aquarium.slotIndex;
    const overlapsExisting = Number.isInteger(startIndex) && assigned.some(entry =>
      rangesOverlap(startIndex, frameCount, entry.startIndex, entry.frameCount)
    );

    if (!Number.isInteger(startIndex) || startIndex < 0 || overlapsExisting) {
      startIndex = nextIndex;
    }

    aquarium.slotIndex = startIndex;
    fish.aquarium = aquarium;
    assigned.push({ startIndex, frameCount });
    nextIndex = Math.max(nextIndex, startIndex + frameCount);
  }
}

function getNextAquariumSlotIndex() {
  ensureAquariumSlotIndexes(state.source?.fish ?? []);
  if (!state.source?.fish?.length) {
    return 0;
  }

  return Math.max(
    0,
    ...state.source.fish.map(fish => {
      const aquarium = normalizeAquariumData(fish.aquarium);
      return (aquarium.slotIndex ?? 0) + getAquariumFrameCount(aquarium);
    })
  );
}

function normalizeAquariumFramePixels(value) {
  if (Array.isArray(value) && value.length === AquariumSpriteWidth * AquariumSpriteHeight) {
    return normalizeSpritePixels(value, AquariumSpriteWidth, AquariumSpriteHeight);
  }

  // Migrate older TOF aquarium drafts that were authored at 32x32.
  if (Array.isArray(value) && value.length === ObjectSpriteWidth * ObjectSpriteHeight) {
    return resizePixelArray(
      value.map(pixel => normalizeSpriteColor(pixel)),
      ObjectSpriteWidth,
      ObjectSpriteHeight,
      AquariumSpriteWidth,
      AquariumSpriteHeight
    );
  }

  return createBlankSpritePixels(AquariumSpriteWidth, AquariumSpriteHeight);
}

function normalizeSpritePixels(value, width = ObjectSpriteWidth, height = ObjectSpriteHeight) {
  if (!Array.isArray(value) || value.length !== width * height) {
    return createBlankSpritePixels(width, height);
  }

  return value.map(pixel => normalizeSpriteColor(pixel));
}

function resizePixelArray(sourcePixels, sourceWidth, sourceHeight, targetWidth, targetHeight) {
  const resized = createBlankSpritePixels(targetWidth, targetHeight);
  for (let y = 0; y < targetHeight; y++) {
    for (let x = 0; x < targetWidth; x++) {
      const sourceX = Math.min(sourceWidth - 1, Math.floor((x / targetWidth) * sourceWidth));
      const sourceY = Math.min(sourceHeight - 1, Math.floor((y / targetHeight) * sourceHeight));
      resized[y * targetWidth + x] = sourcePixels[sourceY * sourceWidth + sourceX] ?? null;
    }
  }

  return resized;
}

function normalizeFishContextTags(value) {
  if (!Array.isArray(value)) {
    return ["fish_has_roe"];
  }

  return [...new Set(
    value
      .map(tag => String(tag ?? "").trim())
      .filter(tag => tag.length > 0 && tag !== "fish_pond")
  )];
}

function normalizeSpriteColor(value) {
  if (!value) {
    return null;
  }

  const normalized = String(value).trim();
  return /^#[0-9a-f]{6}$/i.test(normalized) ? normalized.toLowerCase() : null;
}

function normalizeSpritePalette(value) {
  if (!Array.isArray(value)) {
    return [];
  }

  const unique = [];
  for (const entry of value) {
    const color = normalizeSpriteColor(entry);
    if (!color || unique.includes(color)) {
      continue;
    }

    unique.push(color);
    if (unique.length >= 32) {
      break;
    }
  }

  return unique;
}

function normalizeObjectLookupName(value) {
  return String(value ?? "").trim().toLowerCase();
}

function normalizeObjectItemQuery(value, fallback = "(O)812") {
  const text = String(value ?? "").trim();
  if (!text) {
    return fallback;
  }

  const qualifiedMatch = text.match(/^\(([^)]+)\)(.+)$/i);
  if (qualifiedMatch) {
    const [, rawType, rawId] = qualifiedMatch;
    if (/^(?:O|0)$/i.test(rawType.trim()) && rawId.trim()) {
      return `(O)${rawId.trim()}`;
    }

    return text;
  }

  if (Object.hasOwn(vanillaObjectNames, text)) {
    return `(O)${text}`;
  }

  const byNameId = vanillaObjectIdsByName[normalizeObjectLookupName(text)];
  if (byNameId) {
    return `(O)${byNameId}`;
  }

  return text;
}

function normalizePondProduct(product) {
  return {
    requiredPopulation: readNumericInput(product?.requiredPopulation ?? 0),
    chance: readFloatInput(product?.chance ?? 0.5),
    itemId: normalizeObjectItemQuery(product?.itemId ?? "(O)812"),
    minQuantity: Math.max(1, readNumericInput(product?.minQuantity ?? 1)),
    maxQuantity: Math.max(1, readNumericInput(product?.maxQuantity ?? 1))
  };
}

function sortPondProducts(products) {
  return [...(products ?? [])].sort((left, right) =>
    readNumericInput(right?.requiredPopulation ?? 0) - readNumericInput(left?.requiredPopulation ?? 0)
  );
}

function renderAll() {
  clampSelection();
  renderMeta();
  renderFishList();
  renderFishEditor();
  renderLocationLibrary();
  refreshOutputs();
  persistSnapshots();
}

function clampSelection() {
  if (!state.source.fish.length) {
    state.source.fish.push(createBlankFish());
  }

  if (state.selectedIndex < 0) {
    state.selectedIndex = 0;
  }

  if (state.selectedIndex >= state.source.fish.length) {
    state.selectedIndex = state.source.fish.length - 1;
  }
}

function getCurrentFish() {
  clampSelection();
  return state.source.fish[state.selectedIndex];
}

function renderMeta() {
  $.packName.value = state.source.packName ?? "";
  $.author.value = state.source.author ?? "";
  $.version.value = state.source.version ?? "";
  $.description.value = state.source.description ?? "";
  $.uniqueId.value = state.source.uniqueId ?? "";
  $.minimumApiVersion.value = state.source.minimumApiVersion ?? "";
  $.pondPrefix.value = state.source.pondPrefix ?? "";
  $.objectsTextureFile.value = state.source.objectsTextureFile ?? "";
  $.objectsTextureTarget.value = state.source.objectsTextureTarget ?? "";
}

function renderFishList() {
  fishList.innerHTML = "";

  state.source.fish.forEach((fish, index) => {
    const button = document.createElement("button");
    button.className = `fish-item${index === state.selectedIndex ? " active" : ""}`;
    button.innerHTML = `<strong>${escapeHtml(fish.displayName || fish.slug || `Fish ${index + 1}`)}</strong><small>${escapeHtml(fish.slug || "")}</small>`;
    button.addEventListener("click", () => {
      state.selectedIndex = index;
      renderAll();
    });
    fishList.appendChild(button);
  });
}

function renderFishEditor() {
  const fish = getCurrentFish();

  $.slug.value = fish.slug ?? "";
  $.displayName.value = fish.displayName ?? "";
  $.fishDescription.value = fish.description ?? "";
  $.spriteIndex.value = fish.spriteIndex ?? 0;
  $.price.value = fish.price ?? 0;
  $.edibility.value = fish.edibility ?? 0;
  $.enabledByDefault.checked = !!fish.enabledByDefault;
  $.isLegendary.checked = !!fish.isLegendary;
  $.contextTags.value = (fish.contextTags ?? []).join(", ");

  $.difficulty.value = fish.catch.difficulty ?? 0;
  $.behavior.value = fish.catch.behavior ?? "mixed";
  $.minSize.value = fish.catch.minSize ?? 0;
  $.maxSize.value = fish.catch.maxSize ?? 0;
  $.timeStart.value = fish.catch.timeStart ?? 600;
  $.timeEnd.value = fish.catch.timeEnd ?? 2600;
  $.weather.value = fish.catch.weather ?? "both";
  $.waterCode.value = fish.catch.waterCode ?? "";
  $.maxDepth.value = fish.catch.maxDepth ?? 1;
  $.baseChance.value = fish.catch.baseChance ?? 0.2;
  $.depthMultiplier.value = fish.catch.depthMultiplier ?? 0.15;
  $.minFishingLevel.value = fish.catch.minFishingLevel ?? 0;
  seasonCheckboxes.forEach(box => {
    box.checked = (fish.catch.seasons ?? []).includes(box.dataset.season);
  });

  $.pondSpawnTime.value = fish.pond.spawnTime ?? -1;
  setPondColor(fish.pond.waterColor ?? "200 225 235");
  $.pondWaterMinPopulation.value = fish.pond.waterMinPopulation ?? 2;
  updateEdibilityEstimate(fish.edibility ?? 0);
  updatePriceEstimate(fish.price ?? 0);

  ensureSpriteDraft();
  renderSpriteEditor();
  renderAquariumControls();
  renderLocations();
  renderProducts();
  renderGateTables();
}

function renderLocations() {
  const fish = getCurrentFish();
  locationsTable.innerHTML = "";

  fish.locations.forEach((location, index) => {
    const row = locationTemplate.content.firstElementChild.cloneNode(true);
    bindRowInputs(row, location, fish.locations, index, renderLocations);
    locationsTable.appendChild(row);
  });
}

function renderProducts() {
  const fish = getCurrentFish();
  fish.pond.products = sortPondProducts(fish.pond.products);
  productsTable.innerHTML = "";

  fish.pond.products.forEach((product, index) => {
    const row = productTemplate.content.firstElementChild.cloneNode(true);
    bindRowInputs(row, product, fish.pond.products, index, renderProducts);
    productsTable.appendChild(row);
  });
}

function bindRowInputs(row, item, collection, index, rerender) {
  row.querySelectorAll("input, select").forEach(fieldElement => {
    const field = fieldElement.dataset.field;
    if (fieldElement.tagName === "SELECT" && field === "itemId") {
      item[field] = normalizeObjectItemQuery(item[field]);
      populatePondProductOptions(fieldElement, item[field]);
    }

    fieldElement.value = item[field] ?? "";
    if (field === "location") {
      fieldElement.addEventListener("focus", () => {
        state.activeLocationInput = { input: fieldElement, item };
      });
    }
    const syncValue = () => {
      if (field === "itemId") {
        item[field] = normalizeObjectItemQuery(fieldElement.value);
        fieldElement.value = item[field];
      } else if (fieldElement.type === "number") {
        item[field] = field === "chance"
          ? readFloatInput(fieldElement.value)
          : readNumericInput(fieldElement.value);
      } else {
        item[field] = fieldElement.value;
      }

      if (field === "itemId") {
        updateItemNamePreview(row, item.itemId);
      }
      refreshOutputs();
    };

    fieldElement.addEventListener("input", syncValue);
    fieldElement.addEventListener("change", syncValue);
  });

  updateItemNamePreview(row, item.itemId);

  row.querySelector("[data-action='remove']").addEventListener("click", () => {
    collection.splice(index, 1);
    rerender();
    refreshOutputs();
  });
}

function bindMetaField(id) {
  $[id].addEventListener("input", () => {
    state.source[id] = $[id].value;
    refreshOutputs();
  });
}

function bindFishField(id) {
  const handler = () => {
    const fish = getCurrentFish();

    switch (id) {
      case "slug":
        fish.slug = sanitizeSlug($.slug.value);
        $.slug.value = fish.slug;
        renderFishList();
        break;
      case "displayName":
        fish.displayName = $.displayName.value;
        renderFishList();
        break;
      case "fishDescription":
        fish.description = $.fishDescription.value;
        break;
      case "spriteIndex":
      case "price":
      case "edibility":
        fish[id] = readNumericInput($[id].value);
        if (id === "spriteIndex") {
          renderSpriteEditor();
        }
        break;
      case "enabledByDefault":
        fish.enabledByDefault = $.enabledByDefault.checked;
        break;
      case "isLegendary":
        fish.isLegendary = $.isLegendary.checked;
        break;
      case "contextTags":
        fish.contextTags = normalizeFishContextTags(parseCommaList($.contextTags.value));
        $.contextTags.value = fish.contextTags.join(", ");
        break;
      case "difficulty":
      case "minSize":
      case "maxSize":
      case "timeStart":
      case "timeEnd":
      case "maxDepth":
      case "minFishingLevel":
        fish.catch[id] = readNumericInput($[id].value);
        break;
      case "baseChance":
      case "depthMultiplier":
        fish.catch[id] = readFloatInput($[id].value);
        break;
      case "behavior":
      case "weather":
      case "waterCode":
        fish.catch[id] = $[id].value;
        break;
      case "pondSpawnTime":
        fish.pond.spawnTime = readNumericInput($.pondSpawnTime.value);
        break;
      case "pondWaterMinPopulation":
        fish.pond.waterMinPopulation = readNumericInput($.pondWaterMinPopulation.value);
        break;
    }

    if (id === "edibility") {
      updateEdibilityEstimate(fish.edibility);
    }

    if (id === "price") {
      updatePriceEstimate(fish.price);
    }

    refreshOutputs();
  };

  $[id].addEventListener("input", handler);
}

function bindPondColorFields() {
  const syncFromText = () => {
    setPondColor($.pondWaterColor.value.trim() || "200 225 235");
    refreshOutputs();
  };

  const syncFromPicker = valueOrEvent => {
    const hexValue =
      typeof valueOrEvent === "string"
        ? valueOrEvent
        : valueOrEvent?.target?.value || $.pondWaterColorPicker.value;
    setPondColor(hexToRgbString(hexValue), hexValue);
    refreshOutputs();
  };

  $.pondWaterColor.addEventListener("input", syncFromText);
  $.pondWaterColor.addEventListener("change", syncFromText);
  $.pondWaterColorPicker.addEventListener("input", syncFromPicker);
  $.pondWaterColorPicker.addEventListener("change", syncFromPicker);
  window.tofSetPondColor = (rgbValue, hexValue = null) => {
    setPondColor(rgbValue, hexValue);
    refreshOutputs();
  };
}

function setPondColor(rawRgbValue, rawHexValue = null) {
  const rgbValue = normalizeRgbString(rawRgbValue);
  const hexValue = rawHexValue || rgbStringToHex(rgbValue);
  const fish = getCurrentFish();
  fish.pond.waterColor = rgbValue;
  $.pondWaterColor.value = rgbValue;
  $.pondWaterColorPicker.value = hexValue;
  updatePondColorPreview(rgbValue, hexValue);
}

function bindAquariumFields() {
  $.aquariumEnabled.addEventListener("change", () => {
    getCurrentFish().aquarium.enabled = $.aquariumEnabled.checked;
    refreshOutputs();
    renderSpriteEditor();
  });

  $.aquariumType.addEventListener("change", () => {
    const fish = getCurrentFish();
    fish.aquarium.type = $.aquariumType.value;
    if (aquariumFieldsAreBlank(fish.aquarium)) {
      applyAquariumMappingDefaults(fish.aquarium);
    }
    refreshOutputs();
    renderSpriteEditor();
  });

  ["aquariumField3", "aquariumField4", "aquariumField5", "aquariumField6"].forEach(id => {
    $[id].addEventListener("input", () => {
      const fish = getCurrentFish();
      fish.aquarium[id.replace("aquarium", "").toLowerCase()] = $[id].value.trim();
      $.aquariumEntryPreview.textContent = buildAquariumEntryPreview(state.source, fish);
      refreshOutputs();
    });
  });
}

function aquariumFieldsAreBlank(aquarium) {
  return [aquarium.field3, aquarium.field4, aquarium.field5, aquarium.field6]
    .every(value => !String(value ?? "").trim());
}

function buildAquariumLocalFrameRefs(count) {
  return Array.from({ length: Math.max(0, count) }, (_, index) => String(index));
}

function applyAquariumMappingDefaults(aquarium) {
  const refs = buildAquariumLocalFrameRefs(aquarium.frames.length);
  const first = refs[0] ?? "";

  switch (aquarium.type) {
    case "front_crawl":
      aquarium.field3 = first;
      aquarium.field4 = "";
      aquarium.field5 = refs.length ? [...refs, refs[refs.length - 1], refs[refs.length - 1]].join(" ") : "";
      aquarium.field6 = "";
      break;
    case "crawl":
      aquarium.field3 = first;
      aquarium.field4 = "";
      aquarium.field5 = refs.length ? [...refs, refs[refs.length - 1], refs[refs.length - 1]].join(" ") : "";
      aquarium.field6 = "";
      break;
    case "cephalopod":
      aquarium.field3 = first;
      aquarium.field4 = refs.slice(0, 2).join(" ");
      aquarium.field5 = refs.length > 1 ? refs.slice(1).flatMap(ref => [ref, ref]).join(" ") : "";
      aquarium.field6 = refs.length > 2 ? refs.slice(-2).join(" ") : (refs[1] ?? "");
      break;
    case "float":
      aquarium.field3 = first;
      aquarium.field4 = refs.slice(1, 3).flatMap(ref => [ref, ref]).join(" ");
      aquarium.field5 = refs.slice(3, 5).flatMap(ref => [ref, ref]).join(" ");
      aquarium.field6 = refs.slice(5).flatMap(ref => [ref, ref]).join(" ");
      break;
    case "ground":
      aquarium.field3 = first;
      aquarium.field4 = refs.slice(1).join(" ");
      aquarium.field5 = "";
      aquarium.field6 = "";
      break;
    case "fish":
    case "eel":
    case "static":
    default:
      aquarium.field3 = "";
      aquarium.field4 = "";
      aquarium.field5 = "";
      aquarium.field6 = "";
      break;
  }
}

function autoFillAquariumMapping() {
  const fish = getCurrentFish();
  applyAquariumMappingDefaults(fish.aquarium);
  renderAquariumControls();
  refreshOutputs();
}

function getCurrentSpriteSpec() {
  const fish = getCurrentFish();
  if (state.spriteMode === "aquarium") {
    const aquarium = fish.aquarium;
    const frameIndex = clampAquariumFrameIndex(aquarium);
    return {
      mode: "aquarium",
      width: AquariumSpriteWidth,
      height: AquariumSpriteHeight,
      tileWidth: AquariumSpriteAtlasTileWidth,
      tileHeight: AquariumSpriteAtlasTileHeight,
      frameIndex,
      pixels: normalizeAquariumFramePixels(aquarium.frames[frameIndex]),
      label: `${fish.displayName || fish.slug || "Selected fish"} aquarium frame ${frameIndex + 1}`,
      atlasLabel: buildAquariumAtlasMeta(state.source, fish, frameIndex),
      previewScale: 4,
      write(pixels) {
        aquarium.frames[frameIndex] = [...pixels];
      }
    };
  }

  return {
    mode: "item",
    width: ObjectSpriteWidth,
    height: ObjectSpriteHeight,
    tileWidth: ObjectSpriteAtlasTileWidth,
    tileHeight: ObjectSpriteAtlasTileHeight,
    pixels: normalizeSpritePixels(fish.spritePixels, ObjectSpriteWidth, ObjectSpriteHeight),
    label: `${fish.displayName || fish.slug || "Selected fish"} sprite`,
    atlasLabel: `Slot ${readNumericInput(fish.spriteIndex)} in ${buildAtlasFileName()} (${buildObjectAtlasCanvas(state.source).width}x${buildObjectAtlasCanvas(state.source).height})`,
    previewScale: 4,
    write(pixels) {
      fish.spritePixels = [...pixels];
    }
  };
}

function makeSpriteDraftToken() {
  return state.spriteMode === "aquarium"
    ? `${state.selectedIndex}:aquarium:${state.aquariumFrameIndex}`
    : `${state.selectedIndex}:item`;
}

function clampAquariumFrameIndex(aquarium = getCurrentFish().aquarium) {
  if (!aquarium.frames.length) {
    aquarium.frames.push(createBlankSpritePixels(AquariumSpriteWidth, AquariumSpriteHeight));
  }

  if (state.aquariumFrameIndex < 0) {
    state.aquariumFrameIndex = 0;
  }

  if (state.aquariumFrameIndex >= aquarium.frames.length) {
    state.aquariumFrameIndex = aquarium.frames.length - 1;
  }

  return state.aquariumFrameIndex;
}

function resizeSpriteEditorCanvas(spec) {
  const width = spec.width * SpriteEditorCellSize;
  const height = spec.height * SpriteEditorCellSize;
  spriteEditorCanvas.width = width;
  spriteEditorCanvas.height = height;
  spriteEditorCanvas.style.width = `${width}px`;
  spriteEditorCanvas.style.height = `${height}px`;
  spriteEditorGrid.style.width = `${width}px`;
  spriteEditorGrid.style.height = `${height}px`;
}

function renderAquariumControls() {
  const fish = getCurrentFish();
  ensureAquariumSlotIndexes(state.source?.fish ?? []);
  const aquarium = fish.aquarium;
  clampAquariumFrameIndex(aquarium);
  const typeDoc = aquariumTypeDocs[aquarium.type] ?? aquariumTypeDocs.fish;
  $.aquariumEnabled.checked = !!aquarium.enabled;
  $.aquariumType.value = aquarium.type;
  $.aquariumField3.value = aquarium.field3 ?? "";
  $.aquariumField4.value = aquarium.field4 ?? "";
  $.aquariumField5.value = aquarium.field5 ?? "";
  $.aquariumField6.value = aquarium.field6 ?? "";
  $.aquariumStatus.textContent = aquarium.enabled
    ? `${aquarium.frames.length} frame${aquarium.frames.length === 1 ? "" : "s"} ready for aquarium export as type "${aquarium.type}", starting at slot ${aquarium.slotIndex ?? 0}.`
    : "Aquarium export is disabled for this fish. Enable it when you want this fish to appear correctly in tanks.";
  $.aquariumTypeHint.textContent = typeDoc.hint;
  $.aquariumFieldGuide.textContent = typeDoc.guide;
  $.aquariumEntryPreview.textContent = aquarium.enabled
    ? buildAquariumEntryPreview(state.source, fish)
    : "Aquarium export is disabled for this fish.";

  aquariumFrameList.innerHTML = "";
  aquarium.frames.forEach((framePixels, index) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `aquarium-frame-button${index === state.aquariumFrameIndex ? " active" : ""}`;
    const thumb = buildFrameThumbnailCanvas(normalizeAquariumFramePixels(framePixels), AquariumSpriteWidth, AquariumSpriteHeight, 3);
    thumb.className = "aquarium-frame-thumb";
    const meta = document.createElement("div");
    meta.className = "aquarium-frame-meta";
    meta.innerHTML = `<strong>Frame ${index}</strong><small>Local ref ${index}</small>`;
    button.appendChild(thumb);
    button.appendChild(meta);
    button.addEventListener("click", () => {
      state.aquariumFrameIndex = index;
      if (state.spriteMode === "aquarium") {
        ensureSpriteDraft();
        renderSpriteEditor();
      } else {
        renderAquariumControls();
      }
    });
    aquariumFrameList.appendChild(button);
  });
}

function addAquariumFrame() {
  const aquarium = getCurrentFish().aquarium;
  aquarium.frames.push(createBlankSpritePixels(AquariumSpriteWidth, AquariumSpriteHeight));
  if (aquariumFieldsAreBlank(aquarium)) {
    applyAquariumMappingDefaults(aquarium);
  }
  state.aquariumFrameIndex = aquarium.frames.length - 1;
  state.spriteDraftToken = "";
  state.spriteMode = "aquarium";
  renderSpriteEditor();
  refreshOutputs();
}

function duplicateAquariumFrame() {
  const aquarium = getCurrentFish().aquarium;
  clampAquariumFrameIndex(aquarium);
  aquarium.frames.splice(state.aquariumFrameIndex + 1, 0, [...normalizeAquariumFramePixels(aquarium.frames[state.aquariumFrameIndex])]);
  if (aquariumFieldsAreBlank(aquarium)) {
    applyAquariumMappingDefaults(aquarium);
  }
  state.aquariumFrameIndex += 1;
  state.spriteDraftToken = "";
  state.spriteMode = "aquarium";
  renderSpriteEditor();
  refreshOutputs();
}

function removeAquariumFrame() {
  const aquarium = getCurrentFish().aquarium;
  if (aquarium.frames.length === 1) {
    aquarium.frames[0] = createBlankSpritePixels(AquariumSpriteWidth, AquariumSpriteHeight);
  } else {
    aquarium.frames.splice(clampAquariumFrameIndex(aquarium), 1);
    clampAquariumFrameIndex(aquarium);
  }

  if (aquariumFieldsAreBlank(aquarium)) {
    applyAquariumMappingDefaults(aquarium);
  }

  state.spriteDraftToken = "";
  state.spriteMode = "aquarium";
  renderSpriteEditor();
  refreshOutputs();
}

function bindSpriteEditor() {
  let painting = false;
  let rotating = false;
  let moveScaling = false;
  let lastPointerCell = "";

  const paintFromEvent = event => {
    const point = getSpriteGridPoint(event);
    const x = Math.floor(point.x);
    const y = Math.floor(point.y);
    const cellKey = `${x},${y}`;
    if (cellKey === lastPointerCell) {
      return;
    }

    lastPointerCell = cellKey;
    if (state.spriteTool === "pick") {
      pickSpriteColor(x, y);
      return;
    }

    if (state.spriteTool === "erase") {
      setSpritePixel(x, y, null);
      return;
    }

    if (state.spriteTool === "dodge" || state.spriteTool === "burn") {
      adjustSpritePixelTone(x, y, state.spriteTool);
      return;
    }

    setSpritePixel(x, y, $.spriteColor.value);
  };

  const beginRotation = event => {
    ensureSpriteDraft();
    state.spriteRotationBasePixels = [...state.spriteDraft];
    state.spriteRotationStartAngle = getSpritePointerAngle(event);
    rotating = true;
  };

  const rotateFromEvent = event => {
    if (!rotating || !state.spriteRotationBasePixels) {
      return;
    }

    const currentAngle = getSpritePointerAngle(event);
    const delta = currentAngle - state.spriteRotationStartAngle;
    state.spriteDraft = rotatePixels(state.spriteRotationBasePixels, delta);
    drawSpriteEditorCanvas(state.spriteDraft);
    drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
  };

  const beginMoveScale = event => {
    ensureSpriteDraft();
      const spec = getCurrentSpriteSpec();
      const bounds = getOpaqueSpriteBounds(state.spriteDraft, spec.width, spec.height);
    if (!bounds) {
      return false;
    }

    const point = getSpriteGridPoint(event);
    const action = isPointOnScaleHandle(point, bounds)
      ? "scale"
      : isPointInBounds(point, bounds)
        ? "move"
        : null;
    if (!action) {
      return false;
    }

    state.spriteTransformAction = action;
    state.spriteTransformBasePixels = [...state.spriteDraft];
    state.spriteTransformBounds = bounds;
    state.spriteTransformStartPoint = point;
    moveScaling = true;
    return true;
  };

  const moveScaleFromEvent = event => {
    if (!moveScaling || !state.spriteTransformAction || !state.spriteTransformBasePixels || !state.spriteTransformBounds || !state.spriteTransformStartPoint) {
      return;
    }

    const point = getSpriteGridPoint(event);
    if (state.spriteTransformAction === "move") {
      const dx = Math.round(point.x - state.spriteTransformStartPoint.x);
      const dy = Math.round(point.y - state.spriteTransformStartPoint.y);
      state.spriteDraft = translatePixels(state.spriteTransformBasePixels, dx, dy);
    } else if (state.spriteTransformAction === "scale") {
      const bounds = state.spriteTransformBounds;
      const baseWidth = bounds.maxX - bounds.minX + 1;
      const baseHeight = bounds.maxY - bounds.minY + 1;
      const targetWidth = Math.max(1, Math.round(point.x - bounds.minX + 1));
      const targetHeight = Math.max(1, Math.round(point.y - bounds.minY + 1));
      const scale = Math.max(targetWidth / baseWidth, targetHeight / baseHeight, 0.1);
      state.spriteDraft = scalePixelsFromBounds(state.spriteTransformBasePixels, bounds, scale);
    }

    drawSpriteEditorCanvas(state.spriteDraft);
    drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
  };

  spriteEditorCanvas.addEventListener("pointerdown", event => {
    event.preventDefault();
    spriteEditorGrid.focus();
    spriteEditorCanvas.setPointerCapture(event.pointerId);
    if (state.spriteMoveScaleMode) {
      pushSpriteUndoState();
      if (!beginMoveScale(event)) {
        try {
          spriteEditorCanvas.releasePointerCapture(event.pointerId);
        } catch {
          // ignored
        }
      }
      return;
    }

    if (state.spriteTransformMode) {
      pushSpriteUndoState();
      beginRotation(event);
      rotateFromEvent(event);
      return;
    }

    if (state.spriteTool !== "pick") {
      pushSpriteUndoState();
    }
    painting = true;
    lastPointerCell = "";
    paintFromEvent(event);
  });

  spriteEditorCanvas.addEventListener("pointermove", event => {
    if (state.spriteMoveScaleMode) {
      moveScaleFromEvent(event);
      return;
    }

    if (state.spriteTransformMode) {
      rotateFromEvent(event);
      return;
    }

    if (!painting) return;
    paintFromEvent(event);
  });

  spriteEditorCanvas.addEventListener("pointerup", event => {
    if (state.spriteMoveScaleMode) {
      moveScaling = false;
      state.spriteTransformAction = null;
      state.spriteTransformBasePixels = null;
      state.spriteTransformBounds = null;
      state.spriteTransformStartPoint = null;
      try {
        spriteEditorCanvas.releasePointerCapture(event.pointerId);
      } catch {
        // ignored
      }
      return;
    }

    if (state.spriteTransformMode) {
      rotating = false;
      state.spriteRotationBasePixels = null;
      spriteEditorCanvas.releasePointerCapture(event.pointerId);
      return;
    }

    painting = false;
    lastPointerCell = "";
    spriteEditorCanvas.releasePointerCapture(event.pointerId);
  });

  spriteEditorCanvas.addEventListener("pointercancel", () => {
    painting = false;
    lastPointerCell = "";
    moveScaling = false;
    rotating = false;
    state.spriteTransformAction = null;
    state.spriteTransformBasePixels = null;
    state.spriteTransformBounds = null;
    state.spriteTransformStartPoint = null;
    state.spriteRotationBasePixels = null;
  });
  document.addEventListener("paste", handleSpriteClipboardPaste);
  document.addEventListener("keydown", handleSpriteEditorKeyDown);
}

function setSpritePixel(x, y, color) {
  ensureSpriteDraft();
  const spec = getCurrentSpriteSpec();
  const nextColor = normalizeSpriteColor(color);
  const index = y * spec.width + x;
  if (state.spriteDraft[index] === nextColor) {
    return;
  }

  state.spriteDraft[index] = nextColor;
  drawSpriteEditorCanvas(state.spriteDraft);
  drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
}

function adjustSpritePixelTone(x, y, mode) {
  ensureSpriteDraft();
  const spec = getCurrentSpriteSpec();
  const index = y * spec.width + x;
  const current = normalizeSpriteColor(state.spriteDraft[index]);
  if (!current) {
    return;
  }

  const next = shiftColorTone(current, mode === "dodge" ? 0.18 : -0.18);
  if (next === current) {
    return;
  }

  state.spriteDraft[index] = next;
  drawSpriteEditorCanvas(state.spriteDraft);
  drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
}

function renderSpriteEditor() {
  ensureSpriteDraft();
  const spec = getCurrentSpriteSpec();
  resizeSpriteEditorCanvas(spec);
  $.spriteEditorLabel.textContent = spec.label;
  $.spriteAtlasMeta.textContent = spec.atlasLabel;
  spriteEditItemButton.classList.toggle("active", state.spriteMode === "item");
  spriteEditAquariumButton.classList.toggle("active", state.spriteMode === "aquarium");
  spritePaintToolButton.classList.toggle("active", state.spriteTool === "paint");
  spriteEraseToolButton.classList.toggle("active", state.spriteTool === "erase");
  spritePickColorToolButton.classList.toggle("active", state.spriteTool === "pick");
  spriteDodgeToolButton.classList.toggle("active", state.spriteTool === "dodge");
  spriteBurnToolButton.classList.toggle("active", state.spriteTool === "burn");
  spriteBackgroundToggleButton.textContent = state.spriteBackground === "dark" ? "White bg" : "Black bg";
  spriteBackgroundToggleButton.classList.toggle("active", state.spriteBackground === "dark");
  spriteTransformMoveScaleButton.classList.toggle("active", state.spriteMoveScaleMode);
  spriteTransformRotateButton.classList.toggle("active", state.spriteTransformMode);
  aquariumControls.classList.toggle("hidden", state.spriteMode !== "aquarium");
  renderSpritePalette();
  renderAquariumControls();
  drawSpriteEditorCanvas(state.spriteDraft);
  drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
}

function setSpriteTool(tool) {
  state.spriteTool = tool;
  state.spriteMoveScaleMode = false;
  state.spriteTransformMode = false;
  renderSpriteEditor();
}

function toggleSpriteBackground() {
  state.spriteBackground = state.spriteBackground === "dark" ? "light" : "dark";
  renderSpriteEditor();
}

function setSpriteMode(mode) {
  if (state.spriteMode === mode) {
    return;
  }

  state.spriteMode = mode;
  state.spriteMoveScaleMode = false;
  state.spriteTransformMode = false;
  state.spriteTransformAction = null;
  state.spriteTransformBasePixels = null;
  state.spriteTransformBounds = null;
  state.spriteTransformStartPoint = null;
  state.spriteRotationBasePixels = null;
  ensureSpriteDraft();
  renderSpriteEditor();
}

function toggleSpriteMoveScaleMode() {
  state.spriteMoveScaleMode = !state.spriteMoveScaleMode;
  if (state.spriteMoveScaleMode) {
    state.spriteTransformMode = false;
  }
  state.spriteTransformAction = null;
  state.spriteTransformBasePixels = null;
  state.spriteTransformBounds = null;
  state.spriteTransformStartPoint = null;
  renderSpriteEditor();
}

function toggleSpriteTransformMode() {
  state.spriteTransformMode = !state.spriteTransformMode;
  if (state.spriteTransformMode) {
    state.spriteMoveScaleMode = false;
  }
  state.spriteRotationBasePixels = null;
  renderSpriteEditor();
}

function ensureSpriteDraft() {
  const token = makeSpriteDraftToken();
  if (state.spriteDraftToken === token) {
    return;
  }

  state.spriteDraft = [...getCurrentSpriteSpec().pixels];
  state.spriteDraftToken = token;
  state.spriteUndoStack = [];
}

function saveSpriteDraft() {
  ensureSpriteDraft();
  const fish = getCurrentFish();
  if (state.spriteMode === "aquarium") {
    const aquarium = normalizeAquariumData(fish.aquarium);
    const frameIndex = clampAquariumFrameIndex(aquarium);
    aquarium.frames[frameIndex] = normalizeSpritePixels([...state.spriteDraft], AquariumSpriteWidth, AquariumSpriteHeight);
    fish.aquarium = aquarium;
  } else {
    fish.spritePixels = normalizeSpritePixels([...state.spriteDraft], ObjectSpriteWidth, ObjectSpriteHeight);
  }

  state.spriteDraftToken = makeSpriteDraftToken();
  persistSnapshots();
  void autoSaveSourceIfPossible();
  refreshOutputs();
  renderSpriteEditor();
}

function rotateSpriteDraft(direction) {
  ensureSpriteDraft();
  state.spriteTransformMode = false;
  state.spriteDraft = rotatePixels(state.spriteDraft, direction === "left" ? -Math.PI / 2 : Math.PI / 2);
  drawSpriteEditorCanvas(state.spriteDraft);
  drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
}

function mirrorSpriteDraft() {
  ensureSpriteDraft();
  const spec = getCurrentSpriteSpec();
  pushSpriteUndoState();
  state.spriteMoveScaleMode = false;
  state.spriteTransformMode = false;
  const mirrored = createBlankSpritePixels(spec.width, spec.height);

  for (let y = 0; y < spec.height; y++) {
    for (let x = 0; x < spec.width; x++) {
      mirrored[y * spec.width + x] = state.spriteDraft[y * spec.width + (spec.width - 1 - x)];
    }
  }

  state.spriteDraft = mirrored;
  drawSpriteEditorCanvas(state.spriteDraft);
  drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
}

async function handleSpriteClipboardPaste(event) {
  const clipboardItems = [...(event.clipboardData?.items ?? [])];
  const imageItem = clipboardItems.find(item => item.type.startsWith("image/"));
  if (!imageItem) {
    return;
  }

  const target = event.target instanceof Element ? event.target : null;
  const active = document.activeElement instanceof Element ? document.activeElement : null;
  const targetInsideSpritePanel = !!target && spritePanel?.contains(target);
  const activeInsideSpritePanel = !!active && spritePanel?.contains(active);
  if (!targetInsideSpritePanel && !activeInsideSpritePanel) {
    return;
  }

  const file = imageItem.getAsFile();
  if (!file) {
    return;
  }

  event.preventDefault();
  await importSpriteFromClipboardFile(file);
}

async function importSpriteFromClipboardFile(file) {
  const spec = getCurrentSpriteSpec();
  const image = await loadImageBitmapFromBlob(file);
  if (!image) {
    return;
  }

  const workingCanvas = document.createElement("canvas");
  workingCanvas.width = spec.width;
  workingCanvas.height = spec.height;
  const context = workingCanvas.getContext("2d");
  context.clearRect(0, 0, spec.width, spec.height);
  context.imageSmoothingEnabled = false;

  const scale = Math.min(spec.width / image.width, spec.height / image.height);
  const drawWidth = Math.max(1, Math.round(image.width * scale));
  const drawHeight = Math.max(1, Math.round(image.height * scale));
  const drawX = Math.floor((spec.width - drawWidth) / 2);
  const drawY = Math.floor((spec.height - drawHeight) / 2);
  context.drawImage(image, drawX, drawY, drawWidth, drawHeight);

  const data = context.getImageData(0, 0, spec.width, spec.height).data;
  ensureSpriteDraft();
  pushSpriteUndoState();
  state.spriteDraft = Array.from({ length: spec.width * spec.height }, (_, index) => {
    const pixelOffset = index * 4;
    const alpha = data[pixelOffset + 3];
    if (alpha < 16) {
      return null;
    }

    return rgbToHex(data[pixelOffset], data[pixelOffset + 1], data[pixelOffset + 2]);
  });

  drawSpriteEditorCanvas(state.spriteDraft);
  drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
}

function handleSpriteEditorKeyDown(event) {
  const active = document.activeElement instanceof Element ? document.activeElement : null;
  if (!active || !spritePanel?.contains(active)) {
    return;
  }

  if (!event.ctrlKey || event.shiftKey || event.altKey || event.metaKey || event.key.toLowerCase() !== "z") {
    return;
  }

  event.preventDefault();
  undoSpriteDraft();
}

function pushSpriteUndoState() {
  ensureSpriteDraft();
  const snapshot = [...state.spriteDraft];
  const last = state.spriteUndoStack[state.spriteUndoStack.length - 1];
  if (last && last.every((pixel, index) => pixel === snapshot[index])) {
    return;
  }

  state.spriteUndoStack.push(snapshot);
  if (state.spriteUndoStack.length > 50) {
    state.spriteUndoStack.shift();
  }
}

function undoSpriteDraft() {
  ensureSpriteDraft();
  const previous = state.spriteUndoStack.pop();
  if (!previous) {
    return;
  }

  state.spriteMoveScaleMode = false;
  state.spriteTransformMode = false;
  state.spriteTransformAction = null;
  state.spriteTransformBasePixels = null;
  state.spriteTransformBounds = null;
  state.spriteTransformStartPoint = null;
  state.spriteRotationBasePixels = null;
  state.spriteDraft = [...previous];
  drawSpriteEditorCanvas(state.spriteDraft);
  drawSpriteCanvas(spritePreviewCanvas, state.spriteDraft);
}

async function loadImageBitmapFromBlob(blob) {
  if ("createImageBitmap" in window) {
    try {
      return await createImageBitmap(blob);
    } catch {
      // Fall back to HTMLImageElement below.
    }
  }

  const objectUrl = URL.createObjectURL(blob);
  try {
    return await new Promise(resolve => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => resolve(null);
      image.src = objectUrl;
    });
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

function saveCurrentSpriteColorToPalette() {
  const color = normalizeSpriteColor($.spriteColor.value);
  if (!color || !state.source) {
    return;
  }

  const existing = normalizeSpritePalette(state.source.spritePalette);
  if (existing.includes(color)) {
    state.source.spritePalette = existing;
    renderSpritePalette();
    persistSnapshots();
    return;
  }

  state.source.spritePalette = normalizeSpritePalette([...existing, color]);
  renderSpritePalette();
  persistSnapshots();
  void autoSaveSourceIfPossible();
}

function removeSpritePaletteColor(color) {
  if (!state.source) {
    return;
  }

  state.source.spritePalette = normalizeSpritePalette(
    (state.source.spritePalette ?? []).filter(entry => entry !== color)
  );
  renderSpritePalette();
  persistSnapshots();
  void autoSaveSourceIfPossible();
}

function renderSpritePalette() {
  if (!spritePalette || !state.source) {
    return;
  }

  const palette = normalizeSpritePalette(state.source.spritePalette);
  state.source.spritePalette = palette;
  spritePalette.innerHTML = "";

  if (!palette.length) {
    const empty = document.createElement("div");
    empty.className = "sprite-palette-empty";
    empty.textContent = "No saved swatches yet.";
    spritePalette.appendChild(empty);
    return;
  }

  for (const color of palette) {
    const swatch = document.createElement("div");
    swatch.className = `sprite-swatch${$.spriteColor.value.toLowerCase() === color ? " active" : ""}`;

    const selectButton = document.createElement("button");
    selectButton.type = "button";
    selectButton.className = "sprite-swatch-button";
    selectButton.style.background = color;
    selectButton.title = `Use ${color}`;
    selectButton.addEventListener("click", () => {
      $.spriteColor.value = color;
      renderSpritePalette();
    });

    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.className = "sprite-swatch-remove";
    removeButton.textContent = "×";
    removeButton.title = `Remove ${color}`;
    removeButton.addEventListener("click", event => {
      event.stopPropagation();
      removeSpritePaletteColor(color);
    });

    swatch.appendChild(selectButton);
    swatch.appendChild(removeButton);
    spritePalette.appendChild(swatch);
  }
}

function drawSpriteEditorCanvas(pixels) {
  const spec = getCurrentSpriteSpec();
  const context = spriteEditorCanvas.getContext("2d");
  const theme = getSpriteEditorTheme();
  context.clearRect(0, 0, spriteEditorCanvas.width, spriteEditorCanvas.height);
  context.fillStyle = theme.editorBackground;
  context.fillRect(0, 0, spriteEditorCanvas.width, spriteEditorCanvas.height);
  spriteEditorGrid.style.backgroundColor = theme.editorBackground;
  spritePreviewCanvas.style.backgroundColor = theme.previewBackground;

  pixels.forEach((pixel, index) => {
    if (!pixel) {
      return;
    }

    const x = (index % spec.width) * SpriteEditorCellSize;
    const y = Math.floor(index / spec.width) * SpriteEditorCellSize;
    context.fillStyle = pixel;
    context.fillRect(x, y, SpriteEditorCellSize, SpriteEditorCellSize);
  });

  context.strokeStyle = theme.gridStroke;
  context.lineWidth = 1;
  for (let offset = 0; offset <= spriteEditorCanvas.width; offset += SpriteEditorCellSize) {
    context.beginPath();
    context.moveTo(offset + 0.5, 0);
    context.lineTo(offset + 0.5, spriteEditorCanvas.height);
    context.stroke();
  }

  for (let offset = 0; offset <= spriteEditorCanvas.height; offset += SpriteEditorCellSize) {
    context.beginPath();
    context.moveTo(0, offset + 0.5);
    context.lineTo(spriteEditorCanvas.width, offset + 0.5);
    context.stroke();
  }

  if (state.spriteMoveScaleMode) {
    const bounds = getOpaqueSpriteBounds(pixels, spec.width, spec.height);
    if (bounds) {
      const left = bounds.minX * SpriteEditorCellSize + 0.5;
      const top = bounds.minY * SpriteEditorCellSize + 0.5;
      const width = (bounds.maxX - bounds.minX + 1) * SpriteEditorCellSize;
      const height = (bounds.maxY - bounds.minY + 1) * SpriteEditorCellSize;
      context.save();
      context.strokeStyle = "#b96a2f";
      context.lineWidth = 2;
      context.strokeRect(left, top, width, height);
      context.fillStyle = "#b96a2f";
      const handleSize = Math.max(8, Math.floor(SpriteEditorCellSize));
      context.fillRect(left + width - handleSize / 2, top + height - handleSize / 2, handleSize, handleSize);
      context.restore();
    }
  }
}

function drawSpriteCanvas(canvas, pixels) {
  const spec = getCurrentSpriteSpec();
  const theme = getSpriteEditorTheme();
  canvas.width = spec.width;
  canvas.height = spec.height;
  canvas.style.width = `${spec.width * spec.previewScale}px`;
  canvas.style.height = `${spec.height * spec.previewScale}px`;
  canvas.style.backgroundColor = theme.previewBackground;
  const context = canvas.getContext("2d");
  context.clearRect(0, 0, spec.width, spec.height);

  pixels.forEach((pixel, index) => {
    if (!pixel) {
      return;
    }

    const x = index % spec.width;
    const y = Math.floor(index / spec.width);
    context.fillStyle = pixel;
    context.fillRect(x, y, 1, 1);
  });
}

function getSpriteEditorTheme() {
  if (state.spriteBackground === "dark") {
    return {
      editorBackground: "#101217",
      previewBackground: "#171a22",
      gridStroke: "rgba(255, 255, 255, 0.16)"
    };
  }

  return {
    editorBackground: "#fffdf9",
    previewBackground: "#ffffff",
    gridStroke: "rgba(44, 36, 29, 0.18)"
  };
}

function shiftColorTone(color, amount) {
  const match = normalizeSpriteColor(color)?.match(/^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i);
  if (!match) {
    return normalizeSpriteColor(color);
  }

  const channels = match.slice(1).map(hex => Number.parseInt(hex, 16));
  const shifted = channels.map(channel => {
    if (amount >= 0) {
      return Math.round(channel + (255 - channel) * amount);
    }

    return Math.round(channel * (1 + amount));
  });

  return `#${shifted.map(channel => Math.max(0, Math.min(255, channel)).toString(16).padStart(2, "0")).join("")}`;
}

function buildFrameThumbnailCanvas(pixels, width, height, scale = 3) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  canvas.style.width = `${width * scale}px`;
  canvas.style.height = `${height * scale}px`;
  const context = canvas.getContext("2d");
  context.clearRect(0, 0, width, height);

  pixels.forEach((pixel, index) => {
    if (!pixel) {
      return;
    }

    const x = index % width;
    const y = Math.floor(index / width);
    context.fillStyle = pixel;
    context.fillRect(x, y, 1, 1);
  });

  return canvas;
}

function pickSpriteColor(x, y) {
  ensureSpriteDraft();
  const spec = getCurrentSpriteSpec();
  const color = state.spriteDraft[y * spec.width + x];
  if (!color) {
    return;
  }

  $.spriteColor.value = color;
  renderSpritePalette();
}

function getSpriteGridPoint(event) {
  const spec = getCurrentSpriteSpec();
  const rect = spriteEditorCanvas.getBoundingClientRect();
  const relativeX = ((event.clientX - rect.left) / rect.width) * spec.width;
  const relativeY = ((event.clientY - rect.top) / rect.height) * spec.height;
  return {
    x: Math.max(0, Math.min(spec.width - 1, relativeX)),
    y: Math.max(0, Math.min(spec.height - 1, relativeY))
  };
}

function getOpaqueSpriteBounds(pixels, width, height) {
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;

  pixels.forEach((pixel, index) => {
    if (!pixel) {
      return;
    }

    const x = index % width;
    const y = Math.floor(index / width);
    minX = Math.min(minX, x);
    minY = Math.min(minY, y);
    maxX = Math.max(maxX, x);
    maxY = Math.max(maxY, y);
  });

  if (maxX < 0 || maxY < 0) {
    return null;
  }

  return { minX, minY, maxX, maxY };
}

function isPointInBounds(point, bounds) {
  return point.x >= bounds.minX
    && point.x <= bounds.maxX + 1
    && point.y >= bounds.minY
    && point.y <= bounds.maxY + 1;
}

function isPointOnScaleHandle(point, bounds) {
  const handleRadius = 1.25;
  return point.x >= bounds.maxX + 1 - handleRadius
    && point.x <= bounds.maxX + 1 + handleRadius
    && point.y >= bounds.maxY + 1 - handleRadius
    && point.y <= bounds.maxY + 1 + handleRadius;
}

function getSpritePointerAngle(event) {
  const rect = spriteEditorCanvas.getBoundingClientRect();
  const centerX = rect.left + rect.width / 2;
  const centerY = rect.top + rect.height / 2;
  return Math.atan2(event.clientY - centerY, event.clientX - centerX);
}

function pixelsToCanvas(pixels) {
  const spec = getCurrentSpriteSpec();
  const canvas = document.createElement("canvas");
  canvas.width = spec.width;
  canvas.height = spec.height;
  drawSpriteCanvas(canvas, pixels);
  return canvas;
}

function canvasToPixels(canvas) {
  const spec = getCurrentSpriteSpec();
  const context = canvas.getContext("2d");
  const data = context.getImageData(0, 0, spec.width, spec.height).data;
  return Array.from({ length: spec.width * spec.height }, (_, index) => {
    const pixelOffset = index * 4;
    const alpha = data[pixelOffset + 3];
    if (alpha < 16) {
      return null;
    }

    return rgbToHex(data[pixelOffset], data[pixelOffset + 1], data[pixelOffset + 2]);
  });
}

function translatePixels(sourcePixels, dx, dy) {
  const spec = getCurrentSpriteSpec();
  const sourceCanvas = pixelsToCanvas(sourcePixels);
  const targetCanvas = document.createElement("canvas");
  targetCanvas.width = spec.width;
  targetCanvas.height = spec.height;
  const context = targetCanvas.getContext("2d");
  context.clearRect(0, 0, spec.width, spec.height);
  context.imageSmoothingEnabled = false;
  context.drawImage(sourceCanvas, dx, dy);
  return canvasToPixels(targetCanvas);
}

function scalePixelsFromBounds(sourcePixels, bounds, scale) {
  const spec = getCurrentSpriteSpec();
  const sourceCanvas = pixelsToCanvas(sourcePixels);
  const targetCanvas = document.createElement("canvas");
  targetCanvas.width = spec.width;
  targetCanvas.height = spec.height;
  const context = targetCanvas.getContext("2d");
  context.clearRect(0, 0, spec.width, spec.height);
  context.imageSmoothingEnabled = false;

  const sourceWidth = bounds.maxX - bounds.minX + 1;
  const sourceHeight = bounds.maxY - bounds.minY + 1;
  const destWidth = Math.max(1, Math.min(spec.width - bounds.minX, Math.round(sourceWidth * scale)));
  const destHeight = Math.max(1, Math.min(spec.height - bounds.minY, Math.round(sourceHeight * scale)));

  context.drawImage(
    sourceCanvas,
    bounds.minX,
    bounds.minY,
    sourceWidth,
    sourceHeight,
    bounds.minX,
    bounds.minY,
    destWidth,
    destHeight
  );

  return canvasToPixels(targetCanvas);
}

function rotatePixels(sourcePixels, angleRadians) {
  const spec = getCurrentSpriteSpec();
  const rotated = createBlankSpritePixels(spec.width, spec.height);
  const centerX = (spec.width - 1) / 2;
  const centerY = (spec.height - 1) / 2;
  const cos = Math.cos(angleRadians);
  const sin = Math.sin(angleRadians);

  for (let targetY = 0; targetY < spec.height; targetY++) {
    for (let targetX = 0; targetX < spec.width; targetX++) {
      const dx = targetX - centerX;
      const dy = targetY - centerY;
      const sourceX = Math.round(centerX + dx * cos + dy * sin);
      const sourceY = Math.round(centerY - dx * sin + dy * cos);

      if (sourceX < 0 || sourceX >= spec.width || sourceY < 0 || sourceY >= spec.height) {
        continue;
      }

      rotated[targetY * spec.width + targetX] = sourcePixels[sourceY * spec.width + sourceX];
    }
  }

  return rotated;
}

seasonCheckboxes.forEach(box => {
  box.addEventListener("change", () => {
    getCurrentFish().catch.seasons = seasonCheckboxes
      .filter(item => item.checked)
      .map(item => item.dataset.season);
    refreshOutputs();
  });
});

function addFish() {
  const fish = createBlankFish();
  fish.spriteIndex = getNextSpriteIndex();
  fish.aquarium.slotIndex = getNextAquariumSlotIndex();
  state.source.fish.push(fish);
  state.selectedIndex = state.source.fish.length - 1;
  state.aquariumFrameIndex = 0;
  state.spriteDraftToken = "";
  renderAll();
}

function duplicateFish() {
  const copy = structuredClone(getCurrentFish());
  copy.slug = `${copy.slug}_copy`;
  copy.displayName = `${copy.displayName} Copy`;
  copy.spriteIndex = getNextSpriteIndex();
  copy.spritePixels = createBlankSpritePixels(ObjectSpriteWidth, ObjectSpriteHeight);
  copy.aquarium = createBlankAquariumData();
  copy.aquarium.slotIndex = getNextAquariumSlotIndex();
  state.source.fish.splice(state.selectedIndex + 1, 0, copy);
  state.selectedIndex += 1;
  state.aquariumFrameIndex = 0;
  state.spriteDraftToken = "";
  renderAll();
}

function deleteFish() {
  if (state.source.fish.length === 1) {
    alert("At least one fish record must remain.");
    return;
  }

  state.source.fish.splice(state.selectedIndex, 1);
  state.aquariumFrameIndex = 0;
  state.spriteDraftToken = "";
  renderAll();
}

function addLocation() {
  addLocationWithName("Mountain");
}

function addLocationWithName(locationName) {
  getCurrentFish().locations.push({
    location: locationName,
    chance: 0.15,
    minDistanceFromShore: 1,
    maxDistanceFromShore: -1,
    precedence: 0
  });
  renderLocations();
  refreshOutputs();
}

function addProduct() {
  getCurrentFish().pond.products.push({
    requiredPopulation: 0,
    chance: 0.5,
    itemId: "(O)812",
    minQuantity: 1,
    maxQuantity: 1
  });
  renderProducts();
  refreshOutputs();
}

function addGateItem(gateKey) {
  const collection = getGateEntries(gateKey);
  collection.push({
    itemId: "(O)388",
    quantity: 1
  });
  setGateEntries(gateKey, collection);
  renderGateTables();
  refreshOutputs();
}

function renderLocationLibrary() {
  const query = locationLibrarySearch.value.trim().toLowerCase();
  locationLibraryList.innerHTML = "";

  const filtered = locationLibrary.filter(entry => {
    if (!query) {
      return true;
    }

    return [entry.name, entry.source, entry.note]
      .join(" ")
      .toLowerCase()
      .includes(query);
  });

  filtered.forEach(entry => {
    const card = document.createElement("article");
    card.className = "library-item";
    card.innerHTML = `
      <div class="library-meta">
        <span class="pill">${escapeHtml(entry.source)}</span>
        <span class="pill note">${escapeHtml(entry.note)}</span>
      </div>
      <h3>${escapeHtml(entry.name)}</h3>
      <button type="button">Use location</button>
    `;
    card.querySelector("button").addEventListener("click", () => useLibraryLocation(entry.name));
    locationLibraryList.appendChild(card);
  });

  if (!filtered.length) {
    const empty = document.createElement("p");
    empty.className = "section-copy";
    empty.textContent = "No built-in matches. You can still type any exact custom internal location name manually.";
    locationLibraryList.appendChild(empty);
  }
}

function useLibraryLocation(locationName) {
  if (state.activeLocationInput?.input && document.contains(state.activeLocationInput.input)) {
    state.activeLocationInput.input.value = locationName;
    state.activeLocationInput.item.location = locationName;
    refreshOutputs();
    return;
  }

  addLocationWithName(locationName);
}

function applyPreset(presetKey) {
  const preset = presets[presetKey];
  if (!preset) {
    return;
  }

  const fish = getCurrentFish();
  fish.price = preset.price;
  fish.edibility = preset.edibility;
    fish.contextTags = normalizeFishContextTags([...preset.contextTags]);
  fish.catch = structuredClone(preset.catch);
  fish.locations = structuredClone(preset.locations);
  fish.pond = structuredClone(preset.pond);
  renderFishEditor();
  refreshOutputs();
}

function refreshOutputs() {
  const generated = generatePackFiles(state.source);
  $.manifestPreview.value = generated["manifest.json"];
  $.configPreview.value = generated["config.json"];
  $.contentPreview.value = generated["content.json"];
  $.i18nPreview.value = generated["i18n/default.json"];
  $.dataPreview.value = generated[getCurrentFishDataFileKey()] ?? "";
  $.validationPreview.value = formatValidationReport(validateSource(state.source));
}

function renderGateTables() {
  renderGateTable("4", gate4Table);
  renderGateTable("8", gate8Table);
}

function renderGateTable(gateKey, container) {
  const entries = getGateEntries(gateKey);
  container.innerHTML = "";

  if (!entries.length) {
    const empty = document.createElement("div");
    empty.className = "item-preview-entry unknown";
    empty.textContent = "No request items.";
    container.appendChild(empty);
    return;
  }

  entries.forEach((entry, index) => {
    const row = gateTemplate.content.firstElementChild.cloneNode(true);
    const itemSelect = row.querySelector("[data-field='itemId']");
    const quantityInput = row.querySelector("[data-field='quantity']");
    const exportCode = row.querySelector("[data-field='exportCode']");

    populatePondProductOptions(itemSelect, entry.itemId);
    itemSelect.value = entry.itemId ?? "(O)388";
    quantityInput.value = entry.quantity ?? 1;
    exportCode.textContent = formatGateEntry(entry);

    const syncGateEntry = () => {
      const nextEntries = getGateEntries(gateKey);
      nextEntries[index] = {
        itemId: itemSelect.value,
        quantity: Math.max(1, readNumericInput(quantityInput.value))
      };
      quantityInput.value = String(nextEntries[index].quantity);
      exportCode.textContent = formatGateEntry(nextEntries[index]);
      setGateEntries(gateKey, nextEntries);
      refreshOutputs();
    };

    itemSelect.addEventListener("input", syncGateEntry);
    itemSelect.addEventListener("change", syncGateEntry);
    quantityInput.addEventListener("input", syncGateEntry);
    quantityInput.addEventListener("change", syncGateEntry);

    row.querySelector("[data-action='remove']").addEventListener("click", () => {
      const nextEntries = getGateEntries(gateKey);
      nextEntries.splice(index, 1);
      setGateEntries(gateKey, nextEntries);
      renderGateTables();
      refreshOutputs();
    });

    container.appendChild(row);
  });
}

function getGateEntries(gateKey) {
  return (getCurrentFish().pond.populationGates[gateKey] ?? [])
    .map(parseGateEntry)
    .filter(Boolean);
}

function setGateEntries(gateKey, entries) {
  getCurrentFish().pond.populationGates[gateKey] = entries.map(formatGateEntry);
}

function parseGateEntry(value) {
  const text = String(value ?? "").trim();
  if (!text) {
    return null;
  }

  const match = text.match(/^(\([^)]+\)\S+?)(?:\s+(\d+))?$/i);
  if (!match) {
    return {
      itemId: text,
      quantity: 1
    };
  }

  return {
    itemId: match[1],
    quantity: Math.max(1, Number.parseInt(match[2] ?? "1", 10) || 1)
  };
}

function formatGateEntry(entry) {
  const itemId = String(entry?.itemId ?? "(O)388").trim() || "(O)388";
  const quantity = Math.max(1, Number.parseInt(entry?.quantity ?? "1", 10) || 1);
  return `${itemId} ${quantity}`;
}

function updateItemNamePreview(row, itemId) {
  const target = row.querySelector("[data-field='itemName']");
  if (!target) {
    return;
  }

  const resolved = resolveItemLabel(normalizeObjectItemQuery(itemId, ""));
  target.textContent = resolved.code
    ? `${resolved.label} (${resolved.code})`
    : resolved.label;
  target.className = `item-name${resolved.known ? "" : " unknown"}`;
}

function populatePondProductOptions(select, currentValue) {
  const normalizedCurrentValue = normalizeObjectItemQuery(currentValue, "");
  const knownOptions = pondProductCatalog
    .map(code => ({
      code,
      name: resolveItemLabel(code).label
    }))
    .sort((left, right) => left.name.localeCompare(right.name));

  select.innerHTML = "";

  for (const option of knownOptions) {
    const element = document.createElement("option");
    element.value = option.code;
    element.textContent = `${option.name} (${option.code})`;
    select.appendChild(element);
  }

  if (normalizedCurrentValue && !knownOptions.some(option => option.code === normalizedCurrentValue)) {
    const customOption = document.createElement("option");
    customOption.value = normalizedCurrentValue;
    customOption.textContent = `${resolveItemLabel(normalizedCurrentValue).label} (${normalizedCurrentValue})`;
    select.appendChild(customOption);
  }
}

function resolveItemLabel(value) {
  const text = normalizeObjectItemQuery(value, "");
  if (!text) {
    return { label: "Enter an item query like (O)812", known: false, code: "" };
  }

  const match = text.match(/^\(([^)]+)\)(\S+?)(?:\s+(\d+))?$/i);
  if (!match) {
    return { label: "Unrecognized item query format", known: false, code: text };
  }

  const [, rawType, rawId, rawQuantity] = match;
  const type = rawType.toUpperCase();
  const id = rawId;
  const quantity = rawQuantity ? Number.parseInt(rawQuantity, 10) : null;

  if (type === "O" || type === "0") {
    const name = vanillaObjectNames[id];
    if (name) {
      return {
        label: quantity ? `${name} x${quantity}` : name,
        known: true,
        code: `(O)${id}`
      };
    }

    return {
      label: quantity ? `Unknown object #${id} x${quantity}` : `Unknown object #${id}`,
      known: false,
      code: `(O)${id}`
    };
  }

  return { label: `Unsupported item type ${type}`, known: false, code: text };
}

function buildAtlasFilePath() {
  const configured = String(state.source?.objectsTextureFile ?? "").trim();
  return configured || "assets/fishes.png";
}

function buildAtlasFileName() {
  const path = buildAtlasFilePath().split(/[\\/]/).filter(Boolean);
  return path[path.length - 1] || "fishes.png";
}

function buildAquariumAtlasMeta(source, fish, frameIndex) {
  ensureAquariumSlotIndexes(source?.fish ?? []);
  const aquarium = normalizeAquariumData(fish?.aquarium);
  const planEntry = buildAquariumAtlasPlan(source).find(entry => entry.fish === fish);
  const atlasCanvas = buildAquariumAtlasCanvas(source);
  const baseSlot = planEntry ? planEntry.startIndex : (aquarium.slotIndex ?? 0);
  const slot = baseSlot + frameIndex;
  return `Frame ${frameIndex + 1}, slot ${slot} in ${buildAquariumAtlasFileName()} (${atlasCanvas.width}x${atlasCanvas.height})`;
}

function buildAquariumAtlasFilePath() {
  return "assets/aquariumdata.png";
}

function buildAquariumAtlasFileName() {
  return "aquariumdata.png";
}

function getFishDataFileKey(fish) {
  return `data/${sanitizeSlug(fish?.slug ?? "new_fish")}.json`;
}

function getCurrentFishDataFileKey() {
  return getFishDataFileKey(getCurrentFish());
}

function buildAquariumTextureTarget(source) {
  const safe = String(source?.uniqueId ?? "Kree.TOF").replace(/[^A-Za-z0-9]/g, "") || "TOF";
  return `LooseSprites/${safe}AquariumData`;
}

function buildObjectAtlasCanvas(source) {
  const canvas = document.createElement("canvas");
  const fish = source.fish ?? [];
  const maxIndex = Math.max(0, ...fish.map(entry => Math.max(0, readNumericInput(entry.spriteIndex))));
  canvas.width = Math.max(1, maxIndex + 1) * ObjectSpriteAtlasTileWidth;
  canvas.height = ObjectSpriteAtlasTileHeight;
  const context = canvas.getContext("2d");
  context.clearRect(0, 0, canvas.width, canvas.height);

  for (const entry of fish) {
    const spriteIndex = Math.max(0, readNumericInput(entry.spriteIndex));
    const pixels = normalizeSpritePixels(entry.spritePixels, ObjectSpriteWidth, ObjectSpriteHeight);
    pixels.forEach((pixel, index) => {
      if (!pixel) {
        return;
      }

      const x = spriteIndex * ObjectSpriteAtlasTileWidth + (index % ObjectSpriteWidth);
      const y = Math.floor(index / ObjectSpriteWidth);
      context.fillStyle = pixel;
      context.fillRect(x, y, 1, 1);
    });
  }

  return canvas;
}

function buildAquariumAtlasPlan(source) {
  ensureAquariumSlotIndexes(source?.fish ?? []);
  const plan = [];

  for (const fish of source.fish ?? []) {
    const aquarium = normalizeAquariumData(fish.aquarium);
    if (!aquarium.enabled || !aquarium.frames.length) {
      continue;
    }

      const entry = {
        fish,
        aquarium,
        startIndex: aquarium.slotIndex ?? 0,
        frameCount: aquarium.frames.length
      };
      plan.push(entry);
  }

  return plan.sort((left, right) => left.startIndex - right.startIndex);
}

function buildAquariumAtlasCanvas(source) {
  const plan = buildAquariumAtlasPlan(source);
  const canvas = document.createElement("canvas");
  const totalFrames = plan.length
    ? Math.max(...plan.map(entry => entry.startIndex + entry.frameCount))
    : 0;
  const rows = Math.max(1, Math.ceil(Math.max(1, totalFrames) / AquariumAtlasColumns));
  canvas.width = AquariumAtlasColumns * AquariumSpriteAtlasTileWidth;
  canvas.height = rows * AquariumSpriteAtlasTileHeight;
  const context = canvas.getContext("2d");
  context.clearRect(0, 0, canvas.width, canvas.height);

  for (const entry of plan) {
    entry.aquarium.frames.forEach((framePixels, frameIndex) => {
      const globalIndex = entry.startIndex + frameIndex;
      const column = globalIndex % AquariumAtlasColumns;
      const row = Math.floor(globalIndex / AquariumAtlasColumns);
      const pixels = normalizeAquariumFramePixels(framePixels);
      pixels.forEach((pixel, index) => {
        if (!pixel) {
          return;
        }

        const x = column * AquariumSpriteAtlasTileWidth + (index % AquariumSpriteWidth);
        const y = row * AquariumSpriteAtlasTileHeight + Math.floor(index / AquariumSpriteWidth);
        context.fillStyle = pixel;
        context.fillRect(x, y, 1, 1);
      });
    });
  }

  return canvas;
}

async function buildAtlasBlob() {
  const canvas = buildObjectAtlasCanvas(state.source);
  return new Promise(resolve => {
    canvas.toBlob(blob => resolve(blob), "image/png");
  });
}

async function buildAquariumAtlasBlob() {
  const canvas = buildAquariumAtlasCanvas(state.source);
  return new Promise(resolve => {
    canvas.toBlob(blob => resolve(blob), "image/png");
  });
}

function generatePackFiles(source) {
  const manifest = {
    Name: source.packName,
    Author: source.author,
    Version: source.version,
    Description: source.description,
    UniqueID: source.uniqueId,
    MinimumApiVersion: source.minimumApiVersion,
    ContentPackFor: {
      UniqueID: "Pathoschild.ContentPatcher"
    },
    Dependencies: [
      {
        UniqueID: "Kree.DSS"
      }
    ]
  };

  const config = {};
  const i18n = {};
  const aquariumTextureTarget = buildAquariumTextureTarget(source);
  const content = {
    Format: "2.9.0",
    ConfigSchema: {},
    Changes: [
      {
        Action: "Load",
        Target: source.objectsTextureTarget,
        FromFile: source.objectsTextureFile
      },
      {
        Action: "EditData",
        Target: "{{Kree.DSS/Assets}}",
        Entries: {
          [`${source.uniqueId}_Objects`]: [
            {
              Asset: source.objectsTextureTarget
            }
          ]
        }
      }
    ]
  };
  const aquariumPlan = buildAquariumAtlasPlan(source);
  const generated = {};
  const pondMoveEntries = [];

  for (const fish of source.fish) {
    const toggleKey = `Enable${toPascalCase(fish.slug)}`;
    const pondId = `${source.pondPrefix}_${fish.slug}`;
    const aquariumPlanEntry = aquariumPlan.find(entry => entry.fish === fish);

    config[toggleKey] = !!fish.enabledByDefault;
    content.ConfigSchema[toggleKey] = {
      AllowValues: "true, false",
      Default: fish.enabledByDefault ? "true" : "false"
    };

    i18n[`${fish.slug}.name`] = fish.displayName;
    i18n[`${fish.slug}.description`] = fish.description;
    pondMoveEntries.push(
      pondMoveEntries.length === 0
        ? { ID: pondId, ToPosition: "Top" }
        : { ID: pondId, AfterID: pondMoveEntries[pondMoveEntries.length - 1].ID }
    );
    const fishFileKey = getFishDataFileKey(fish);
    generated[fishFileKey] = stringify(buildFishIncludeFile(source, fish, aquariumPlanEntry));
    content.Changes.push({
      Action: "Include",
      FromFile: fishFileKey,
      When: { [toggleKey]: "true" }
    });
  }

  if (pondMoveEntries.length) {
    content.Changes.push({
      Action: "EditData",
      Target: "Data/FishPondData",
      MoveEntries: pondMoveEntries
    });
  }

  if (aquariumPlan.length) {
    content.Changes.splice(1, 0, {
      Action: "Load",
      Target: aquariumTextureTarget,
      FromFile: buildAquariumAtlasFilePath()
    });
  }

  generated["manifest.json"] = stringify(manifest);
  generated["config.json"] = stringify(config);
  generated["content.json"] = stringify(content);
  generated["i18n/default.json"] = stringify(i18n);
  return generated;
}

function buildFishString(fish) {
  return [
    fish.displayName,
    readNumericInput(fish.catch.difficulty),
    fish.catch.behavior,
    readNumericInput(fish.catch.minSize),
    readNumericInput(fish.catch.maxSize),
    `${readNumericInput(fish.catch.timeStart)} ${readNumericInput(fish.catch.timeEnd)}`,
    (fish.catch.seasons || []).join(" "),
    fish.catch.weather,
    fish.catch.waterCode,
    readNumericInput(fish.catch.maxDepth),
    formatNumber(fish.catch.baseChance),
    formatNumber(fish.catch.depthMultiplier),
    readNumericInput(fish.catch.minFishingLevel)
  ].join("/");
}

function getNormalizedSeasonList(fish) {
  const validSeasons = ["spring", "summer", "fall", "winter"];
  return [...new Set((fish.catch.seasons || []).map(season => String(season).trim().toLowerCase()))]
    .filter(season => validSeasons.includes(season));
}

function buildLocationSeasonFields(fish) {
  const seasons = getNormalizedSeasonList(fish);
  if (!seasons.length || seasons.length === 4) {
    return {};
  }

  if (seasons.length === 1) {
    return { Season: seasons[0] };
  }

  return { Condition: `SEASON ${seasons.join(" ")}` };
}

function mapAquariumFieldRefs(rawValue, startIndex, frameCount) {
  const text = String(rawValue ?? "").trim();
  if (!text) {
    return "";
  }

  return text
    .split(/\s+/)
    .map(token => {
      const parsed = Number.parseInt(token, 10);
      if (!Number.isFinite(parsed)) {
        return token;
      }

      const clamped = Math.max(0, Math.min(frameCount - 1, parsed));
      return String(startIndex + clamped);
    })
    .join(" ");
}

function buildAquariumEntryString(planEntry, aquariumTextureTarget) {
  const { aquarium, startIndex, frameCount } = planEntry;
  const texturePath = aquariumTextureTarget.replace(/\//g, "\\");
  return [
    startIndex,
    aquarium.type,
    mapAquariumFieldRefs(aquarium.field3, startIndex, frameCount),
    mapAquariumFieldRefs(aquarium.field4, startIndex, frameCount),
    mapAquariumFieldRefs(aquarium.field5, startIndex, frameCount),
    mapAquariumFieldRefs(aquarium.field6, startIndex, frameCount),
    texturePath
  ].join("/");
}

function buildAquariumEntryPreview(source, fish) {
  const plan = buildAquariumAtlasPlan(source).find(entry => entry.fish === fish);
  if (!plan) {
    return "Enable aquarium export and add at least one frame to preview the final Data/AquariumFish string.";
  }

  return buildAquariumEntryString(plan, buildAquariumTextureTarget(source));
}

function buildFishIncludeFile(source, fish, aquariumPlanEntry) {
  const itemId = `${source.uniqueId}_${fish.slug}`;
  const qualifiedItemId = `(O)${itemId}`;
  const fishTag = `fish_${source.pondPrefix}_${fish.slug}`;
  const pondId = `${source.pondPrefix}_${fish.slug}`;
  const aquariumTextureTarget = buildAquariumTextureTarget(source);
  const file = { Changes: [] };

  file.Changes.push({
    Action: "EditData",
    Target: "Data/Objects",
    Entries: {
      [itemId]: {
        Name: itemId,
        DisplayName: `{{i18n:${fish.slug}.name}}`,
        Description: `{{i18n:${fish.slug}.description}}`,
        Type: "Fish",
        Category: -4,
        Price: readNumericInput(fish.price),
        Texture: source.objectsTextureTarget,
        SpriteIndex: String(readNumericInput(fish.spriteIndex)),
        Edibility: readNumericInput(fish.edibility),
        ContextTags: [...new Set([...normalizeFishContextTags(fish.contextTags || []), fishTag])]
      }
    }
  });

  file.Changes.push({
    Action: "EditData",
    Target: "Data/Fish",
    Entries: {
      [itemId]: buildFishString(fish)
    }
  });

  file.Changes.push({
    Action: "EditData",
    Target: "Data/FishPondData",
    Entries: {
      [pondId]: {
        Id: pondId,
        RequiredTags: [fishTag],
        SpawnTime: readNumericInput(fish.pond.spawnTime),
        WaterColor: [
          {
            Id: "Default",
            Color: fish.pond.waterColor,
            MinPopulation: readNumericInput(fish.pond.waterMinPopulation),
            MinUnlockedPopulationGate: 0,
            Condition: null
          }
        ],
        ProducedItems: sortPondProducts(fish.pond.products).map(product => ({
          RequiredPopulation: readNumericInput(product.requiredPopulation),
          Chance: readFloatInput(product.chance),
          ItemID: normalizeObjectItemQuery(product.itemId),
          MinQuantity: readNumericInput(product.minQuantity),
          MaxQuantity: readNumericInput(product.maxQuantity)
        })),
        PopulationGates: {
          "4": [...(fish.pond.populationGates["4"] || [])],
          "8": [...(fish.pond.populationGates["8"] || [])]
        }
      }
    }
  });

  for (const location of fish.locations) {
    const locationKey = sanitizeLocationKey(location.location);
    file.Changes.push({
      Action: "EditData",
      Target: "Data/Locations",
      TargetField: [location.location, "Fish"],
      Entries: {
        [`${source.uniqueId}_${locationKey}_${toPascalCase(fish.slug)}`]: {
          Id: `${source.uniqueId}_${locationKey}_${toPascalCase(fish.slug)}`,
          ItemId: qualifiedItemId,
          ...buildLocationSeasonFields(fish),
          Chance: readFloatInput(location.chance),
          MinDistanceFromShore: readNumericInput(location.minDistanceFromShore),
          MaxDistanceFromShore: readNumericInput(location.maxDistanceFromShore),
          Precedence: readNumericInput(location.precedence),
          CatchLimit: fish.isLegendary ? 1 : -1,
          IsBossFish: !!fish.isLegendary,
          CanUseTrainingRod: fish.isLegendary ? false : null
        }
      }
    });
  }

  if (aquariumPlanEntry) {
    file.Changes.push({
      Action: "EditData",
      Target: "Data/AquariumFish",
      Entries: {
        [itemId]: buildAquariumEntryString(aquariumPlanEntry, aquariumTextureTarget)
      }
    });
  }

  return file;
}

function validateSource(source) {
  const warnings = [];
  const slugMap = new Map();
  const spriteIndexMap = new Map();
  const aquariumRanges = [];

  for (const fish of source.fish ?? []) {
    const slug = sanitizeSlug(fish.slug);
    const displayName = fish.displayName || slug;
    const spriteIndex = Math.max(0, readNumericInput(fish.spriteIndex));
    const aquarium = normalizeAquariumData(fish.aquarium);
    const startIndex = aquarium.slotIndex ?? 0;
    const frameCount = aquarium.frames.length || 1;

    if (slugMap.has(slug)) {
      warnings.push(`Duplicate slug: ${slug} (${slugMap.get(slug)} and ${displayName}).`);
    } else {
      slugMap.set(slug, displayName);
    }

    if (spriteIndexMap.has(spriteIndex)) {
      warnings.push(`Duplicate item sprite slot ${spriteIndex}: ${spriteIndexMap.get(spriteIndex)} and ${displayName}.`);
    } else {
      spriteIndexMap.set(spriteIndex, displayName);
    }

    if (!fish.locations?.length) {
      warnings.push(`${displayName} has no fishing locations.`);
    }

    if (aquarium.enabled && !aquarium.frames.length) {
      warnings.push(`${displayName} has aquarium export enabled but no aquarium frames.`);
    }

    for (const entry of aquariumRanges) {
      if (rangesOverlap(startIndex, frameCount, entry.startIndex, entry.frameCount)) {
        warnings.push(`Aquarium slot overlap: ${displayName} (${startIndex}-${startIndex + frameCount - 1}) overlaps ${entry.name} (${entry.startIndex}-${entry.startIndex + entry.frameCount - 1}).`);
      }
    }

    aquariumRanges.push({ name: displayName, startIndex, frameCount });
  }

  return warnings;
}

function formatValidationReport(warnings) {
  if (!warnings.length) {
    return "No validation warnings.";
  }

  return warnings.map((warning, index) => `${index + 1}. ${warning}`).join("\n");
}

function sanitizeSlug(value) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "") || "new_fish";
}

function sanitizeLocationKey(value) {
  return String(value).replace(/[^A-Za-z0-9]+/g, "");
}

function toPascalCase(value) {
  return sanitizeSlug(value)
    .split("_")
    .filter(Boolean)
    .map(part => part[0].toUpperCase() + part.slice(1))
    .join("");
}

function parseCommaList(value) {
  return value
    .split(",")
    .map(part => part.trim())
    .filter(Boolean);
}

function addCommonContextTag(tag) {
  const nextTag = String(tag ?? "").trim();
  if (!nextTag) {
    return;
  }

  const fish = getCurrentFish();
  const tags = new Set(parseCommaList($.contextTags.value));
  tags.add(nextTag);
  const nextValue = [...tags].join(", ");
  $.contextTags.value = nextValue;
    fish.contextTags = normalizeFishContextTags([...tags]);
  refreshOutputs();
}

function renderContextTagButtons() {
  if (!contextTagButtons) {
    return;
  }

  contextTagButtons.innerHTML = "";
  for (const tag of commonContextTags) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "tag-button";
    button.textContent = `+ ${tag}`;
    button.addEventListener("click", () => addCommonContextTag(tag));
    contextTagButtons.appendChild(button);
  }
}

function parseLineList(value) {
  return value
    .split(/\r?\n/)
    .map(part => part.trim())
    .filter(Boolean);
}

function readNumericInput(value) {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : 0;
}

function readFloatInput(value) {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function updateEdibilityEstimate(rawEdibility) {
  const edibility = readNumericInput(rawEdibility);
  const qualities = [
    { label: "normal", value: 0 },
    { label: "silver", value: 1 },
    { label: "gold", value: 2 },
    { label: "iridium", value: 4 }
  ];
  const values = qualities.map(quality => {
    const energy = Math.floor(Math.ceil(edibility * 2.5) + edibility * quality.value);
    const health = Math.max(0, Math.floor(energy * 0.45));
    return `${quality.label} ${energy} energy, ${health} health`;
  });
  const prefix = edibility < 0 ? "Est. effect:" : "Est. restore:";
  $.edibilityEstimate.textContent = `${prefix} ${values.join("; ")}.`;
}

function updatePriceEstimate(rawPrice) {
  const price = Math.max(0, readNumericInput(rawPrice));
  const silver = Math.floor(price * 1.25);
  const gold = Math.floor(price * 1.5);
  const iridium = Math.floor(price * 2);
  $.priceEstimate.textContent = `Est. sell value: normal ${price}g, silver ${silver}g, gold ${gold}g, iridium ${iridium}g.`;
}

function rgbStringToHex(value) {
  const match = String(value).trim().match(/^(\d{1,3})\s+(\d{1,3})\s+(\d{1,3})$/);
  if (!match) {
    return "#c8e1eb";
  }

  const parts = match.slice(1).map(part => {
    const num = Math.max(0, Math.min(255, Number.parseInt(part, 10) || 0));
    return num.toString(16).padStart(2, "0");
  });

  return `#${parts.join("")}`;
}

function hexToRgbString(value) {
  const hex = String(value).replace("#", "");
  if (hex.length !== 6) {
    return "200 225 235";
  }

  const parts = [
    Number.parseInt(hex.slice(0, 2), 16),
    Number.parseInt(hex.slice(2, 4), 16),
    Number.parseInt(hex.slice(4, 6), 16)
  ];

  return parts.join(" ");
}

function normalizeRgbString(value) {
  const match = String(value).trim().match(/^(\d{1,3})[,\s]+(\d{1,3})[,\s]+(\d{1,3})$/);
  if (!match) {
    return "200 225 235";
  }

  const parts = match.slice(1).map(part => Math.max(0, Math.min(255, Number.parseInt(part, 10) || 0)));
  return parts.join(" ");
}

function rgbToHex(r, g, b) {
  return `#${[r, g, b].map(value => value.toString(16).padStart(2, "0")).join("")}`;
}

function updatePondColorPreview(value, hexValue = null) {
  const color = hexValue || rgbStringToHex(value);
  $.pondWaterColorPreview.style.background = color;
  $.pondWaterColorPreview.style.backgroundColor = color;
  $.pondWaterColorPreview.textContent = value;
}

function formatNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? String(parsed) : "0";
}

function stringify(value) {
  return JSON.stringify(value, null, 2);
}

function downloadGenerated(fileName, key = fileName) {
  const generated = generatePackFiles(state.source);
  downloadText(fileName.split("/").pop(), generated[key]);
}

function downloadCurrentFishDataFile() {
  const key = getCurrentFishDataFileKey();
  downloadGenerated(key.split("/").pop(), key);
}

async function downloadAtlas() {
  const isAquarium = state.spriteMode === "aquarium";
  const blob = isAquarium ? await buildAquariumAtlasBlob() : await buildAtlasBlob();
  if (!blob) {
    alert(`Could not build the ${isAquarium ? "aquarium " : ""}atlas PNG.`);
    return;
  }

  downloadBlob(isAquarium ? buildAquariumAtlasFileName() : buildAtlasFileName(), blob);
}

async function downloadObjectAtlas() {
  const blob = await buildAtlasBlob();
  if (!blob) {
    alert("Could not build the atlas PNG.");
    return;
  }

  downloadBlob(buildAtlasFileName(), blob);
}

async function downloadAquariumAtlas() {
  const blob = await buildAquariumAtlasBlob();
  if (!blob) {
    alert("Could not build the aquarium atlas PNG.");
    return;
  }

  downloadBlob(buildAquariumAtlasFileName(), blob);
}

function downloadText(fileName, text) {
  const blob = new Blob([text], { type: "application/json" });
  downloadBlob(fileName, blob);
}

function downloadBlob(fileName, blob) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

async function openSourceFile() {
  if (!window.showOpenFilePicker) {
    alert("Your browser does not support the File System Access API. Use download and drag-drop instead.");
    return;
  }

  if (state.sourceHandle) {
    try {
      const rememberedFile = await state.sourceHandle.getFile();
      state.source = normalizeSource(JSON.parse(await rememberedFile.text()));
      state.sourceName = rememberedFile.name || state.sourceHandle.name || "fishes.json";
      state.selectedIndex = 0;
      renderAll();
      return;
    } catch {
      // If the remembered file handle is no longer valid, fall back to the picker.
    }
  }

  const pickerOptions = {
    id: openPickerId,
    types: [
      {
        description: "Fish source JSON",
        accept: { "application/json": [".json"] }
      }
    ],
    multiple: false
  };

  if (state.sourceHandle) {
    pickerOptions.startIn = state.sourceHandle;
  }

  const [handle] = await window.showOpenFilePicker(pickerOptions);

  const file = await handle.getFile();
  state.source = normalizeSource(JSON.parse(await file.text()));
  state.sourceHandle = handle;
  state.sourceName = file.name || handle.name || "fishes.json";
  await storeSourceHandle(handle);
  state.selectedIndex = 0;
  renderAll();
}

async function saveSourceFile() {
  if (!window.showSaveFilePicker && !state.sourceHandle) {
    alert("Your browser does not support direct saving. Use Download fishes.json instead.");
    return;
  }

  let handle = state.sourceHandle;
  if (!handle) {
    const pickerOptions = {
      id: openPickerId,
      suggestedName: "fishes.json",
      types: [
        {
          description: "Fish source JSON",
          accept: { "application/json": [".json"] }
        }
      ]
    };

    if (state.sourceHandle) {
      pickerOptions.startIn = state.sourceHandle;
    }

    handle = await window.showSaveFilePicker(pickerOptions);
    state.sourceHandle = handle;
    state.sourceName = handle.name || "fishes.json";
    await storeSourceHandle(handle);
  }

  const writable = await handle.createWritable();
  await writable.truncate(0);
  await writable.write(stringify(state.source));
  await writable.close();
  persistSnapshots();
}

async function autoSaveSourceIfPossible() {
  if (!state.sourceHandle || !state.source) {
    return;
  }

  try {
    const writable = await state.sourceHandle.createWritable();
    await writable.truncate(0);
    await writable.write(stringify(state.source));
    await writable.close();
  } catch {
    // Ignore silent autosave failures. Manual Save fishes.json remains available.
  }
}

async function writePackFiles() {
  try {
    if (!window.showDirectoryPicker) {
      alert("Your browser does not support direct folder writes. Use the download buttons instead.");
      return;
    }

    const generated = generatePackFiles(state.source);
    const dir = await window.showDirectoryPicker();
    for (const [path, contents] of Object.entries(generated)) {
      await writeNestedFile(dir, path, contents);
    }

    const atlasBlob = await buildAtlasBlob();
    if (atlasBlob) {
      await writeNestedFile(dir, buildAtlasFilePath(), atlasBlob);
    }

    const aquariumAtlasBlob = await buildAquariumAtlasBlob();
    if (aquariumAtlasBlob && buildAquariumAtlasPlan(state.source).length) {
      await writeNestedFile(dir, buildAquariumAtlasFilePath(), aquariumAtlasBlob);
    }

    alert("Generated pack files and atlas PNGs were written to the selected folder.");
  } catch (error) {
    console.error("TOF write-pack failed", error);
    const message = error && typeof error === "object" && "message" in error
      ? String(error.message)
      : String(error ?? "Unknown error");
    alert(`Could not write generated pack files.\n\n${message}`);
  }
}

async function writeFile(directoryHandle, fileName, contents) {
  const handle = await directoryHandle.getFileHandle(fileName, { create: true });
  const writable = await handle.createWritable();
  await writable.truncate(0);
  await writable.write(contents);
  await writable.close();
}

async function writeNestedFile(rootDirectoryHandle, relativePath, contents) {
  const parts = String(relativePath)
    .split(/[\\/]/)
    .map(part => part.trim())
    .filter(Boolean);
  if (!parts.length) {
    return;
  }

  let directory = rootDirectoryHandle;
  for (const part of parts.slice(0, -1)) {
    directory = await directory.getDirectoryHandle(part, { create: true });
  }

  await writeFile(directory, parts[parts.length - 1], contents);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}

async function tryRestoreSourceHandle() {
  try {
    const handle = await loadStoredSourceHandle();
    if (!handle) {
      return false;
    }

    let permission = await handle.queryPermission({ mode: "read" });
    if (permission !== "granted") {
      permission = await handle.requestPermission({ mode: "read" });
    }

    if (permission !== "granted") {
      return false;
    }

    const file = await handle.getFile();
    state.source = normalizeSource(JSON.parse(await file.text()));
    state.sourceHandle = handle;
    state.sourceName = file.name || handle.name || "fishes.json";
    state.selectedIndex = 0;
    return true;
  } catch {
    return false;
  }
}

function tryRestoreLocalSnapshot() {
  try {
    const raw = localStorage.getItem(localSnapshotKey);
    if (!raw) {
      return false;
    }

    state.source = normalizeSource(JSON.parse(raw));
    state.sourceName = localStorage.getItem(localSnapshotNameKey) || "fishes.json";
    state.selectedIndex = 0;
    return true;
  } catch {
    return false;
  }
}

async function tryRestoreIndexedDbSnapshot() {
  try {
    const snapshot = await loadStoredSnapshot();
    if (!snapshot?.source) {
      return false;
    }

    state.source = normalizeSource(snapshot.source);
    state.sourceName = snapshot.name || "fishes.json";
    state.selectedIndex = 0;
    return true;
  } catch {
    return false;
  }
}

function persistSnapshots() {
  persistLocalSnapshot();
  void persistIndexedDbSnapshot();
}

function persistLocalSnapshot() {
  try {
    if (!state.source) {
      return;
    }

    localStorage.setItem(localSnapshotKey, stringify(state.source));
    localStorage.setItem(localSnapshotNameKey, state.sourceName || "fishes.json");
  } catch {
    // Ignore storage quota or browser support failures.
  }
}

async function persistIndexedDbSnapshot() {
  try {
    if (!state.source) {
      return;
    }

    const db = await openHandleDb();
    const payload = {
      name: state.sourceName || "fishes.json",
      source: state.source
    };

    await new Promise((resolve, reject) => {
      const tx = db.transaction(handleStoreName, "readwrite");
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
      tx.objectStore(handleStoreName).put(payload, snapshotKey);
    });
  } catch {
    // Ignore browser support or quota failures.
  }
}

async function loadStoredSnapshot() {
  if (!window.indexedDB) {
    return null;
  }

  const db = await openHandleDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(handleStoreName, "readonly");
    const request = tx.objectStore(handleStoreName).get(snapshotKey);
    request.onsuccess = () => resolve(request.result ?? null);
    request.onerror = () => reject(request.error);
  });
}

async function storeSourceHandle(handle) {
  if (!window.indexedDB || !handle) {
    return;
  }

  const db = await openHandleDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(handleStoreName, "readwrite");
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
    tx.objectStore(handleStoreName).put(handle, handleKey);
  });
}

async function loadStoredSourceHandle() {
  if (!window.indexedDB) {
    return null;
  }

  const db = await openHandleDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(handleStoreName, "readonly");
    const request = tx.objectStore(handleStoreName).get(handleKey);
    request.onsuccess = () => resolve(request.result ?? null);
    request.onerror = () => reject(request.error);
  });
}

async function openHandleDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(handleDbName, 1);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(handleStoreName)) {
        db.createObjectStore(handleStoreName);
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}
