using Domain.Common;
using System;

namespace Domain.Entities
{
    public class Notification : BaseEntity
    {
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string Type { get; set; } = "Info";
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
