namespace HomeApp.Expenses.Data;

public sealed class ExpenseGroup
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563eb";
    public bool IsEnabled { get; set; } = true;
    public int DisplayOrder { get; set; }
    public List<ExpenseEntry> Expenses { get; set; } = [];
}
