using System;

namespace LotCatalogFunction.Models
{
    public class SkippedGroup
    {
        public Guid RunID { get; set; }

        public string Reason { get; set; }

        public string SalesType { get; set; }
        public string Group { get; set; }
        public string Gender { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public string Quality { get; set; }
        public string Clarity { get; set; }
        public string HairLength { get; set; }
        public string Damages { get; set; }

        public int BoxCount { get; set; }
        public int ShowlotCount { get; set; }
        public int TotalSkins { get; set; }

        public string BoxNumbers { get; set; }
    }
}