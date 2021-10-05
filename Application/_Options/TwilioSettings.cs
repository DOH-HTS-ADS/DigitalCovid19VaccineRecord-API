using Application.Common.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    public class TwilioSettings : ISettingsValidate
    {
        [Display(Name = "TwilioSettings.AccountSID")]
        [Required(AllowEmptyStrings = false)]
        public string AccountSID { get; set; }

        [Display(Name = "TwilioSettings.AuthToken")]
        [Required(AllowEmptyStrings = false)]
        public string AuthToken { get; set; }

        [Display(Name = "TwilioSettings.FromPhone")]
        [Required(AllowEmptyStrings = false)]
        public string FromPhone { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string SandBox { get; set; }

        #region IOptionsValidatable Implementation

        public void Validate()
        {
            Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
        }

        #endregion IOptionsValidatable Implementation
    }
}