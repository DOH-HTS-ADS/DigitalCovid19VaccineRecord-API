using Application.Common.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    public class AzureSynapseSettings : ISettingsValidate
    {
        [Display(Name = "AzureSynapseSettings.ConnectionString")]
        [Required(AllowEmptyStrings = false)]
        public string ConnectionString { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string StatusQuery { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string IdQuery { get; set; }

        public string RelaxedQuery { get; set; }

        public string UseRelaxed { get; set; }

        #region IOptionsValidatable Implementation

        public void Validate()
        {
            Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
        }

        #endregion IOptionsValidatable Implementation
    }
}