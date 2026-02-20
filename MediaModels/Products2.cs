using System.ComponentModel.DataAnnotations;

namespace MediaModels
{
    public class Products2
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(255, MinimumLength = 3, ErrorMessage = "Product name must be between 3 and 255 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(5000, ErrorMessage = "Product info cannot exceed 5000 characters")]
        public string Info { get; set; } = string.Empty;

        public List<ProductMediaRel> MediaRelations { get; set; } = new();

    }
}