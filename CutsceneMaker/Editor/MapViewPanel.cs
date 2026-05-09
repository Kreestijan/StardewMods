using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using xTile.Dimensions;
using xTile.Layers;

namespace CutsceneMaker.Editor;

public sealed class MapViewPanel : EditorPanel
{
    private static readonly Point DefaultSpriteSize = new(16, 32);

    private readonly EditorState state;
    private readonly Dictionary<string, Texture2D?> actorTextures = new(StringComparer.OrdinalIgnoreCase);
    private Vector2 panPixels = Vector2.Zero;
    private bool isPanning;
    private Point lastPanCursor;
    private bool loggedMapDrawFailure;
    private RenderTarget2D? mapRenderTarget;
    private NpcPlacement? draggedActor;
    private GameLocation? lastDrawnLocation;

    public MapViewPanel(EditorState state)
        : base("Map View")
    {
        this.state = state;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        Microsoft.Xna.Framework.Rectangle contentBounds = this.GetContentBounds();

        if (this.state.BootstrappedMap is null)
        {
            string reason = string.IsNullOrWhiteSpace(this.state.MapLoadFailureMessage)
                ? "No loader reason was recorded."
                : this.state.MapLoadFailureMessage;
            this.DrawWarning(spriteBatch, contentBounds, $"{reason}\nPlay mode disabled.");
            return;
        }

        if (!ReferenceEquals(this.lastDrawnLocation, this.state.BootstrappedMap))
        {
            this.lastDrawnLocation = this.state.BootstrappedMap;
            this.panPixels = Vector2.Zero;
            this.isPanning = false;
            this.draggedActor = null;
        }

        this.DrawMapToRenderTarget(spriteBatch, contentBounds);
        if (this.state.Mode == EditorMode.Play)
        {
            this.DrawPreviewEmotes(spriteBatch, contentBounds);
        }

        this.DrawCursorTile(spriteBatch, contentBounds);
        this.DrawPlaybackFadeOverlay(spriteBatch, contentBounds);
    }

    public override void ReceiveLeftClick(int x, int y)
    {
        Microsoft.Xna.Framework.Rectangle contentBounds = this.GetContentBounds();
        if (!contentBounds.Contains(x, y))
        {
            return;
        }

        if (this.TryStartActorDrag(contentBounds, x, y))
        {
            return;
        }

        Point tile = this.ScreenToTile(contentBounds, x, y);
        if (this.state.SelectedCommandIndex == -1)
        {
            this.state.Cutscene.FarmerPlacement.TileX = tile.X;
            this.state.Cutscene.FarmerPlacement.TileY = tile.Y;
            this.state.IsDirty = true;
            return;
        }

        if (this.state.SelectedCommandIndex >= 0
            && this.state.SelectedCommandIndex < this.state.Cutscene.Commands.Count
            && this.state.Cutscene.Commands[this.state.SelectedCommandIndex] is TimelineCommand { Type: CommandType.Move } command)
        {
            command.TileX = tile.X;
            command.TileY = tile.Y;
            this.state.IsDirty = true;
        }
    }

    public override void ReceiveRightClick(int x, int y)
    {
        if (!this.GetContentBounds().Contains(x, y))
        {
            return;
        }

        this.isPanning = true;
        this.lastPanCursor = new Point(x, y);
    }

    public override void ReceiveScrollWheelAction(int direction)
    {
    }

    public override void Update()
    {
        if (this.draggedActor is not null)
        {
            this.UpdateActorDrag();
            return;
        }

        if (!this.isPanning)
        {
            return;
        }

        MouseState mouse = Mouse.GetState();
        if (mouse.RightButton != ButtonState.Pressed)
        {
            this.isPanning = false;
            return;
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        Point delta = cursor - this.lastPanCursor;
        if (delta == Point.Zero)
        {
            return;
        }

        this.panPixels -= new Vector2(delta.X, delta.Y);
        this.lastPanCursor = cursor;
    }

    private void DrawMap(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds)
    {
        GameLocation location = this.state.BootstrappedMap!;
        xTile.Dimensions.Rectangle previousViewport = Game1.viewport;
        GameLocation? previousLocation = Game1.currentLocation;
        bool sceneStarted = false;

        try
        {
            Game1.currentLocation = location;
            Game1.viewport = new xTile.Dimensions.Rectangle(
                (int)Math.Round(this.panPixels.X),
                (int)Math.Round(this.panPixels.Y),
                contentBounds.Width,
                contentBounds.Height
            );

            sceneStarted = this.TryBeginMapScene(spriteBatch);
            this.DrawMapLayers(location.backgroundLayers, sortBaseOffset: -1f);
            this.DrawMapLayers(location.buildingLayers, sortBaseOffset: 0f);
            location.draw(spriteBatch);
            if (this.state.Mode == EditorMode.Play)
            {
                location.currentEvent?.draw(spriteBatch);
            }

            this.DrawMapLayers(location.frontLayers, sortBaseOffset: 64f);
            this.DrawMapLayers(location.alwaysFrontLayers, sortBaseOffset: -1f);
            if (this.state.Mode == EditorMode.Play)
            {
                location.currentEvent?.drawAfterMap(spriteBatch);
            }
        }
        catch (Exception ex)
        {
            this.state.MapLoadFailureMessage = $"Map draw failed: {FormatException(ex)}";
            if (!this.loggedMapDrawFailure)
            {
                this.loggedMapDrawFailure = true;
                ModEntry.Instance.Monitor.Log($"Cutscene Maker map draw failed: {ex}", LogLevel.Warn);
            }

            this.DrawWarning(spriteBatch, contentBounds, $"{this.state.MapLoadFailureMessage}\nPlay mode disabled.");
        }
        finally
        {
            if (sceneStarted)
            {
                Game1.mapDisplayDevice.EndScene();
            }

            Game1.viewport = previousViewport;
            Game1.currentLocation = previousLocation;
        }
    }

    private void DrawPreviewEmotes(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds)
    {
        Event? currentEvent = this.state.BootstrappedMap?.currentEvent;
        if (currentEvent is null || this.state.PreviewEmotes.Count == 0)
        {
            return;
        }

        xTile.Dimensions.Rectangle viewport = new(
            (int)Math.Round(this.panPixels.X),
            (int)Math.Round(this.panPixels.Y),
            contentBounds.Width,
            contentBounds.Height
        );

        foreach (PreviewEmote emote in this.state.PreviewEmotes)
        {
            NPC? actor = currentEvent.getActorByName(emote.ActorName);
            Vector2? actorLocalPosition = actor?.getLocalPosition(viewport);
            int spriteWidth = actor?.Sprite?.SourceRect.Width ?? 16;

            if (actorLocalPosition is null && this.TryGetEditorActorPosition(emote.ActorName, viewport, out Vector2 editorPosition))
            {
                actorLocalPosition = editorPosition;
            }

            if (actorLocalPosition is null)
            {
                continue;
            }

            Vector2 position = new(contentBounds.X + actorLocalPosition.Value.X, contentBounds.Y + actorLocalPosition.Value.Y);
            position.Y -= 96f;
            position.X += spriteWidth * Game1.pixelZoom / 2f - 32f;

            Microsoft.Xna.Framework.Rectangle source = new(
                emote.Frame * 16 % Game1.emoteSpriteSheet.Width,
                emote.Frame * 16 / Game1.emoteSpriteSheet.Width * 16,
                16,
                16
            );

            Microsoft.Xna.Framework.Rectangle destination = new((int)position.X, (int)position.Y, 16 * Game1.pixelZoom, 16 * Game1.pixelZoom);
            if (!destination.Intersects(contentBounds))
            {
                continue;
            }

            spriteBatch.Draw(Game1.emoteSpriteSheet, position, source, Color.White, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 1f);
        }
    }

    private void DrawPlaybackFadeOverlay(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds)
    {
        if (this.state.Mode != EditorMode.Play || (!Game1.globalFade && !Game1.fadeToBlack))
        {
            return;
        }

        float alpha = Math.Clamp(Game1.fadeToBlackAlpha, 0f, 1f);
        if (alpha <= 0f)
        {
            return;
        }

        spriteBatch.Draw(Game1.fadeToBlackRect, contentBounds, Color.Black * alpha);
    }

    private bool TryGetEditorActorPosition(string actorName, xTile.Dimensions.Rectangle viewport, out Vector2 position)
    {
        NpcPlacement? placement = this.state.Cutscene.Actors
            .FirstOrDefault(actor => actor.ActorName.Equals(actorName, StringComparison.OrdinalIgnoreCase));

        if (placement is null)
        {
            position = Vector2.Zero;
            return false;
        }

        position = new Vector2(placement.TileX * Game1.tileSize - viewport.X, placement.TileY * Game1.tileSize - viewport.Y);
        return true;
    }

    private void DrawMapToRenderTarget(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds)
    {
        GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
        this.EnsureRenderTarget(graphicsDevice, contentBounds.Width, contentBounds.Height);
        RenderTargetBinding[] previousRenderTargets = graphicsDevice.GetRenderTargets();
        Microsoft.Xna.Framework.Rectangle targetBounds = new(0, 0, contentBounds.Width, contentBounds.Height);

        spriteBatch.End();
        graphicsDevice.SetRenderTarget(this.mapRenderTarget);
        graphicsDevice.Clear(Color.Transparent);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        try
        {
            this.DrawMap(spriteBatch, targetBounds);
            if (this.state.Mode != EditorMode.Play)
            {
                this.DrawActorPlaceholders(spriteBatch, targetBounds);
            }
        }
        finally
        {
            spriteBatch.End();
            graphicsDevice.SetRenderTargets(previousRenderTargets);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        spriteBatch.Draw(this.mapRenderTarget, contentBounds, Color.White);
    }

    private void DrawMapLayers(List<KeyValuePair<Layer, int>> layers, float sortBaseOffset)
    {
        if (Game1.mapDisplayDevice is null)
        {
            return;
        }

        foreach (KeyValuePair<Layer, int> layer in layers)
        {
            layer.Key.Draw(
                Game1.mapDisplayDevice,
                Game1.viewport,
                Location.Origin,
                wrapAround: false,
                Game1.pixelZoom,
                sortBaseOffset + layer.Value
            );
        }
    }

    private bool TryBeginMapScene(SpriteBatch spriteBatch)
    {
        if (Game1.mapDisplayDevice is null)
        {
            return false;
        }

        Game1.mapDisplayDevice.BeginScene(spriteBatch);
        return true;
    }

    private void EnsureRenderTarget(GraphicsDevice graphicsDevice, int width, int height)
    {
        if (this.mapRenderTarget is not null
            && this.mapRenderTarget.Width == width
            && this.mapRenderTarget.Height == height
            && !this.mapRenderTarget.IsDisposed)
        {
            return;
        }

        this.mapRenderTarget?.Dispose();
        this.mapRenderTarget = new RenderTarget2D(
            graphicsDevice,
            Math.Max(1, width),
            Math.Max(1, height),
            mipMap: false,
            SurfaceFormat.Color,
            DepthFormat.None
        );
    }

    private void DrawActorPlaceholders(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds)
    {
        this.DrawActor(spriteBatch, contentBounds, this.state.Cutscene.FarmerPlacement, Color.DodgerBlue, "Characters\\Farmer\\farmer_base", DefaultSpriteSize);
        foreach (NpcPlacement actor in this.state.Cutscene.Actors)
        {
            this.DrawActor(
                spriteBatch,
                contentBounds,
                actor,
                Color.OrangeRed,
                GetNpcSpriteAsset(actor.ActorName),
                GetNpcSpriteSize(actor.ActorName)
            );
        }
    }

    private Microsoft.Xna.Framework.Rectangle GetContentBounds()
    {
        return new Microsoft.Xna.Framework.Rectangle(
            this.Bounds.X + 16,
            this.Bounds.Y + 48,
            this.Bounds.Width - 32,
            this.Bounds.Height - 68
        );
    }

    private void DrawActor(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds, NpcPlacement actor, Color fallbackColor, string textureAssetName, Point sourceSize)
    {
        Microsoft.Xna.Framework.Rectangle bounds = this.GetActorBounds(contentBounds, actor, sourceSize);
        Texture2D? texture = this.GetActorTexture(textureAssetName);

        if (texture is not null && texture.Width >= sourceSize.X && texture.Height >= sourceSize.Y)
        {
            Microsoft.Xna.Framework.Rectangle sourceRect = this.GetStandingSourceRect(texture, sourceSize, actor.Facing, out SpriteEffects effects);
            spriteBatch.Draw(texture, bounds, sourceRect, Color.White, 0f, Vector2.Zero, effects, 0f);
            return;
        }

        spriteBatch.Draw(Game1.staminaRect, bounds, fallbackColor * 0.8f);
    }

    private Texture2D? GetActorTexture(string assetName)
    {
        if (this.actorTextures.TryGetValue(assetName, out Texture2D? cached))
        {
            return cached;
        }

        try
        {
            cached = Game1.content.Load<Texture2D>(assetName);
        }
        catch
        {
            cached = null;
        }

        this.actorTextures[assetName] = cached;
        return cached;
    }

    private bool TryStartActorDrag(Microsoft.Xna.Framework.Rectangle contentBounds, int x, int y)
    {
        for (int i = this.state.Cutscene.Actors.Count - 1; i >= 0; i--)
        {
            NpcPlacement actor = this.state.Cutscene.Actors[i];
            if (this.GetActorBounds(contentBounds, actor, GetNpcSpriteSize(actor.ActorName)).Contains(x, y))
            {
                this.draggedActor = actor;
                return true;
            }
        }

        if (this.GetActorBounds(contentBounds, this.state.Cutscene.FarmerPlacement, DefaultSpriteSize).Contains(x, y))
        {
            this.draggedActor = this.state.Cutscene.FarmerPlacement;
            return true;
        }

        return false;
    }

    private void UpdateActorDrag()
    {
        NpcPlacement? actor = this.draggedActor;
        if (actor is null)
        {
            return;
        }

        MouseState mouse = Mouse.GetState();
        if (mouse.LeftButton != ButtonState.Pressed)
        {
            this.draggedActor = null;
            return;
        }

        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);
        Microsoft.Xna.Framework.Rectangle contentBounds = this.GetContentBounds();
        if (!contentBounds.Contains(mouseX, mouseY))
        {
            return;
        }

        Point tile = this.ScreenToTile(contentBounds, mouseX, mouseY);
        if (actor.TileX == tile.X && actor.TileY == tile.Y)
        {
            return;
        }

        actor.TileX = tile.X;
        actor.TileY = tile.Y;
        this.state.IsDirty = true;
    }

    private Microsoft.Xna.Framework.Rectangle GetActorBounds(Microsoft.Xna.Framework.Rectangle contentBounds, NpcPlacement actor, Point sourceSize)
    {
        Vector2 tileTopLeft = this.TileToScreen(contentBounds, actor.TileX, actor.TileY);
        int width = Math.Max(1, sourceSize.X) * Game1.pixelZoom;
        int height = Math.Max(1, sourceSize.Y) * Game1.pixelZoom;
        int x = (int)Math.Round(tileTopLeft.X + (Game1.tileSize - width) / 2f);
        int y = (int)Math.Round(tileTopLeft.Y + Game1.tileSize - height);
        return new Microsoft.Xna.Framework.Rectangle(x, y, width, height);
    }

    private Microsoft.Xna.Framework.Rectangle GetStandingSourceRect(Texture2D texture, Point sourceSize, int facing, out SpriteEffects effects)
    {
        effects = SpriteEffects.None;
        int columns = Math.Max(1, texture.Width / Math.Max(1, sourceSize.X));
        int rows = Math.Max(1, texture.Height / Math.Max(1, sourceSize.Y));
        int row = facing switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            3 => rows >= 4 ? 3 : 1,
            _ => 0
        };

        if (facing == 3 && rows < 4)
        {
            effects = SpriteEffects.FlipHorizontally;
        }

        return AnimatedSprite.GetSourceRect(texture.Width, sourceSize.X, sourceSize.Y, row * columns);
    }

    private static string GetNpcSpriteAsset(string actorName)
    {
        return ModEntry.KnownNpcSpriteAssets.TryGetValue(actorName, out string? assetName) && !string.IsNullOrWhiteSpace(assetName)
            ? assetName
            : $"Characters\\{actorName}";
    }

    private static Point GetNpcSpriteSize(string actorName)
    {
        return ModEntry.KnownNpcSpriteSizes.TryGetValue(actorName, out Point size) && size != Point.Zero
            ? size
            : DefaultSpriteSize;
    }

    private void DrawCursorTile(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds)
    {
        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);
        if (!contentBounds.Contains(mouseX, mouseY))
        {
            return;
        }

        Point tile = this.ScreenToTile(contentBounds, mouseX, mouseY);
        Utility.drawTextWithShadow(
            spriteBatch,
            $"Tile: {tile.X}, {tile.Y}",
            Game1.smallFont,
            new Vector2(contentBounds.X + 12, contentBounds.Bottom - 32),
            Color.White
        );
    }

    private Vector2 TileToScreen(Microsoft.Xna.Framework.Rectangle contentBounds, int tileX, int tileY)
    {
        return new Vector2(
            contentBounds.X + tileX * Game1.tileSize - this.panPixels.X,
            contentBounds.Y + tileY * Game1.tileSize - this.panPixels.Y
        );
    }

    private Point ScreenToTile(Microsoft.Xna.Framework.Rectangle contentBounds, int screenX, int screenY)
    {
        int tileX = (int)Math.Floor((screenX - contentBounds.X + this.panPixels.X) / Game1.tileSize);
        int tileY = (int)Math.Floor((screenY - contentBounds.Y + this.panPixels.Y) / Game1.tileSize);
        return new Point(tileX, tileY);
    }

    private void DrawWarning(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle contentBounds, string message)
    {
        IClickableMenu.drawTextureBox(
            spriteBatch,
            contentBounds.X,
            contentBounds.Y,
            contentBounds.Width,
            contentBounds.Height,
            Color.White
        );

        Utility.drawTextWithShadow(
            spriteBatch,
            "Could not preview this map:",
            Game1.smallFont,
            new Vector2(contentBounds.X + 24, contentBounds.Y + 24),
            Color.Red
        );

        int textX = contentBounds.X + 24;
        int textY = contentBounds.Y + 24 + Game1.smallFont.LineSpacing + 8;
        int maxWidth = Math.Max(120, contentBounds.Width - 48);
        int maxBottom = contentBounds.Bottom - 24;
        foreach (string line in WrapText(message, maxWidth))
        {
            if (textY + Game1.smallFont.LineSpacing > maxBottom)
            {
                Utility.drawTextWithShadow(spriteBatch, "...", Game1.smallFont, new Vector2(textX, textY), Color.Red);
                break;
            }

            Utility.drawTextWithShadow(spriteBatch, line, Game1.smallFont, new Vector2(textX, textY), Color.Red);
            textY += Game1.smallFont.LineSpacing + 2;
        }
    }

    private static string FormatException(Exception ex)
    {
        return string.IsNullOrWhiteSpace(ex.Message)
            ? ex.GetType().Name
            : $"{ex.GetType().Name}: {ex.Message}";
    }

    private static IEnumerable<string> WrapText(string text, int maxWidth)
    {
        foreach (string paragraph in text.Split('\n'))
        {
            string line = string.Empty;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = string.IsNullOrEmpty(line) ? word : line + " " + word;
                if (Game1.smallFont.MeasureString(candidate).X <= maxWidth)
                {
                    line = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(line))
                {
                    yield return line;
                }

                line = word;
            }

            if (!string.IsNullOrEmpty(line))
            {
                yield return line;
            }
        }
    }
}
