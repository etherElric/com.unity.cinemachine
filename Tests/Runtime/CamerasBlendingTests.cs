using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cinemachine;
using Tests.Runtime;

namespace Tests.Runtime
{
    [TestFixture]
    public class CamerasBlendingTests : CinemachineFixtureBase
    {
        private const float BlendingTime = 1;

        private CinemachineBrain brain;
        private CinemachineVirtualCamera targetVCam;

        [SetUp]
        public override void SetUp()
        {
            // Camera
            var cameraHolder = CreateGameObject("MainCamera", typeof(Camera), typeof(CinemachineBrain));
            brain = cameraHolder.GetComponent<CinemachineBrain>();

            // Blending
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.Linear,
                BlendingTime);

            var followObject = CreateGameObject("Follow Object");

            // Source vcam
            var sourceVCam = CreateGameObject("Source CM Vcam", typeof(CinemachineVirtualCamera)).GetComponent<CinemachineVirtualCamera>();
            sourceVCam.Priority = 2;
            sourceVCam.Follow = followObject.transform;

            // target vcam
            targetVCam = CreateGameObject("Target CM Vcam", typeof(CinemachineVirtualCamera)).GetComponent<CinemachineVirtualCamera>();
            targetVCam.Priority = 1;
            targetVCam.Follow = followObject.transform;

            base.SetUp();
        }

        [UnityTest]
        public IEnumerator BlendingBetweenCameras()
        {
            targetVCam.Priority = 3;
            yield return null;

            yield return new WaitForSeconds(BlendingTime + 0.01f);
            Assert.That(brain.IsBlending, Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptedBlendingBetweenCameras()
        {
            // Start blending
            targetVCam.Priority = 3;
            yield return null;

            // Wait for 90% of blending duration
            yield return new WaitForSeconds(BlendingTime * 0.9f);

            // Blend back to source
            targetVCam.Priority = 1;
            yield return null;
            yield return new WaitForSeconds(BlendingTime * 0.1f);

            // Quickly blend to target again
            targetVCam.Priority = 3;
            yield return null;

            // We went 90%, then got 10% back, it means we are 20% away from the target
            yield return new WaitForSeconds(BlendingTime * 0.21f);

            Assert.That(brain.IsBlending, Is.False);

            // Start blending
            targetVCam.Priority = 3;
            yield return null;

            // Wait for 90% of blending duration
            yield return new WaitForSeconds(BlendingTime * 0.9f);

            // Blend back to source
            targetVCam.Priority = 1;
            yield return null;
            yield return new WaitForSeconds(BlendingTime * 0.1f);

            // Quickly blend to target again
            targetVCam.Priority = 3;
            yield return null;

            // We went 90%, then got 10% back, it means we are 20% away from the target - wait only 10% worth
            yield return new WaitForSeconds(BlendingTime * 0.1f);

            // Blend back to source
            targetVCam.Priority = 1;
            yield return null;
            yield return new WaitForSeconds(BlendingTime * 0.1f);

            // Quickly blend to target again
            targetVCam.Priority = 3;
            yield return null;

            // We went 90%, then got 10% back, it means we are 20% away from the target
            yield return new WaitForSeconds(BlendingTime * 0.21f);

            Assert.That(brain.IsBlending, Is.False);
        }

        [UnityTest]
        public IEnumerator DoesInterruptedBlendingBetweenCamerasTakesDoubleTime()
        {
            // Start blending
            targetVCam.Priority = 3;
            yield return null;

            // Wait for 90% of blending duration
            yield return new WaitForSeconds(BlendingTime * 0.9f);

            // Blend back to source
            targetVCam.Priority = 1;
            yield return null;
            yield return new WaitForSeconds(BlendingTime * 0.1f);

            // Quickly blend to target again
            targetVCam.Priority = 3;
            yield return null;

            // We went 90%, then got 10% back, it means we are 20% away from the target
            yield return new WaitForSeconds(BlendingTime + 0.01f);

            Assert.That(brain.IsBlending, Is.False);
        }

        [UnityTest]
        public IEnumerator SetActiveBlend()
        {
            // Start blending
            targetVCam.Priority = 3;

            // Wait 5 frames
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            var blend = brain.ActiveBlend;
            float percentComplete = blend.TimeInBlend / blend.Duration;

            // Freeze the blend
            blend.TimeInBlend -= CinemachineCore.UniformDeltaTimeOverride;
            brain.ActiveBlend = blend;

            // Wait a frame and check that TimeInBlend is the same
            yield return null;
            blend = brain.ActiveBlend;
            Assert.That(percentComplete == blend.TimeInBlend / blend.Duration);

            // Force the blend to complete
            blend.Duration = 0;
            brain.ActiveBlend = blend;

            // Wait a frame and check that blend is finished
            yield return null;
            Assert.That(brain.ActiveBlend == null);

            // Blend back to source
            targetVCam.Priority = 1;

            // Wait 5 frames
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            blend = brain.ActiveBlend;
            Assert.That(percentComplete == blend.TimeInBlend / blend.Duration);

            // Kill the blend
            brain.ActiveBlend = null;

            // Wait a frame and check that blend is finished
            yield return null;
            Assert.That(brain.ActiveBlend == null);
        }
    }
}