using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotCatalogFunction.Models
{
    public class LotSizeRule
    {
        public int RuleID { get; set; }

        public string Gender { get; set; }
        public string Size { get; set; }

        public int MaxBoxes { get; set; }
        public int? MaxSkinsPerBox { get; set; }
        public int? ShowlotSkins { get; set; }

        public int MaxLotSizeExclShowlot { get; set; }
        public int MaxLotSizeInclShowlot { get; set; }

        public int Priority { get; set; }
        public bool IsActive { get; set; }
    }
}