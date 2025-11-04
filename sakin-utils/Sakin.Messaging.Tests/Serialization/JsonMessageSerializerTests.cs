using FluentAssertions;
using Sakin.Messaging.Serialization;

namespace Sakin.Messaging.Tests.Serialization
{
    public class JsonMessageSerializerTests
    {
        private readonly JsonMessageSerializer _serializer;

        public JsonMessageSerializerTests()
        {
            _serializer = new JsonMessageSerializer();
        }

        [Fact]
        public void Serialize_WithValidObject_ReturnsBytes()
        {
            var testObject = new TestMessage { Id = "123", Name = "Test", Value = 42 };

            var result = _serializer.Serialize(testObject);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }

        [Fact]
        public void Serialize_WithNull_ThrowsArgumentNullException()
        {
            TestMessage? testObject = null;

            var act = () => _serializer.Serialize(testObject!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Deserialize_WithValidBytes_ReturnsObject()
        {
            var testObject = new TestMessage { Id = "123", Name = "Test", Value = 42 };
            var bytes = _serializer.Serialize(testObject);

            var result = _serializer.Deserialize<TestMessage>(bytes);

            result.Should().NotBeNull();
            result!.Id.Should().Be("123");
            result.Name.Should().Be("Test");
            result.Value.Should().Be(42);
        }

        [Fact]
        public void Deserialize_WithEmptyBytes_ThrowsArgumentException()
        {
            var bytes = Array.Empty<byte>();

            var act = () => _serializer.Deserialize<TestMessage>(bytes);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Deserialize_WithNullBytes_ThrowsArgumentException()
        {
            byte[]? bytes = null;

            var act = () => _serializer.Deserialize<TestMessage>(bytes!);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SerializeDeserialize_WithComplexObject_PreservesData()
        {
            var testObject = new ComplexTestMessage
            {
                Id = "456",
                Nested = new TestMessage { Id = "789", Name = "Nested", Value = 100 },
                Items = new List<string> { "item1", "item2", "item3" },
                Timestamp = DateTime.UtcNow
            };

            var bytes = _serializer.Serialize(testObject);
            var result = _serializer.Deserialize<ComplexTestMessage>(bytes);

            result.Should().NotBeNull();
            result!.Id.Should().Be("456");
            result.Nested.Should().NotBeNull();
            result.Nested!.Id.Should().Be("789");
            result.Items.Should().HaveCount(3);
            result.Items.Should().Contain("item1");
        }

        [Fact]
        public void Serialize_UsesCamelCase()
        {
            var testObject = new TestMessage { Id = "123", Name = "Test", Value = 42 };

            var bytes = _serializer.Serialize(testObject);
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            json.Should().Contain("\"id\"");
            json.Should().Contain("\"name\"");
            json.Should().Contain("\"value\"");
            json.Should().NotContain("\"Id\"");
        }

        [Fact]
        public void Serialize_WithEnum_UsesCamelCase()
        {
            var testObject = new TestMessageWithEnum { Status = TestStatus.InProgress };

            var bytes = _serializer.Serialize(testObject);
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            json.Should().Contain("\"inProgress\"");
            json.Should().NotContain("\"InProgress\"");
        }

        private record TestMessage
        {
            public string Id { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int Value { get; init; }
        }

        private record ComplexTestMessage
        {
            public string Id { get; init; } = string.Empty;
            public TestMessage? Nested { get; init; }
            public List<string> Items { get; init; } = new();
            public DateTime Timestamp { get; init; }
        }

        private record TestMessageWithEnum
        {
            public TestStatus Status { get; init; }
        }

        private enum TestStatus
        {
            Pending,
            InProgress,
            Completed
        }
    }
}
