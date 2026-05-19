using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotCatalogFunction.Models
{
    public class GeneratedLot
    {
        public Guid UniqueID { get; set; } = Guid.NewGuid();

        public string IsShow { get; set; }
        public int? ShowlotBoxNumber { get; set; }

        public string SalesType { get; set; }
        public string Group { get; set; }
        public string Gender { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public string Quality { get; set; }
        public string Clarity { get; set; }
        public string HairLength { get; set; }
        public string Damages { get; set; }

        public string IncludedBoxNumbers { get; set; }
        public int BoxCount { get; set; }
        public int TotalSkins { get; set; }
    }
}
