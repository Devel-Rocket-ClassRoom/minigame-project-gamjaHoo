// EventBusTests.cs
// EventBus DoD 검증: v0.1-tasks.md Task 2.1.

using NUnit.Framework;
using FMLite.Core;

namespace FMLite.Tests
{
    public class EventBusTests
    {
        private class DummyEvent
        {
            public int value;
        }

        [SetUp]
        public void Setup() => EventBus.Clear();

        [Test]
        public void Subscribe_PublishesToHandler()
        {
            int received = 0;
            EventBus.Subscribe<DummyEvent>(e => received = e.value);

            EventBus.Publish(new DummyEvent { value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Subscribe_DuplicateHandler_InvokedOnlyOnce()
        {
            int callCount = 0;
            void Handler(DummyEvent _) => callCount++;

            EventBus.Subscribe<DummyEvent>(Handler);
            EventBus.Subscribe<DummyEvent>(Handler);

            EventBus.Publish(new DummyEvent());

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Unsubscribe_HandlerNotInvoked()
        {
            int callCount = 0;
            void Handler(DummyEvent _) => callCount++;

            EventBus.Subscribe<DummyEvent>(Handler);
            EventBus.Unsubscribe<DummyEvent>(Handler);

            EventBus.Publish(new DummyEvent());

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new DummyEvent()));
        }
    }
}
