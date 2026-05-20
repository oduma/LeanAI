using LeanAI.Domain.Common;

namespace LeanAI.Domain.WeightManagement.Entities;

public class AppSettings : BaseEntity
{
    public const string DefaultModelName = "gemini-2.5-flash";

    public string    GeminiModelName  { get; set; } = DefaultModelName;

    /// <summary>First calendar column. Monday or Sunday. Default Monday.</summary>
    public DayOfWeek CalendarFirstDay { get; set; } = DayOfWeek.Monday;

    public bool UseBmr { get; set; }
}
