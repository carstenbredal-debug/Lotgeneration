using System;

namespace LotCatalogFunction.Models
{
    public class LotCatalogRow
    {
        public int StringNumber { get; set; }
        public int SortNumber { get; set; }
        public string IsShow { get; set; }
        public string ShowlotBoxNumber { get; set; }
        public string SalesType { get; set; }
        public string Gender { get; set; }
        public string Group { get; set; }
        public string HairLength { get; set; }
        public string Size { get; set; }
        public string Quality { get; set; }
        public string Color { get; set; }
        public string Clarity { get; set; }
        public string Damages { get; set; }
        public string IncludedBoxNumbers { get; set; }
        public int? BoxCount { get; set; }
        public int? TotalSkins { get; set; }
        public Guid UniqueID { get; set; }
    }
}