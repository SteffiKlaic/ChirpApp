using System.ComponentModel.DataAnnotations;

namespace ChirpApp.Models
{
    public class LoginViewModel
    {

        [Required]
        public string Login { get; set; } // E-Mail ODER Benutzername

        [Required]
        [DataType(DataType.Password)]
        public string Passwort { get; set; }
    }
}
