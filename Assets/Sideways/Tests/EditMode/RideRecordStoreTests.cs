using NUnit.Framework;
using Sideways.Core;

namespace Sideways.Tests
{
    public sealed class RideRecordStoreTests
    {
        private RideRecordStore store;

        [SetUp]
        public void SetUp()
        {
            store = new RideRecordStore("sideways.tests.ride");
            store.Clear();
        }

        [TearDown]
        public void TearDown() => store.Clear();

        [Test]
        public void RecordKeepsLatestAndHighestScores()
        {
            store.Record(120);
            store.Record(80);
            Assert.That(store.LastScore, Is.EqualTo(80));
            Assert.That(store.BestScore, Is.EqualTo(120));
        }

        [Test]
        public void RecordClampsNegativeScore()
        {
            store.Record(-20);
            Assert.That(store.LastScore, Is.Zero);
            Assert.That(store.BestScore, Is.Zero);
        }
    }
}
