﻿using Application.Common.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    public class MessageQueueSettings : ISettingsValidate
    {
        [Required(AllowEmptyStrings = false)]
        public string ConnectionString { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string QueueName { get; set; }

        [Required(AllowEmptyStrings = true)]
        public string AlternateQueueName { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string SleepSeconds { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string InvisibleSeconds { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string NumberThreads { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string MaxDequeuePerMinute { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string MessageExpireHours { get; set; }

        #region IOptionsValidatable Implementation

        public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

        #endregion IOptionsValidatable Implementation
    }
}