using System;
using NUnit.Framework;
using Sideways.UI;

namespace Sideways.Tests
{
    public sealed class PlayerPreferencesTests
    {
        [Test]
        public void PreferencesRoundTripThroughLocalStore()
        {
            var store = new PlayerPreferencesStore($"sideways.tests.{Guid.NewGuid():N}");
            var expected = new PlayerPreferences(false, false, true, true, true);

            store.Save(expected);

            Assert.That(store.Load(), Is.EqualTo(expected));
            store.Clear();
        }

        [Test]
        public void NewStoreUsesAccessibleDefaults()
        {
            var store = new PlayerPreferencesStore($"sideways.tests.{Guid.NewGuid():N}");

            Assert.That(store.Load(), Is.EqualTo(PlayerPreferences.Default));
            Assert.That(store.Load().AudioEnabled, Is.True);
            Assert.That(store.Load().HapticsEnabled, Is.True);
            store.Clear();
        }
    }
}
