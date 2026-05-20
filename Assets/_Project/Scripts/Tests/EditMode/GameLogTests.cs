// GameLogTests.cs
// DoD 검증: v0.1-tasks.md Task 2.4.

using FMLite.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FMLite.Tests
{
    public class GameLogTests
    {
        [SetUp]
        public void Setup() => GameLog.IsDebugMode = true;

        [Test]
        public void Log_WhenDebugMode_PrintsWithCategoryPrefix()
        {
            LogAssert.Expect(LogType.Log, "[Match] hello");
            GameLog.Log(LogCategory.Match, "hello");
        }

        [Test]
        public void Log_WhenDebugModeOff_DoesNotPrint()
        {
            GameLog.IsDebugMode = false;
            GameLog.Log(LogCategory.Match, "should not appear");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Warn_PrintsRegardlessOfDebugMode()
        {
            GameLog.IsDebugMode = false;
            LogAssert.Expect(LogType.Warning, "[Transfer] warn msg");
            GameLog.Warn(LogCategory.Transfer, "warn msg");
        }

        [Test]
        public void Error_PrintsRegardlessOfDebugMode()
        {
            GameLog.IsDebugMode = false;
            LogAssert.Expect(LogType.Error, "[Youth] err msg");
            GameLog.Error(LogCategory.Youth, "err msg");
        }
    }
}
