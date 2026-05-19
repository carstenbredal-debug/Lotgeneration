using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotCatalogFunction.Models
{
    public class LotSortOrder
    {
        public string ColumnName { get; set; } = "";
        public string Value { get; set; } = "";
        public int SortOrder { get; set; }
    }
}