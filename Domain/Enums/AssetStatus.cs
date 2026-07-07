using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Enums
{
    public enum AssetStatus
    {
        Available = 1,
        Assigned = 2,
        InTransit = 3,
        UnderMaintenance = 4,
        Retired = 5
    }
}
