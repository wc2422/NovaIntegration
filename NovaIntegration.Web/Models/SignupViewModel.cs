using System.ComponentModel.DataAnnotations;
//asked gpt for some help getting the requirements for the passwords and they referenced this library

namespace NovaIntegration.Web.Models
{
    public class SignupViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords must match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}