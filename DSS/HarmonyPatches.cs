using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace DSS;

internal static class HarmonyPatches
{
    private static DoubleResRegistry registry = null!;
    private static bool isDrawingReplacement;

    public static void Initialize(DoubleResRegistry registryInstance)
    {
        registry = registryInstance;
    }

    public static void PatchAll(string uniqueId)
    {
        Harmony harmony = new(uniqueId);
        Type spriteBatchType = typeof(SpriteBatch);

        harmony.Patch(
            original: AccessTools.Method(spriteBatchType, nameof(SpriteBatch.Draw), new[]
            {
                typeof(Texture2D),
                typeof(Vector2),
                typeof(Rectangle?),
                typeof(Color),
                typeof(float),
                typeof(Vector2),
                typeof(Vector2),
                typeof(SpriteEffects),
                typeof(float)
            }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawWithVectorScale))
        );

        harmony.Patch(
            original: AccessTools.Method(spriteBatchType, nameof(SpriteBatch.Draw), new[]
            {
                typeof(Texture2D),
                typeof(Vector2),
                typeof(Rectangle?),
                typeof(Color),
                typeof(float),
                typeof(Vector2),
                typeof(float),
                typeof(SpriteEffects),
                typeof(float)
            }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawWithFloatScale))
        );

        harmony.Patch(
            original: AccessTools.Method(spriteBatchType, nameof(SpriteBatch.Draw), new[]
            {
                typeof(Texture2D),
                typeof(Rectangle),
                typeof(Rectangle?),
                typeof(Color),
                typeof(float),
                typeof(Vector2),
                typeof(SpriteEffects),
                typeof(float)
            }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawWithRectangle))
        );

        harmony.Patch(
            original: AccessTools.Method(spriteBatchType, nameof(SpriteBatch.Draw), new[]
            {
                typeof(Texture2D),
                typeof(Vector2),
                typeof(Rectangle?),
                typeof(Color)
            }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawSimpleWithSource))
        );

        harmony.Patch(
            original: AccessTools.Method(spriteBatchType, nameof(SpriteBatch.Draw), new[]
            {
                typeof(Texture2D),
                typeof(Rectangle),
                typeof(Rectangle?),
                typeof(Color)
            }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawRectangleWithSource))
        );

        harmony.Patch(
            original: AccessTools.Method(spriteBatchType, nameof(SpriteBatch.Draw), new[]
            {
                typeof(Texture2D),
                typeof(Vector2),
                typeof(Color)
            }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawSimple))
        );

        harmony.Patch(
            original: AccessTools.Method(spriteBatchType, nameof(SpriteBatch.Draw), new[]
            {
                typeof(Texture2D),
                typeof(Rectangle),
                typeof(Color)
            }),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DrawRectangle))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getSourceRectForStandardTileSheet)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GetSourceRectForStandardTileSheet))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getSquareSourceRectForNonStandardTileSheet)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GetSquareSourceRectForNonStandardTileSheet))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getArbitrarySourceRect)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GetArbitrarySourceRect))
        );
    }

    public static bool DrawWithVectorScale(SpriteBatch __instance, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
    {
        if (!TryGetDrawData(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return true;
        }

        return DrawScaledVector(__instance, scaledTexture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth, definition);
    }

    public static bool DrawWithFloatScale(SpriteBatch __instance, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
    {
        if (!TryGetDrawData(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return true;
        }

        return DrawScaledVector(__instance, scaledTexture, position, sourceRectangle, color, rotation, origin, new Vector2(scale, scale), effects, layerDepth, definition);
    }

    public static bool DrawWithRectangle(SpriteBatch __instance, Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
    {
        if (!TryGetDrawData(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return true;
        }

        return DrawScaledRectangle(__instance, scaledTexture, destinationRectangle, sourceRectangle, color, rotation, origin, effects, layerDepth, definition);
    }

    public static bool DrawSimpleWithSource(SpriteBatch __instance, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
    {
        if (!TryGetDrawData(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return true;
        }

        return DrawScaledVector(__instance, scaledTexture, position, sourceRectangle, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f, definition);
    }

    public static bool DrawRectangleWithSource(SpriteBatch __instance, Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color)
    {
        if (!TryGetDrawData(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return true;
        }

        return DrawScaledRectangle(__instance, scaledTexture, destinationRectangle, sourceRectangle, color, 0f, Vector2.Zero, SpriteEffects.None, 0f, definition);
    }

    public static bool DrawSimple(SpriteBatch __instance, Texture2D texture, Vector2 position, Color color)
    {
        if (!TryGetDrawData(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return true;
        }

        return DrawScaledVector(__instance, scaledTexture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f, definition);
    }

    public static bool DrawRectangle(SpriteBatch __instance, Texture2D texture, Rectangle destinationRectangle, Color color)
    {
        if (!TryGetDrawData(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return true;
        }

        return DrawScaledRectangle(__instance, scaledTexture, destinationRectangle, null, color, 0f, Vector2.Zero, SpriteEffects.None, 0f, definition);
    }

    public static void GetSourceRectForStandardTileSheet(ref Texture2D tileSheet, int tilePosition, ref int width, ref int height)
    {
        ReplaceWithProxyTexture(ref tileSheet);
    }

    public static void GetSquareSourceRectForNonStandardTileSheet(ref Texture2D tileSheet, int tilePosition, ref int tileWidth, ref int tileHeight)
    {
        ReplaceWithProxyTexture(ref tileSheet);
    }

    public static void GetArbitrarySourceRect(ref Texture2D tileSheet, int tilePosition, ref int tileWidth, ref int tileHeight)
    {
        ReplaceWithProxyTexture(ref tileSheet);
    }

    private static void ReplaceWithProxyTexture(ref Texture2D tileSheet)
    {
        if (registry.TryGetProxyTexture(tileSheet, out Texture2D proxyTexture))
        {
            tileSheet = proxyTexture;
        }
    }

    private static bool TryGetDrawData(Texture2D texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture)
    {
        definition = null;
        scaledTexture = texture;

        if (isDrawingReplacement)
        {
            return false;
        }

        return registry.TryGetDefinition(texture, out definition, out scaledTexture) && definition is not null;
    }

    private static bool DrawScaledVector(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth, DoubleResAssetDefinition definition)
    {
        Rectangle? scaledSource = sourceRectangle.HasValue
            ? registry.ScaleSourceRect(definition, sourceRectangle.Value)
            : null;

        Vector2 adjustedScale = registry.ScaleDrawScale(definition, scale);
        Vector2 adjustedOrigin = registry.ScaleOrigin(definition, origin);

        isDrawingReplacement = true;
        try
        {
            spriteBatch.Draw(texture, position, scaledSource, color, rotation, adjustedOrigin, adjustedScale, effects, layerDepth);
        }
        finally
        {
            isDrawingReplacement = false;
        }

        return false;
    }

    private static bool DrawScaledRectangle(SpriteBatch spriteBatch, Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth, DoubleResAssetDefinition definition)
    {
        Rectangle? scaledSource = sourceRectangle.HasValue
            ? registry.ScaleSourceRect(definition, sourceRectangle.Value)
            : null;

        Vector2 adjustedOrigin = registry.ScaleOrigin(definition, origin);

        isDrawingReplacement = true;
        try
        {
            spriteBatch.Draw(texture, destinationRectangle, scaledSource, color, rotation, adjustedOrigin, effects, layerDepth);
        }
        finally
        {
            isDrawingReplacement = false;
        }

        return false;
    }
}
