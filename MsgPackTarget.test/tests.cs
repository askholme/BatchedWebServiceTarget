using System;
using NUnit.Framework;
using MsgPackTarget;
using NLog.Layouts;
using NLog.Targets;
using NLog;
using Moq;
using System.IO;
using NLog.Config;
using System.Threading;
using System.Collections.Generic;
using MsgPack.Serialization;
namespace MsgPackTarget.test
{
    [TestFixture]
    public class tests
    {
        private readonly ILogger logger = LogManager.GetLogger("MsgPAckTarget.test.UnitTest");
        public DictionaryLayout getLayout()  {
            var layout = new DictionaryLayout();
            layout.Attributes.Add(new DictionaryLayoutAttribute("time", "${longdate}"));
            layout.Attributes.Add(new DictionaryLayoutAttribute("level", "${level:upperCase=True}"));
            layout.Attributes.Add(new DictionaryLayoutAttribute("message", "${message}"));
            layout.Attributes.Add(new DictionaryLayoutAttribute("machine", "${machinename}"));
            layout.Attributes.Add(new DictionaryLayoutAttribute("username", "${windows-identity}"));
            return layout;
        }
        [Test]
        public void TestLayout()
        {
            var layout = getLayout();
            LogEventInfo testEvent = new LogEventInfo(LogLevel.Error,"test","mytest");
            var output = layout.GetFormattedDict(testEvent);
            Assert.AreEqual("ERROR", output["level"]);
            Assert.AreEqual("mytest",output["message"]);
            Assert.True(output.ContainsKey("username"));
            Assert.True(output.ContainsKey("machine"));
        }
        [Test]
        public void TestTarget()
        {
            var MockedWebService = new Mock<IBatchWebService>();
            MockedWebService.Setup(batch => batch.testConnection()).Returns(true);
            var batches = new System.Collections.Generic.List<byte[]>();
            MockedWebService.Setup(batch => batch.sendData(It.IsAny<byte[]>(),It.IsAny<string>())).Returns(true).Callback<byte[],string>((bytes,id) => batches.Add(bytes));
            var target = new NLog.Targets.MsgPackTarget(MockedWebService.Object);
            target.Layout = getLayout();
            target.queuePath = Path.GetTempPath();
            target.logsPerBatch = 50;
            target.waitBetweenBatch = 300;
            target.waitBetweenConnections = 800;
            SimpleConfigurator.ConfigureForTargetLogging(target, LogLevel.Debug);                   
            var count = 140;
            for (int currentSequenceNumber = 0; currentSequenceNumber < count; currentSequenceNumber++)
                logger.Debug("Test {0}", currentSequenceNumber);
            // we sleep the time required to do all batches + some extra just for safety
            Thread.Sleep(target.waitBetweenBatch*(count/target.logsPerBatch)+target.waitBetweenConnections+100);
            Assert.DoesNotThrow(delegate { MockedWebService.Verify(batch => batch.testConnection(), Times.AtLeastOnce()); });
            Assert.DoesNotThrow(delegate {MockedWebService.Verify(batch => batch.sendData(It.IsAny<byte[]>(),It.IsAny<string>()),Times.AtLeast(2));});

            var serializer = MessagePackSerializer.Get<BatchedWebServiceTarget.WebServicePayLoad>();
            int testCount = 0;
            Dictionary<string, string> log = new Dictionary<string, string>(); ;
            foreach (var batch in batches) {
                var unpackedBatch = serializer.UnpackSingleObject(batch);
                testCount = unpackedBatch.logs.Count + testCount;
                Assert.True(unpackedBatch.logs.Count <= target.logsPerBatch);
                Assert.AreEqual(1, unpackedBatch.version);
                log = unpackedBatch.logs[unpackedBatch.logs.Count - 1];
                Assert.AreEqual(String.Format("Test {0}", testCount-1), log["message"]);
                Assert.AreEqual("DEBUG", log["level"]);
            }
            Assert.AreEqual(count,testCount);
            LogManager.Shutdown();
            
        }
    }
}
