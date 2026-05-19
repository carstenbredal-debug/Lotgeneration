using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotCatalogFunction.Models
{
    public class BoxRow
    {
        public int BoxNumber { get; set; }

        public string BoxType { get; set; }

        public string SalesType { get; set; }
        public string Gender { get; set; }
        public string Group { get; set; }
        public string Damages { get; set; }

        public string Size { get; set; }
        public string HairLength { get; set; }
        public string Color { get; set; }
        public string Quality { get; set; }
        public string Clarity { get; set; }

        public int Skins { get; set; }
    }
}