using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LotCatalogFunction.Models
{
    public class StringDefinition
    {
        public int StringDefinitionID { get; set; }
        public string ColumnName { get; set; } = "";
        public bool IsActive { get; set; }
    }
}