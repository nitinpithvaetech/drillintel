using System;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data.Objects.Logger;
using Xunit;

namespace DrillIntel.Tests
{
    public class DynamicLoggerTests : IDisposable
    {
        private readonly string _testLogDir;

        public DynamicLoggerTests()
        {
            _testLogDir = Path.Combine(Path.GetTempPath(), "DrillIntel_LoggerTests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testLogDir))
                {
                    Directory.Delete(_testLogDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void DynamicLogger_WritesAllLevelsToFile_WithCorrectFormatting()
        {
            string logFile = Path.Combine(_testLogDir, "test_log.txt");
            ILog logger = DynamicLogger.GetDynamicLogger("TestProcess", logFile);

            logger.Debug("Debug test message");
            logger.Info("Info test message");
            logger.Warn("Warning test message");
            logger.Error("Error test message");
            logger.Error("Exception error message", new InvalidOperationException("Test inner failure"));

            Assert.True(File.Exists(logFile));
            string content = File.ReadAllText(logFile);

            Assert.Contains("[DEBUG] [TestProcess] Debug test message", content);
            Assert.Contains("[INFO] [TestProcess] Info test message", content);
            Assert.Contains("[WARN] [TestProcess] Warning test message", content);
            Assert.Contains("[ERROR] [TestProcess] Error test message", content);
            Assert.Contains("[ERROR] [TestProcess] Exception error message: Test inner failure", content);
        }

        [Fact]
        public void DynamicLogger_GetDyamicLogger_SupportsLegacySpellingFactory()
        {
            string logFile = Path.Combine(_testLogDir, "legacy_factory_log.txt");
            ILog logger = DynamicLogger.GetDyamicLogger("LegacyProcess", logFile);

            logger.Info("Legacy factory message");

            Assert.True(File.Exists(logFile));
            string content = File.ReadAllText(logFile);
            Assert.Contains("[INFO] [LegacyProcess] Legacy factory message", content);
        }

        [Fact]
        public void DynamicLogger_CreatesDirectoryAutomatically_WhenDirectoryDoesNotExist()
        {
            string nestedLogFile = Path.Combine(_testLogDir, "sub1", "sub2", "sub3", "nested.log");
            ILog logger = new DynamicLogger("NestedProcess", nestedLogFile);

            logger.Info("Auto create directory test");

            Assert.True(File.Exists(nestedLogFile));
            string content = File.ReadAllText(nestedLogFile);
            Assert.Contains("Auto create directory test", content);
        }

        [Fact]
        public void DynamicLogger_ThreadSafeConcurrentWrites_DoesNotThrow()
        {
            string logFile = Path.Combine(_testLogDir, "concurrent_log.txt");
            ILog logger = new DynamicLogger("ConcurrentProcess", logFile);

            Parallel.For(0, 50, i =>
            {
                logger.Info($"Concurrent message #{i}");
                logger.Debug($"Debug concurrent #{i}");
            });

            Assert.True(File.Exists(logFile));
            string[] lines = File.ReadAllLines(logFile);
            Assert.Equal(100, lines.Length);
        }

        [Fact]
        public void DynamicLogger_DefaultsToCommonApplicationDataPath_WhenFilePathOmitted()
        {
            var logger = new DynamicLogger("BroomStickProcessor");
            Assert.NotNull(logger.FilePath);
            Assert.Contains("DrillIntel", logger.FilePath);
            Assert.Contains("BroomStickProcessor", logger.FilePath);
            Assert.EndsWith(".txt", logger.FilePath);

            string expectedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DrillIntel", "logs", "BroomStickProcessor");
            Assert.True(Directory.Exists(expectedDir), $"Expected directory {expectedDir} to exist.");

            string expectedFile = Path.Combine(expectedDir, $"BroomStickProcessor_{DateTime.Now:yyyy_MM_dd}.txt");
            Assert.Equal(expectedFile, logger.FilePath);

            // Test writing message creates the file
            logger.Info("BroomStickProcessor test log entry");
            Assert.True(File.Exists(logger.FilePath), $"Expected file {logger.FilePath} to exist.");
        }
    }
}

