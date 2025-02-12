﻿using Application.Common.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    public class AppSettings : ISettingsValidate
    {
        [Required(AllowEmptyStrings = false)]
        public string AppleWalletUrl { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string CDCUrl { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string CodeSecret { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string CommonHealthUrl { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string ContactUsUrl { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string CovidWebUrl { get; set; }

        [Required(AllowEmptyStrings = true)]
        public string DeveloperEmail { get; set; }

        [Required(AllowEmptyStrings = true)]
        public string DeveloperSms { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string EmailLogoUrl { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string GoogleWalletUrl { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string LinkExpireHours { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string MaxQrSeconds { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string MaxQrTries { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string MaxStatusSeconds { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string MaxStatusTries { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string NumberOfDoses { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string QrCodeApi { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string RedisConnectionString { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string SendNotFoundEmail { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string SendNotFoundSms { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string SupportPhoneNumber { get; set; }

        public string TryLegacyEncryption { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string UseMessageQueue { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string VaccineFAQUrl { get; set; }

        [Display(Name = "AppSettings.WebUrl")]
        [Required(AllowEmptyStrings = false)]
        public string WebUrl { get; set; }

        #region IOptionsValidatable Implementation

        public void Validate()
        {
            Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
        }

        #endregion IOptionsValidatable Implementation
    }
}