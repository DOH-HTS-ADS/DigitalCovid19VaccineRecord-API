﻿using Application.Common.Interfaces;
using Application.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Infrastructure.QrApi
{
    public class QrApiService : IQrApiService
    {
        private readonly AppSettings _appSettings;

        #region Constructor

        public QrApiService(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        #endregion Constructor

        #region IQrApiService Implementation

        public async Task<byte[]> GetQrCodeAsync(string shc)
        {
            var content = new StringContent(shc);

            HttpClient client = new();
            var response = await client.PostAsync(_appSettings.QrCodeApi, content);

            var responseString = await response.Content.ReadAsStringAsync();

            var base64Part = responseString.Replace("data:image/png;base64,", "");

            var pngContent = Convert.FromBase64String(base64Part);
            return pngContent;
        }

        #endregion IQrApiService Implementation
    }
}