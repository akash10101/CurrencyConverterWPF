namespace Challenge.Models
{
    public sealed class CurrencyInfo
    {
        public CurrencyInfo(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public string Code { get; }

        public string Name { get; }

        public string DisplayName => Code + " - " + Name;

        public override string ToString() => DisplayName;
    }
}
