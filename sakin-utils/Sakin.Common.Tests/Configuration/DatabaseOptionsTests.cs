using Sakin.Common.Configuration;
using Xunit;

namespace Sakin.Common.Tests.Configuration
{
    public class DatabaseOptionsTests
    {
        [Fact]
        public void GetConnectionString_ReturnsCorrectFormat()
        {
            var options = new DatabaseOptions
            {
                Host = "testhost",
                Username = "testuser",
                Password = "testpass",
                Database = "testdb",
                Port = 5433
            };

            string connectionString = options.GetConnectionString();

            Assert.Contains("Host=testhost", connectionString);
            Assert.Contains("Username=testuser", connectionString);
            Assert.Contains("Password=testpass", connectionString);
            Assert.Contains("Database=testdb", connectionString);
            Assert.Contains("Port=5433", connectionString);
        }

        [Fact]
        public void DatabaseOptions_HasCorrectDefaults()
        {
            var options = new DatabaseOptions();

            Assert.Equal("localhost", options.Host);
            Assert.Equal("postgres", options.Username);
            Assert.Equal(string.Empty, options.Password);
            Assert.Equal("network_db", options.Database);
            Assert.Equal(5432, options.Port);
        }

        [Fact]
        public void SectionName_IsCorrect()
        {
            Assert.Equal("Database", DatabaseOptions.SectionName);
        }
    }
}
