﻿// Copyright 2012 Microsoft Corporation
// 
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

using System;

namespace Microsoft.WindowsAzure.MediaServices.Client.Rest
{
    /// <summary>
    /// Describes Live channel settings.
    /// </summary>
    internal class ChannelServiceSettings
    {
        /// <summary>
        /// Gets or sets preview settings.
        /// </summary>
        public PreviewEndpointSettings Preview { get; set; }

        /// <summary>
        /// Gets or sets ingest settings.
        /// </summary>
        public IngestEndpointSettings Ingest { get; set; }

        /// <summary>
        /// Gets or sets input settings
        /// </summary>
        public InputSettings Input { get; set; }

        /// <summary>
        /// Gets or sets output settings
        /// </summary>
        public OutputSettings Output { get; set; }

		/// <summary>
		/// Gets or sets client access policy.
		/// </summary>
		public CrossSiteAccessPolicy ClientAccessPolicy { get; set; }

		/// <summary>
		/// Gets or sets cross domain access policy.
		/// </summary>
		public CrossSiteAccessPolicy CrossDomainPolicy { get; set; }

		/// <summary>
		/// Gets or sets custom domain settings.
		/// </summary>
		public CustomDomainSettings CustomDomain { get; set; }

        /// <summary>
        /// Creates an instance of ChannelServiceSettings class.
        /// </summary>
        public ChannelServiceSettings() { }

        /// <summary>
        /// Creates an instance of ChannelServiceSettings class from an instance of ChannelSettings.
        /// </summary>
        /// <param name="settings">Settings to copy into newly created instance.</param>
        public ChannelServiceSettings(ChannelSettings settings) 
        {
            if (settings == null) return;

            Preview = settings.Preview;
            Ingest = settings.Ingest;
            Output = settings.Output;

            if (settings.Input != null && settings.Input.FMp4FragmentDuration.HasValue)
            {
                Input = new InputSettings
                {
                    FMp4FragmentDuration = settings.Input.FMp4FragmentDuration.Value.Ticks
                };
            }

			if (settings.ClientAccessPolicy != null)
			{
				ClientAccessPolicy = new CrossSiteAccessPolicy
				{
					Policy = settings.ClientAccessPolicy.Policy,
					Version = settings.ClientAccessPolicy.Version
				};
			}

			if (settings.CrossDomainPolicy != null)
			{
				CrossDomainPolicy = new CrossSiteAccessPolicy
				{
					Policy = settings.CrossDomainPolicy.Policy,
					Version = settings.CrossDomainPolicy.Version
				};
			}

			if (settings.CustomDomain != null)
			{
				CustomDomain = new CustomDomainSettings
				{
					CustomDomainNames = settings.CustomDomain.CustomDomainNames
				};
			}
		}

        /// <summary>
        /// Casts ChannelServiceSettings to ChannelSettings.
        /// </summary>
        /// <param name="settings">Object to cast.</param>
        /// <returns>Casted object.</returns>
        public static explicit operator ChannelSettings(ChannelServiceSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            var result = new ChannelSettings
            {
                Preview = settings.Preview, 
                Ingest = settings.Ingest,
                Output = settings.Output
            };

            if (settings.Input != null && settings.Input.FMp4FragmentDuration.HasValue)
            {
                result.Input = new Client.InputSettings
                {
                    FMp4FragmentDuration = TimeSpan.FromTicks(settings.Input.FMp4FragmentDuration.Value)
                };
            }

			var policy = settings.ClientAccessPolicy;

			if (policy != null)
			{
				result.ClientAccessPolicy = new Client.CrossSiteAccessPolicy
				{
					Policy = policy.Policy,
					Version = policy.Version
				};
			}

			policy = settings.CrossDomainPolicy;

			if (policy != null)
			{
				result.CrossDomainPolicy = new Client.CrossSiteAccessPolicy
				{
					Policy = policy.Policy,
					Version = policy.Version
				};
			}

			var customDomain = settings.CustomDomain;

			if (customDomain != null)
			{
				result.CustomDomain = new Client.CustomDomainSettings
				{
					CustomDomainNames = customDomain.CustomDomainNames
				};
			}

            return result;
        }
    }

    /// <summary>
    /// Describes Channel input settings.
    /// </summary>
    internal class InputSettings
    {
        /// <summary>
        /// Gets or sets FMp4 fragment duration.
        /// </summary>
        public long? FMp4FragmentDuration { get; set; }
    }
}
