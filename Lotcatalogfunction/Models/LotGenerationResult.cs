using System;
using System.Collections.Generic;

namespace LotCatalogFunction.Models
{
    public class LotGenerationResult
    {
        public Guid RunId { get; set; }
        public List<GeneratedLot> Lots { get; set; } = new();
        public List<SkippedGroup> SkippedGroups { get; set; } = new();
    }
}