using Domain.Common;

namespace Domain.Entities
{
    public class Settings : BaseEntity
    {
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string? Description { get; set; }
        public string Group { get; set; } = "System";
    }
}
