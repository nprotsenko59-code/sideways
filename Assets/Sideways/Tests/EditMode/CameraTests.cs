using NUnit.Framework;
using Sideways.CameraSystem;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class CameraTests
    {
        [Test]
        public void CameraViewToggleAlternatesBetweenChaseAndOverhead()
        {
            var gameObject = new GameObject("CameraModeTest");
            try
            {
                var controller = gameObject.AddComponent<ChaseCameraController>();
                Assert.That(controller.ViewMode, Is.EqualTo(CameraViewMode.Chase));

                controller.ToggleViewMode();
                Assert.That(controller.ViewMode, Is.EqualTo(CameraViewMode.Overhead));

                controller.ToggleViewMode();
                Assert.That(controller.ViewMode, Is.EqualTo(CameraViewMode.Chase));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
