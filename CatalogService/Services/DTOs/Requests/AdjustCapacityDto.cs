using System.ComponentModel.DataAnnotations;

namespace CatalogService.Services.DTOs.Requests
{
    public record AdjustCapacityDto
    (
        [Required(ErrorMessage = "The remaining seats value is required.")]
        [Range(0, 1000000, ErrorMessage = "Remaining seats cannot be a negative number.")]
        int? RemainingSeats,

        [Required(ErrorMessage = "You must provide a reason for the adjustment.")]
        [MinLength(5, ErrorMessage = "The reason must be at least 5 characters long.")]
        string Reason
    );
}
