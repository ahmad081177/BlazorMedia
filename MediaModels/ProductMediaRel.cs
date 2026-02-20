using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaModels
{
    public class ProductMediaRel
    {
        public int ID { get; set; }

        [Required]
        public int ProductID { get; set; }

        [Required]
        public int MediaID { get; set; }

        public bool IsPrimary { get; set; }

        public Products2? Product { get; set; }
        public MediaItem? Media { get; set; }
    }
}
