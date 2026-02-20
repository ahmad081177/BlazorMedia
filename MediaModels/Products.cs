using System.ComponentModel.DataAnnotations;

namespace MediaModels
{
    public class Products
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(255, MinimumLength = 3, ErrorMessage = "Product name must be between 3 and 255 characters")]
        public string Name { get; set; }

        [StringLength(5000, ErrorMessage = "Product info cannot exceed 5000 characters")]
        public string Info { get; set; } = string.Empty;

        [Required(ErrorMessage = "Primary image is required")]
        [StringLength(500, ErrorMessage = "Image path cannot exceed 500 characters")]
        public string Image1 { get; set; }

        [StringLength(500, ErrorMessage = "Image path cannot exceed 500 characters")]
        public string Image2 { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Video path cannot exceed 500 characters")]
        public string Video { get; set; } = string.Empty;
    }
}