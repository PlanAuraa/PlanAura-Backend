namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>One label/value fact rendered in the cover page's summary card.</summary>
    public class ContractSummaryItem
    {
        public string Label { get; set; } = null!;
        public string Value { get; set; } = null!;

        public ContractSummaryItem()
        {
        }

        public ContractSummaryItem(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }
}
