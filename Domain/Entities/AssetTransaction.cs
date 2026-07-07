using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class AssetTransaction : BaseEntity
    {
        public Guid AssetId { get; set; }

        public Asset Asset { get; set; } = null!;

        public Guid? FromLocationId { get; set; }

        public Guid? ToLocationId { get; set; }

        public Guid? DriverId { get; set; }

        public Guid? DeviceId { get; set; }

        public TransactionType TransactionType { get; set; }

        public DateTime TransactionTime { get; set; }

        public string? Remarks { get; set; }
    }
}
