﻿//-----------------------------------------------------------------------
// <copyright file="AadAuthenticationE2ETests.cs" company="Microsoft">Copyright 2012 Microsoft Corporation</copyright>
// <license>
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </license>

using System;
using System.Configuration;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MediaServices.Client.Tests.Common;

namespace Microsoft.WindowsAzure.MediaServices.Client.Tests.AadAuthentication
{
    [TestClass]
    public class AadAuthenticationE2ETests
    {
        private Uri _mediaServicesApiServerUri;

        [TestInitialize]
        public void SetupTest()
        {
            _mediaServicesApiServerUri = new Uri(WindowsAzureMediaServicesTestConfiguration.MediaServicesAccountCustomApiServerEndpoint);
        }

        /// <summary>
        /// This test case will prompt for user's credentials, so adding a Ignore tag here.
        /// </summary>
        [Ignore]
        [TestMethod]
        [TestCategory("ClientSDK")]
        [Owner("ClientSDK")]
        public void TestUserCredential()
        {
            var environment = WindowsAzureMediaServicesTestConfiguration.GetSelfDefinedEnvironment();
            var tokenCredentials = new AzureAdTokenCredentials(WindowsAzureMediaServicesTestConfiguration.UserTenant, environment);
            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);

            var mediaContext = new CloudMediaContext(_mediaServicesApiServerUri, tokenProvider);
            mediaContext.Assets.FirstOrDefault();
        }

        [TestMethod]
        [TestCategory("ClientSDK")]
        [Owner("ClientSDK")]
        public void TestServicePrincipalWithClientSymmetricKey()
        {
            var clientId = WindowsAzureMediaServicesTestConfiguration.ClientIdForAdAuth;
            var clientSecret = WindowsAzureMediaServicesTestConfiguration.ClientSecretForAdAuth;

            var environment = WindowsAzureMediaServicesTestConfiguration.GetSelfDefinedEnvironment();
            var tokenCredentials = new AzureAdTokenCredentials(WindowsAzureMediaServicesTestConfiguration.UserTenant, new AzureAdClientSymmetricKey(clientId, clientSecret), environment);
            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);

            var mediaContext = new CloudMediaContext(_mediaServicesApiServerUri, tokenProvider);
            mediaContext.Assets.FirstOrDefault();
        }

        [TestMethod]
        [TestCategory("ClientSDK")]
        [Owner("ClientSDK")]
        public void TestServicePrincipalWithClientCertificate()
        {
            var clientId = WindowsAzureMediaServicesTestConfiguration.ClientIdForAdAuth;
            var clientCertificateThumbprint = ConfigurationManager.AppSettings["ClientCertificateThumbprintForAdAuth"];

            var environment = WindowsAzureMediaServicesTestConfiguration.GetSelfDefinedEnvironment();
            var tokenCredentials = new AzureAdTokenCredentials(ConfigurationManager.AppSettings["UserTenant"], new AzureAdClientCertificate(clientId, clientCertificateThumbprint), environment);

            var tokenProvider = new AzureAdTokenProvider(tokenCredentials);
            var mediaContext = new CloudMediaContext(_mediaServicesApiServerUri, tokenProvider);
            mediaContext.Assets.FirstOrDefault();
        }
    }
}