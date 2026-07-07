using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Location : BaseEntity
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public Guid ZoneId { get; set; }
        public Zone Zone { get; set; } = null!;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
}
