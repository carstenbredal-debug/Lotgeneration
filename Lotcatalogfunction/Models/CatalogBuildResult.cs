using System.Collections.Generic;

namespace LotCatalogFunction.Models
{
    public class CatalogBuildResult
    {
        public List<CatalogLot> CatalogLots { get; set; } = new();
        public List<SkippedGroup> SkippedGroups { get; set; } = new();
    }
}
