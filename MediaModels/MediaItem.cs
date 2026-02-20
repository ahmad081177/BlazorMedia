using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaModels
{
    public class MediaItem
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Media URL is required")]
        [StringLength(500, ErrorMessage = "Media URL cannot exceed 500 characters")]
        public string MediaURL { get; set; } = string.Empty;
    }
}
