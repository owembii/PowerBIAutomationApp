using FigBytesUtility.Core;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerBIAutomationApp.Utilities
{
    public static class FBConfigManager
    {
        private static readonly string strVaultURL;
        static FBConfigurationManager _fBConfigurationManager;
        private static readonly string strVaultAppReaderName;

        static FBConfigManager()
        {
            strVaultURL = Environment.GetEnvironmentVariable("AzureKeyVaultURL");
            strVaultAppReaderName = Environment.GetEnvironmentVariable("AzureKeyVaultReaderAppName");
        }
        public static string GetConfigurationSetting(int iOrganizationID, string strSecretName)
        {
            string strSecretValue = string.Empty;

            try
            {
                _fBConfigurationManager = new FBConfigurationManager(iOrganizationID, strVaultURL, strVaultAppReaderName);
                strSecretValue = _fBConfigurationManager.GetConfigurationSetting(strSecretName);
            }
            catch (Exception Ex)
            {
                throw;
            }

            return strSecretValue;
        }
        public static string GetConfigurationSettingByEnvironmentID(int iEnvironmentID, string strSecretName)
        {
            string strSecretValue = string.Empty;

            try
            {
                _fBConfigurationManager = new FBConfigurationManager(iEnvironmentID, strVaultURL, strVaultAppReaderName);
                strSecretValue = _fBConfigurationManager.GetConfigurationSetting(iEnvironmentID, strSecretName);
            }
            catch (Exception Ex)
            {
                throw;
            }

            return strSecretValue;
        }
        public static async Task<string> GetAccessToken()
        {
            string clientId = Environment.GetEnvironmentVariable(strVaultAppReaderName + "_AzureClientID");
            string clientSecret = Environment.GetEnvironmentVariable(strVaultAppReaderName + "_AzureClientSecret"); ;
            string tenantId = Environment.GetEnvironmentVariable(strVaultAppReaderName + "_AzureTenantID");

            string authority = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
            string resource = "https://analysis.windows.net/powerbi/api/.default"; // Power BI API resource

            string strAcessToken = string.Empty;

            try
            {
                var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authority))
                .Build();

                var result = await app.AcquireTokenForClient(new[] { resource })
                                  .ExecuteAsync();

                strAcessToken = result.AccessToken;
            }
            catch (Exception ex)
            {
                throw;
            }

            return strAcessToken;
        }
        public static string Encryption(string strText)
        {
            string strDecryptText = string.Empty;
            Utility utility = new Utility(strVaultURL, strVaultAppReaderName);
            strDecryptText = utility.Encryption(strText);
            return strDecryptText;
        }
        public static string Decryption(string strText)
        {
            string strDecryptText = string.Empty;
            Utility utility = new Utility(strVaultURL, strVaultAppReaderName);
            strDecryptText = utility.Decryption(strText);
            return strDecryptText;
        }
    }
}
