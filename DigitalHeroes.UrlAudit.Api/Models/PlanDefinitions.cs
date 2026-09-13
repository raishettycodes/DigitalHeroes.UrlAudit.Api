namespace DigitalHeroes.UrlAudit.Api.Configuration;

public static class PlanDefinitions
{
    public const string Free = "Free";
    public const string Starter = "Starter";
    public const string Professional = "Professional";
    public const string Agency = "Agency";

    public static readonly Dictionary<string, PlanDefinition> Plans =
        new()
        {
            {
                Free,
                new PlanDefinition
                {
                    Name = Free,
                    MonthlyPrice = 0,
                    MonthlyAuditLimit = 100
                }
            },

            {
                Starter,
                new PlanDefinition
                {
                    Name = Starter,
                    MonthlyPrice = 199,
                    MonthlyAuditLimit = 500
                }
            },

            {
                Professional,
                new PlanDefinition
                {
                    Name = Professional,
                    MonthlyPrice = 499,
                    MonthlyAuditLimit = 2000
                }
            },

            {
                Agency,
                new PlanDefinition
                {
                    Name = Agency,
                    MonthlyPrice = 1499,
                    MonthlyAuditLimit = -1
                }
            }
        };
}

public class PlanDefinition
{
    public string Name { get; set; } = string.Empty;

    public decimal MonthlyPrice { get; set; }

    public int MonthlyAuditLimit { get; set; }
}