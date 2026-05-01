using System.ComponentModel.DataAnnotations;

namespace TextEnhancer.Api.Models;

public class EnhanceRequest
{
    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Note { get; set; } = string.Empty;
}
