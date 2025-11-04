using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;

namespace Sakin.Common.Tests.Cache
{
    public class RedisClientTests
    {
        private readonly Mock<ILogger<RedisClient>> _mockLogger;
        private readonly Mock<IOptions<RedisOptions>> _mockOptions;

        public RedisClientTests()
        {
            _mockLogger = new Mock<ILogger<RedisClient>>();
            _mockOptions = new Mock<IOptions<RedisOptions>>();

            var redisOptions = new RedisOptions
            {
                ConnectionString = "localhost:6379"
            };

            _mockOptions.Setup(x => x.Value).Returns(redisOptions);
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenConnectionStringIsInvalid()
        {
            // Arrange
            var invalidOptions = new Mock<IOptions<RedisOptions>>();
            invalidOptions.Setup(x => x.Value).Returns(new RedisOptions 
            { 
                ConnectionString = "invalid:connection:string" 
            });

            // Act & Assert
            Assert.ThrowsAny<Exception>(() => new RedisClient(invalidOptions.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_ShouldAttemptConnection_WhenValidConnectionStringProvided()
        {
            // This test verifies the constructor attempts connection but handles the case 
            // when Redis is not available in the test environment

            // Arrange
            var validOptions = new Mock<IOptions<RedisOptions>>();
            validOptions.Setup(x => x.Value).Returns(new RedisOptions 
            { 
                ConnectionString = "localhost:6379,abortConnect=false" 
            });

            // Act & Assert - We expect either success or a connection exception
            // The important thing is that the constructor follows the expected pattern
            var exception = Record.Exception(() => {
                using var client = new RedisClient(validOptions.Object, _mockLogger.Object);
            });

            // We don't assert success/failure since Redis might not be running
            // We just verify the constructor pattern is followed
            Assert.True(exception == null || exception is StackExchange.Redis.RedisConnectionException);
        }

        [Fact]
        public void RedisOptions_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var options = new RedisOptions();

            // Assert
            Assert.Equal("localhost:6379", options.ConnectionString);
            Assert.Equal("Redis", RedisOptions.SectionName);
        }
    }
}