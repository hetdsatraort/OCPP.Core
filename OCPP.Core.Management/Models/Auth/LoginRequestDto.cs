using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Email or Phone is required")]
        public string EmailOrPhone { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }
}
