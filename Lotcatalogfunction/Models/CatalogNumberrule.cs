using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotCatalogFunction.Models
{
    public class CatalogNumberRule
    {
        public int CatalogNumberRuleID { get; set; }
        public string SalesType { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Group { get; set; } = "";
        public int StartNumber { get; set; }
        public bool IsActive { get; set; }
    }
}