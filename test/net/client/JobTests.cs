﻿//-----------------------------------------------------------------------
// <copyright file="JobTests.cs" company="Microsoft">Copyright 2012 Microsoft Corporation</copyright>
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
using System.Collections.Generic;
using System.Data.Services.Client;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MediaServices.Client.Tests.Helpers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.WindowsAzure.MediaServices.Client.Tests
{
    [TestClass]
    public partial class JobTests
    {

        public const string ExpressionEncoder = "Windows Azure Media Encoder";
        public const string WameV1Preset = "H.264 256k DSL CBR";
        public const string WameV2Preset = "H264 Broadband SD 4x3";
        public const string Mp4ToSmoothStreamsTask = "MP4 to Smooth Streams Task";
        public const string PlayReadyProtectionTask = "PlayReady Protection Task";
        public const string SmoothToHlsTask = "Smooth Streams to HLS Task";
        public const string StrorageDecryptionProcessor = "Storage Decryption";
        public const string MediaEncryptor = "Windows Azure Media Encryptor";
        public const string MediaPackager = "Windows Azure Media Packager";

        private CloudMediaContext _dataContext;
        private string _smallWmv;
        private const string NamePrefix = "JobTests_";
        private const int InitialJobPriority = 1;


        /// <summary>
        ///     Gets or sets the test context which provides information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void SetupTest()
        {
            _dataContext = WindowsAzureMediaServicesTestConfiguration.CreateCloudMediaContext();
            _smallWmv = WindowsAzureMediaServicesTestConfiguration.GetVideoSampleFilePath(TestContext, WindowsAzureMediaServicesTestConfiguration.SmallWmv);
        }

        // media processor versions
        public static string GetWamePreset(IMediaProcessor mediaProcessor)
        {
            var mpVersion = new Version(mediaProcessor.Version);
            if (mpVersion.Major == 1)
            {
                return WameV1Preset;
            }
            else
            {
                return WameV2Preset;
            }
        }


        public static void VerifyAllTasksFinished(string jobId)
        {
            CloudMediaContext context2 = WindowsAzureMediaServicesTestConfiguration.CreateCloudMediaContext();
            IJob job2 = context2.Jobs.Where(c => c.Id == jobId).Single();
            Assert.AreEqual(JobState.Finished, job2.State);

            foreach (ITask task in job2.Tasks)
            {
                Assert.AreEqual(JobState.Finished, task.State);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldCreateJobPreset()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            string name = GenerateName("Job 1");
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, name, mediaProcessor, GetWamePreset(mediaProcessor), asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldSplitMetadataLost()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.None);
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            string name = GenerateName("ShouldSplitMetadataLost");

            IJob job = CreateAndSubmitOneTaskJob(_dataContext, name, mediaProcessor, "H264 Smooth Streaming 720p", asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);

            IJob refreshedJob = _dataContext.Jobs.Where(c => c.Id == job.Id).Single();
            bool ok = refreshedJob.Tasks.Single().OutputAssets.Single().AssetFiles.AsEnumerable().Select(f => f.Name).Contains("SmallWmv_metadata.xml");

            Assert.IsTrue(ok);
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldFinishJobWithSuccessWhenPresetISUTF8()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            string presetXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <Thumbnail Size=""80,60"" Type=""Jpeg"" Filename=""{OriginalFilename}_{ThumbnailTime}.{DefaultExtension}"">
                  <Time Value=""0:0:0""/>
                  <Time Value=""0:0:3"" Step=""0:0:0.25"" Stop=""0:0:10""/>
                </Thumbnail>";
            string name = GenerateName("ShouldFinishJobWithSuccessWhenPresetISUTF8");
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, name, mediaProcessor, presetXml, asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldContainTaskHistoryEventsOnceJobFinished()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            string name = GenerateName("ShouldContainTaskHistoryEventsOnceJobFinished");
            string preset = GetWamePreset(mediaProcessor);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, name, mediaProcessor, preset, asset, TaskOptions.None);
            ITask task = job.Tasks.FirstOrDefault();
            Assert.IsNotNull(task);
            Assert.IsNotNull(task.HistoricalEvents, "HistoricalEvents should not be null for submitted job");
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
            Assert.IsTrue(task.HistoricalEvents.Count > 0, "HistoricalEvents should not be empty after job has been finished");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldThrowTryingToCreateJobWithOneTaskAndNoOutput()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IJob job = _dataContext.Jobs.Create("CreateJobWithOneTaskAndNoOutput");
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            ITask task = job.Tasks.AddNew("Task1", processor, GetWamePreset(processor), TaskOptions.None);
            task.InputAssets.Add(asset);
            job.Submit();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldThrowTryingTocreateCreateJobWithNoTasks()
        {
            try
            {
                IJob job = _dataContext.Jobs.Create("CreateJobWithNoTasks");
                job.Submit();
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("There must be at least one task.", ex.Message, "Wrong exception message");
                throw ex;
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldSubmitAndFinishJobWithOneTaskEmptyConfiguration()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IJob job = _dataContext.Jobs.Create("CreateJobWithOneTaskEmptyConfiguration");
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpStorageDecryptorName, WindowsAzureMediaServicesTestConfiguration.MpStorageDecryptorVersion);
            ITask task = job.Tasks.AddNew("Task1", processor, String.Empty, TaskOptions.None);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew("Output", AssetCreationOptions.None);
            job.Submit();
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }


        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldFinishJobWithErrorWithInvalidPreset()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldFinishJobWithErrorWithInvalidPreset"), processor, "Some wrong Preset", asset, TaskOptions.None);
            Action<string> verify = id =>
            {
                IJob job2 = _dataContext.Jobs.Where(c => c.Id == id).FirstOrDefault();
                Assert.IsNotNull(job2);
                Assert.IsNotNull(job2.Tasks);
                Assert.AreEqual(1, job2.Tasks.Count);
                Assert.IsNotNull(job2.Tasks[0].ErrorDetails);
                Assert.AreEqual(1, job2.Tasks[0].ErrorDetails.Count);
                Assert.IsNotNull(job2.Tasks[0].ErrorDetails[0]);
                Assert.AreEqual("UserInput", job2.Tasks[0].ErrorDetails[0].Code);
            };
            WaitForJob(job.Id, JobState.Error, verify);
        }

        [TestMethod]
        [Priority(0)]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldCancelJobAfterSubmission()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldCancelJobAfterSubmission"), processor, GetWamePreset(processor), asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Processing, (string id) => { });
            job.Cancel();
            WaitForJob(job.Id, JobState.Canceling, (string id) => { });
        }

        [TestMethod]
        [DeploymentItem(@"Media\Thumbnail.xml", "Media")]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldFinishJobCreatedFromThumbnailXml()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            string xmlPreset = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.ThumbnailXml);
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldFinishJobCreatedFromThumbnailXml"), processor, xmlPreset, asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Media\ThumbnailWithZeroStep.xml", "Media")]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldFinishJobWithZeroStepThumbnail()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            string xmlPreset = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.ThumbnailWithZeroStepXml);
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldFinishJobWithZeroStepThumbnail"), processor, xmlPreset, asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv2.wmv", "Media")]
        [DeploymentItem(@"Media\SmallMp41.mp4", "Media")]
        [DeploymentItem(@"Configuration\multi.xml", "Configuration")]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldCreateJobWithMultipleAssetsAndValidateParentLinks()
        {
            // Create multiple assets, set them as parents for a job, and validate that the parent links are set.
            IAsset asset1 = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IAsset asset2 = AssetTests.CreateAsset(_dataContext, WindowsAzureMediaServicesTestConfiguration.SmallWmv2, AssetCreationOptions.StorageEncrypted);
            IAsset asset3 = AssetTests.CreateAsset(_dataContext, WindowsAzureMediaServicesTestConfiguration.SmallMp41, AssetCreationOptions.StorageEncrypted);

            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.MultiConfig);

            IJob job = _dataContext.Jobs.Create("Test");
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            ITask task = job.Tasks.AddNew("Task1", mediaProcessor, configuration, TaskOptions.None);

            task.InputAssets.Add(asset1);
            task.InputAssets.Add(asset2);
            task.InputAssets.Add(asset3);
            task.OutputAssets.AddNew("JobOutput", options: AssetCreationOptions.None);
            job.Submit();

            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);

            Assert.IsTrue(job.OutputMediaAssets[0].ParentAssets.Count == 3);
            IEnumerable<string> parentIds = job.OutputMediaAssets[0].ParentAssets.Select(a => a.Id);
            Assert.IsTrue(parentIds.Contains(asset1.Id));
            Assert.IsTrue(parentIds.Contains(asset2.Id));
            Assert.IsTrue(parentIds.Contains(asset3.Id));
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv2.wmv", "Media")]
        [DeploymentItem(@"Media\SmallMp41.mp4", "Media")]
        [DeploymentItem(@"Configuration\multi.xml", "Configuration")]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldSubmitAndFinishJobWithMultipleAssetAndVerifyOrderOfInputAssets()
        {
            // Create multiple assets, set them as parents for a job, and validate that the parent links are set.
            IAsset asset1 = AssetTests.CreateAsset(_dataContext, WindowsAzureMediaServicesTestConfiguration.SmallWmv2, AssetCreationOptions.StorageEncrypted);
            IAsset asset2 = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IAsset asset3 = AssetTests.CreateAsset(_dataContext, WindowsAzureMediaServicesTestConfiguration.SmallMp41, AssetCreationOptions.StorageEncrypted);
            asset1.Name = "SmallWmv2";
            asset2.Name = "SmallWmv";
            asset3.Name = "SmallMP41";
            asset1.Update();
            asset2.Update();
            asset3.Update();

            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.MultiConfig);

            IJob job = _dataContext.Jobs.Create("Test");
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            ITask task = job.Tasks.AddNew("Task1", mediaProcessor, configuration, TaskOptions.None);

            task.InputAssets.Add(asset1);
            task.InputAssets.Add(asset2);
            task.InputAssets.Add(asset3);
            task.OutputAssets.AddNew("JobOutput", options: AssetCreationOptions.None);
            job.Submit();

            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);

            Assert.IsTrue(job.InputMediaAssets.Count == 3);
            Assert.IsTrue(job.InputMediaAssets[0].Name == "SmallWmv2");
            Assert.IsTrue(job.InputMediaAssets[1].Name == "SmallWmv");
            Assert.IsTrue(job.InputMediaAssets[2].Name == "SmallMP41");
        }

        [TestMethod]
        [DeploymentItem(@"Media\Thumbnail.xml", "Media")]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        [Priority(0)]
        public void ShouldSubmitAndFinishChainedTasks()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);

            IJob job = _dataContext.Jobs.Create("Test");
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            ITask task = job.Tasks.AddNew("Task1", mediaProcessor, GetWamePreset(mediaProcessor), TaskOptions.None);
            task.InputAssets.Add(asset);
            IAsset asset2 = task.OutputAssets.AddNew("Another asset");

            string xmlPreset = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.ThumbnailXml);
            ITask task2 = job.Tasks.AddNew("Task2", mediaProcessor, xmlPreset, TaskOptions.None);
            task2.InputAssets.Add(asset2);
            task2.OutputAssets.AddNew("JobOutput", options: AssetCreationOptions.None);
            job.Submit();


            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Media\Thumbnail.xml", "Media")]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        [Priority(1)]
        public void ShouldSubmitAndFinishChainedTasksUsingParentOverload()
        {
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);

            IJob job = _dataContext.Jobs.Create("Test");
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            ITask task = job.Tasks.AddNew("Task1", mediaProcessor, GetWamePreset(mediaProcessor), TaskOptions.None);
            task.InputAssets.Add(asset);
            IAsset asset1 = task.OutputAssets.AddNew("output asset");

            string xmlPreset = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.ThumbnailXml);
            ITask task2 = job.Tasks.AddNew("Task2", mediaProcessor, xmlPreset, TaskOptions.None, task);
            task2.OutputAssets.AddNew("JobOutput", options: AssetCreationOptions.None);
            job.Submit();
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }



        [TestMethod]
        [DeploymentItem(@"Configuration\MP4 to Smooth Streams.xml", "Configuration")]
        [DeploymentItem(@"Media\SmallMP41.mp4", "Media")]
        public void ShouldSubmitAndFihishMp4ToSmoothJob()
        {
            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.DefaultMp4ToSmoothConfig);
            IAsset asset = AssetTests.CreateAsset(_dataContext, WindowsAzureMediaServicesTestConfiguration.SmallMp41, AssetCreationOptions.StorageEncrypted);
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpPackagerName, WindowsAzureMediaServicesTestConfiguration.MpPackagerVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitAndFihishMp4ToSmoothJob"), mediaProcessor, configuration, asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Configuration\PlayReady Protection.xml", "Configuration")]
        [DeploymentItem(@"Media\Small.ism", "Media")]
        [DeploymentItem(@"Media\Small.ismc", "Media")]
        [DeploymentItem(@"Media\Small.ismv", "Media")]
        [Priority(0)]
        public void ShouldSubmitAndFinishPlayReadyProtectionJob()
        {
            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.PlayReadyConfig);

            IAsset asset = CreateSmoothAsset();
            IMediaProcessor mediaEncryptor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncryptorName, WindowsAzureMediaServicesTestConfiguration.MpEncryptorVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitAndFinishPlayReadyProtectionJob"), mediaEncryptor, configuration, asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [Priority(0)]
        [TestMethod]
        [DeploymentItem(@"Configuration\Smooth Streams to Apple HTTP Live Streams.xml", "Configuration")]
        [DeploymentItem(@"Media\Small.ism", "Media")]
        [DeploymentItem(@"Media\Small.ismc", "Media")]
        [DeploymentItem(@"Media\Small.ismv", "Media")]
        public void ShouldSubmitAndFinishSmoothToHlsJob()
        {
            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.SmoothToHlsConfig);

            IAsset asset = CreateSmoothAsset();
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpPackagerName, WindowsAzureMediaServicesTestConfiguration.MpPackagerVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitAndFinishSmoothToHlsJob"), mediaProcessor, configuration, asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }


        [Priority(1)]
        [TestMethod]
        [DeploymentItem(@"Configuration\Smooth Streams to Encrypted Apple HTTP Live Streams.xml", "Configuration")]
        [DeploymentItem(@"Media\Small.ism", "Media")]
        [DeploymentItem(@"Media\Small.ismc", "Media")]
        [DeploymentItem(@"Media\Small.ismv", "Media")]
        public void ShouldSubmitAndFinishSmoothToHlsEncryptedJob()
        {
            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.SmoothToEncryptHlsConfig);

            IAsset asset = CreateSmoothAsset();

            IMediaProcessor mediaPackager = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpPackagerName, WindowsAzureMediaServicesTestConfiguration.MpPackagerVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitAndFinishSmoothToHlsEncryptedJob"), mediaPackager, configuration, asset, TaskOptions.ProtectedConfiguration);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Configuration\MP4 to Smooth Streams.xml", "Configuration")]
        [DeploymentItem(@"Media\SmallMP41.mp4", "Media")]
        public void ShouldSubmitAndFinishMp4ToSmoothJobWithStorageProtectedInputsAndOutputs()
        {
            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.DefaultMp4ToSmoothConfig);
            IAsset asset = AssetTests.CreateAsset(_dataContext, WindowsAzureMediaServicesTestConfiguration.SmallMp41, AssetCreationOptions.StorageEncrypted);
            IJob job = _dataContext.Jobs.Create("MP4 to Smooth with protected input and output assets");
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpPackagerName, WindowsAzureMediaServicesTestConfiguration.MpPackagerVersion);
            ITask task = job.Tasks.AddNew(MediaPackager, mediaProcessor, configuration, TaskOptions.None);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew("Output encrypted", AssetCreationOptions.StorageEncrypted);
            job.Submit();
            Assert.IsNotNull(task.InputAssets);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Configuration\PlayReady Protection.xml", "Configuration")]
        [DeploymentItem(@"Media\Small.ism", "Media")]
        [DeploymentItem(@"Media\Small.ismc", "Media")]
        [DeploymentItem(@"Media\Small.ismv", "Media")]
        public void ShouldSubmitAndFinishPlayReadyProtectionJobWithStorageAndConfigurationEncryption()
        {
            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.PlayReadyConfig);

            IAsset asset = CreateSmoothAsset();
            IMediaProcessor mediaEncryptor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncryptorName, WindowsAzureMediaServicesTestConfiguration.MpEncryptorVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitAndFinishPlayReadyProtectionJobWithStorageAndConfigurationEncryption"), mediaEncryptor, configuration, asset, TaskOptions.ProtectedConfiguration);
            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
        }

        [TestMethod]
        [DeploymentItem(@"Media\EncodePlusEncryptWithEE.xml", "Media")]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldSubmitAndFinishEETaskWithStorageProtectedInputAndClearOutput()
        {
            //
            //  This test uses the same preset as the EE DRM tests but does not apply
            //  common encryption.  This preset gets split into multiple subtasks by EE.
            //

            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);

            // Load the EE preset to create a smooth streaming presentation with PlayReady protection
            string xmlPreset = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.EncodePlusEncryptWithEeXml);

            // Remove the DRM Section to produce clear content
            var doc = new XmlDocument();
            doc.LoadXml(xmlPreset);

            XmlNodeList drmNodes = doc.GetElementsByTagName("Drm");
            Assert.AreEqual(1, drmNodes.Count);

            XmlNode drmNode = drmNodes[0];
            drmNode.ParentNode.RemoveChild(drmNode);

            xmlPreset = doc.OuterXml;
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitAndFinishEETaskWithStorageProtectedInputAndClearOutput"), processor, xmlPreset, asset, TaskOptions.None);

            Assert.AreEqual(1, job.Tasks.Count);
            Assert.AreEqual(TaskOptions.None, job.Tasks[0].Options);
            Assert.IsNull(job.Tasks[0].InitializationVector);
            Assert.IsTrue(String.IsNullOrEmpty(job.Tasks[0].EncryptionKeyId));
            Assert.IsNull(job.Tasks[0].EncryptionScheme);
            Assert.IsNull(job.Tasks[0].EncryptionVersion);

            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);

            CloudMediaContext context2 = WindowsAzureMediaServicesTestConfiguration.CreateCloudMediaContext();
            IJob job2 = context2.Jobs.Where(c => c.Id == job.Id).Single();

            Assert.AreEqual(1, job2.Tasks.Count);
            Assert.AreEqual(1, job2.Tasks[0].OutputAssets.Count);
        }

        [TestMethod]
        [Priority(1)]
        [ExpectedException(typeof(DataServiceRequestException))]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldThrowTryingToDeleteJobInProcessingState()
        {
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldThrowTryingToDeleteJobInProcessingState"), processor, GetWamePreset(processor), asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Processing, (string id) => { });

            job.Delete();
        }

        [TestMethod]
        [Priority(0)]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldDeleteJobInFinishedState()
        {
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldDeleteJobInFinishedState"), processor, GetWamePreset(processor), asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Finished, (string id) => { });
            job.Delete();
        }

        [TestMethod]
        [Priority(1)]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldDeleteJobInCancelledState()
        {
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldDeleteJobInCancelledState"), processor, GetWamePreset(processor), asset, TaskOptions.None);
            WaitForJob(job.Id, JobState.Processing, (string id) => { });
            job.Cancel();
            WaitForJob(job.Id, JobState.Canceled, (string id) => { });
            job.Delete();
        }

        [TestMethod]
        [DeploymentItem(@"Configuration\MP4 to Smooth Streams.xml", "Configuration")]
        [DeploymentItem(@"Media\SmallMP41.mp4", "Media")]
        [Priority(0)]
        public void ShouldReceiveNotificationsForCompeletedJob()
        {
            string endPointAddress = Guid.NewGuid().ToString();
            CloudQueueClient client = CloudStorageAccount.Parse(WindowsAzureMediaServicesTestConfiguration.ClientStorageConnectionString).CreateCloudQueueClient();
            CloudQueue queue = client.GetQueueReference(endPointAddress);
            queue.CreateIfNotExists();
            string endPointName = Guid.NewGuid().ToString();
            INotificationEndPoint notificationEndPoint = _dataContext.NotificationEndPoints.Create(endPointName, NotificationEndPointType.AzureQueue, endPointAddress);
            Assert.IsNotNull(notificationEndPoint);

            string configuration = File.ReadAllText(WindowsAzureMediaServicesTestConfiguration.DefaultMp4ToSmoothConfig);
            IAsset asset = AssetTests.CreateAsset(_dataContext, WindowsAzureMediaServicesTestConfiguration.SmallMp41, AssetCreationOptions.StorageEncrypted);
            IMediaProcessor mediaProcessor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpPackagerName, WindowsAzureMediaServicesTestConfiguration.MpPackagerVersion);

            IJob job = _dataContext.Jobs.Create("CreateJobWithNotificationSubscription");
            ITask task = job.Tasks.AddNew("Task1", mediaProcessor, configuration, TaskOptions.None);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew("Output", AssetCreationOptions.None);

            job.JobNotificationSubscriptions.AddNew(NotificationJobState.All, notificationEndPoint);

            job.Submit();

            Assert.IsTrue(job.JobNotificationSubscriptions.Count > 0);

            WaitForJob(job.Id, JobState.Finished, VerifyAllTasksFinished);
            Thread.Sleep((int)TimeSpan.FromMinutes(5).TotalMilliseconds);

            Assert.IsNotNull(queue);
            Assert.IsTrue(queue.Exists());
            IEnumerable<CloudQueueMessage> messages = queue.GetMessages(10);
            Assert.IsTrue(messages.Any());
            Assert.AreEqual(4, messages.Count(), "Expecting to have 4 notifications messages");

            IJob lastJob = _dataContext.Jobs.Where(j => j.Id == job.Id).FirstOrDefault();
            Assert.IsNotNull(lastJob);
            Assert.IsTrue(lastJob.JobNotificationSubscriptions.Count > 0);
            IJobNotificationSubscription lastJobNotificationSubscription = lastJob.JobNotificationSubscriptions.Where(n => n.NotificationEndPoint.Id == notificationEndPoint.Id).FirstOrDefault();
            Assert.IsNotNull(lastJobNotificationSubscription);
            INotificationEndPoint lastNotificationEndPoint = lastJobNotificationSubscription.NotificationEndPoint;
            Assert.IsNotNull(lastNotificationEndPoint);
            Assert.AreEqual(endPointName, lastNotificationEndPoint.Name);
            Assert.AreEqual(endPointAddress, lastNotificationEndPoint.EndPointAddress);
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        public void ShouldUpdateJobPriorityWhenJobIsQueued()
        {
            const int newPriority = 3;

            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            //Create temp job to simuate queue when no reserved unit are allocated
            IJob tempJob = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("SubmitJobToCreateQueue"), processor, GetWamePreset(processor), asset, TaskOptions.None);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitJobAndUpdatePriorityWhenJobIsQueued"), processor, GetWamePreset(processor), asset, TaskOptions.None);

            WaitForJobStateAndUpdatePriority(job, JobState.Queued, newPriority);
            WaitForJob(job.Id, JobState.Finished, (string id) =>
                { 
                    var finished = _dataContext.Jobs.Where(c => c.Id == job.Id && c.Priority == newPriority).FirstOrDefault();
                    Assert.IsNotNull(finished);
                });
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        [ExpectedException(typeof(DataServiceRequestException))]
        public void ShouldThrowTryingUpdateJobPriorityWhenJobIsProcessing()
        {
            const int newPriority = 3;

            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitJobAndUpdatePriorityWhenJobIsQueued"), processor, GetWamePreset(processor), asset, TaskOptions.None);
            try
            {
                WaitForJobStateAndUpdatePriority(job, JobState.Processing, newPriority);
            }
            catch (DataServiceRequestException ex)
            {
                Assert.IsTrue(ex.InnerException.Message.Contains("Job's priority can only be changed if the job is in Queued state"));
                throw ex;
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\SmallWmv.wmv", "Media")]
        [ExpectedException(typeof(DataServiceRequestException))]
        public void ShouldThrowTryingUpdateJobPriorityWhenJobIsFinished()
        {
            const int newPriority = 3;
            
            IMediaProcessor processor = GetMediaProcessor(_dataContext, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
            IAsset asset = AssetTests.CreateAsset(_dataContext, _smallWmv, AssetCreationOptions.StorageEncrypted);
            IJob job = CreateAndSubmitOneTaskJob(_dataContext, GenerateName("ShouldSubmitJobAndUpdatePriorityWhenJobIsQueued"), processor, GetWamePreset(processor), asset, TaskOptions.None);
            try
            {
                WaitForJobStateAndUpdatePriority(job, JobState.Finished, newPriority);
            }
            catch (DataServiceRequestException ex)
            {
                Assert.IsTrue(ex.InnerException.Message.Contains("Job's priority can only be changed if the job is in Queued state"));
                throw ex;
            }
        }



        #region Helper Methods

        private IAsset CreateSmoothAsset()
        {
            var filePaths = new[] { WindowsAzureMediaServicesTestConfiguration.SmallIsm, WindowsAzureMediaServicesTestConfiguration.SmallIsmc, WindowsAzureMediaServicesTestConfiguration.SmallIsmv };
            return CreateSmoothAsset(filePaths);
        }

        private IAsset CreateSmoothAsset(string[] filePaths)
        {
            IAsset asset = _dataContext.Assets.Create(Guid.NewGuid().ToString(), AssetCreationOptions.StorageEncrypted);
            IAccessPolicy policy = _dataContext.AccessPolicies.Create("Write", TimeSpan.FromMinutes(5), AccessPermissions.Write);
            ILocator locator = _dataContext.Locators.CreateSasLocator(asset, policy);
            var blobclient = new BlobTransferClient
            {
                NumberOfConcurrentTransfers = 5,
                ParallelTransferThreadCount = 5
            };


            foreach (string filePath in filePaths)
            {
                var info = new FileInfo(filePath);
                IAssetFile file = asset.AssetFiles.Create(info.Name);
                file.UploadAsync(filePath, blobclient, locator, CancellationToken.None).Wait();
                if (WindowsAzureMediaServicesTestConfiguration.SmallIsm == filePath)
                {
                    file.IsPrimary = true;
                    file.Update();
                }
            }
            return asset;
        }

        public static IJob CreateAndSubmitOneTaskJob(CloudMediaContext context, string name, IMediaProcessor mediaProcessor, string preset, IAsset asset, TaskOptions options)
        {
            IJob job = context.Jobs.Create(name);
            job.Priority = InitialJobPriority;
            ITask task = job.Tasks.AddNew("Task1", mediaProcessor, preset, options);
            task.InputAssets.Add(asset);
            task.OutputAssets.AddNew("Output asset", AssetCreationOptions.None);
            DateTime timebeforeSubmit = DateTime.UtcNow;
            job.Submit();
            Assert.AreEqual(1, job.Tasks.Count, "Job contains unexpected amount of tasks");
            Assert.AreEqual(1, job.InputMediaAssets.Count, "Job contains unexpected total amount of input assets");
            Assert.AreEqual(1, job.OutputMediaAssets.Count, "Job contains unexpected total amount of output assets");
            Assert.AreEqual(1, job.Tasks[0].InputAssets.Count, "job.Task[0] contains unexpected amount of input assets");
            Assert.AreEqual(1, job.Tasks[0].OutputAssets.Count, "job.Task[0] contains unexpected amount of output assets");
            Assert.IsFalse(String.IsNullOrEmpty(job.Tasks[0].InputAssets[0].Id), "Asset Id is Null or empty");
            Assert.IsFalse(String.IsNullOrEmpty(job.Tasks[0].OutputAssets[0].Id), "Asset Id is Null or empty");
            return job;
        }

        public static void WaitForJob(string jobId, JobState jobState, Action<string> verifyAction)
        {
            DateTime start = DateTime.Now;
            while (true)
            {
                CloudMediaContext context2 = WindowsAzureMediaServicesTestConfiguration.CreateCloudMediaContext();
                IJob job2 = context2.Jobs.Where(c => c.Id == jobId).Single();
                if (job2.State == jobState)
                {
                    verifyAction(jobId);
                    return;
                }
                if (job2.State == JobState.Error)
                {
                    StringBuilder str = new StringBuilder();
                    str.AppendFormat("Job should not fail - Current State = {0} Expected State = {1} jobId = {2}", job2.State, jobState, jobId);
                    str.AppendLine();
                    foreach (var task in job2.Tasks)
                    {
                        foreach (var error in task.ErrorDetails)
                        {
                            str.AppendFormat("Error Code: {0} ErrorMessage: {1}", error.Code, error.Message);
                            str.AppendLine();
                        }
                    }

                    throw new Exception(str.ToString());
                }
                if (DateTime.Now - start > TimeSpan.FromMinutes(5))
                {
                    throw new Exception("Job Timed out - Current State " + job2.State.ToString() + " Expected State " + jobState + " jobId = " + jobId);
                }
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }

        public static IMediaProcessor GetEncoderMediaProcessor(CloudMediaContext context)
        {
            return GetMediaProcessor(context, WindowsAzureMediaServicesTestConfiguration.MpEncoderName, WindowsAzureMediaServicesTestConfiguration.MpEncoderVersion);
        }

        public static IMediaProcessor GetMediaProcessor(CloudMediaContext context, string mpName, string mpVersion)
        {
            IMediaProcessor mp = context.MediaProcessors.Where(c => c.Name == mpName && c.Version.StartsWith(mpVersion)).ToList().OrderByDescending(c => new Version(c.Version)).FirstOrDefault();

            if (mp == null)
            {
                throw new ArgumentException(string.Format("Media Processor {0}, version {1} is not found", mpName, mpVersion), "mpName");
            }

            Trace.WriteLine(string.Format("Using media processor {0} Version {1}, ID {2}", mp.Name, mp.Version, mp.Id));
            return mp;
        }

        private string GenerateName(string name)
        {
            return NamePrefix + name;
        }

        /// <summary>
        /// Waits for expected job state and updates job priority.
        /// </summary>
        /// <param name="job">The job.</param>
        /// <param name="expectedJobState">Expected state of the job.</param>
        /// <param name="newPriority">The new priority.</param>
        private void WaitForJobStateAndUpdatePriority(IJob job, JobState expectedJobState, int newPriority)
        {
            WaitForJob(job.Id, expectedJobState, (string id) => { });

            job = _dataContext.Jobs.Where(c => c.Id == job.Id).FirstOrDefault();
            Assert.IsNotNull(job);
            Assert.AreEqual(InitialJobPriority, job.Priority);
            job.Priority = newPriority;
            job.Update();

            job = _dataContext.Jobs.Where(c => c.Id == job.Id).FirstOrDefault();
            Assert.IsNotNull(job);
            Assert.AreEqual(newPriority, job.Priority, "Job Priority is not matching expected value");
        }
        #endregion
    }
}