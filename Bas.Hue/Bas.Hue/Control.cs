namespace Bas.Hue
{
    public sealed class Control
    {
        public int MinDimLevel { get; set; }
        public int MaxLumen { get; set; }
        public string ColorGamutType { get; set; }
        public int MinColorTemperature { get; set; }
        public int MaxColorTemperature { get; set; }
        public (float, float)[] ColorGamut { get; set; }
    }
}