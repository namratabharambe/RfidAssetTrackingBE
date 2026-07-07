using Domain.Common;
using System;

namespace Domain.Entities
{
    public class Barcode : BaseEntity
    {
        public string BarcodeValue { get; set; } = null!;
        public string Format { get; set; } = "Code128";
        public bool IsActive { get; set; } = true;
        public Guid? AssetId { get; set; }
        public Asset? Asset { get; set; }
    }
}
