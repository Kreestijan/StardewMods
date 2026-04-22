namespace CustomChestSize;

internal readonly struct ChestGridLayout
{
    public ChestGridLayout(int columns, int rows)
    {
        this.Columns = columns;
        this.Rows = rows;
    }

    public int Columns { get; }

    public int Rows { get; }

    public int Capacity => this.Columns * this.Rows;
}
