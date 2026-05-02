using Microsoft.Xna.Framework;

namespace CutsceneMaker.Models;

public sealed class PreviewEmote
{
    private float intervalMs;
    private bool fading;

    public PreviewEmote(string actorName, int emoteId)
    {
        this.ActorName = actorName;
        this.EmoteId = emoteId;
    }

    public string ActorName { get; }

    public int EmoteId { get; }

    public int Frame { get; private set; }

    public bool IsFinished { get; private set; }

    public void Update(GameTime time)
    {
        if (this.IsFinished)
        {
            return;
        }

        this.intervalMs += (float)time.ElapsedGameTime.TotalMilliseconds;
        if (this.fading && this.intervalMs > 20f)
        {
            this.intervalMs = 0f;
            this.Frame--;
            if (this.Frame < 0)
            {
                this.IsFinished = true;
            }

            return;
        }

        if (!this.fading && this.intervalMs > 20f && this.Frame <= 3)
        {
            this.intervalMs = 0f;
            this.Frame++;
            if (this.Frame == 4)
            {
                this.Frame = this.EmoteId;
            }

            return;
        }

        if (!this.fading && this.intervalMs > 250f)
        {
            this.intervalMs = 0f;
            this.Frame++;
            if (this.Frame >= this.EmoteId + 4)
            {
                this.fading = true;
                this.Frame = 3;
            }
        }
    }
}
