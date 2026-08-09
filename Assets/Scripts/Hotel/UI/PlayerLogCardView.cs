using System.Collections.Generic;
using Hotel.Runtime;

public sealed class PlayerLogCardView
{
    public int Sequence;
    public int Day;
    public string PhaseText;
    public PlayerLogCategory Category;
    public string Title;
    public string Summary;
}

public sealed class PlayerLogDayGroup
{
    public int Day;
    public readonly List<PlayerLogCardView> Cards = new List<PlayerLogCardView>();
}
