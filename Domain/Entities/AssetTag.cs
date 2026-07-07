using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class AssetTag : BaseEntity
    {
        public Guid AssetId { get; set; }

        public Asset Asset { get; set; } = null!;

        public string TagNumber { get; set; } = null!;

        public TagType TagType { get; set; }

        public bool IsActive { get; set; }
    

        private AssetTag()
        {
        }

        public AssetTag(string tagNumber, Guid assetId)
        {
            TagNumber = tagNumber;
            AssetId = assetId;
        }
    }
}
