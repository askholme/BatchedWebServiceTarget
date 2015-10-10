using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NLog;
using NLog.Targets;
using NLog.Internal;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using DiskQueue;
using System.IO;
using MsgPack.Serialization;
using BatchedWebService;
namespace NLog.Targets
{
    // make layout with custom output method, modelled after json layout but returning a dict(string,string). 
    // then message pack the dict for disk queue storage and unpack afterwards

    [Target("BatchedWebServiceTarget")]
    public class BatchedWebServiceTarget : Target
    {
        public struct WebServicePayLoad
        {
            public int version;
            public List<Dictionary<string, string>> logs;
            public string id;
        }
        protected IPersistentQueue queue;
        protected Thread backgroundThread;
        protected MessagePackSerializer<Dictionary<String,String>> logEventSerializer;
        protected MessagePackSerializer<WebServicePayLoad> logBatchSerializer;
        protected IBatchWebService webService;
        public BatchedWebServiceTarget(IBatchWebService WebService = null)
        {
            this.logEventSerializer = MessagePackSerializer.Get<Dictionary<String,String>>();
            this.logBatchSerializer = MessagePackSerializer.Get<WebServicePayLoad>();
            if (WebService == null) {
                this.webService = new BatchWebService();
            }else {
                this.webService = WebService;
            }
            
            // probably need to set some attributes here
            this.Layout = new DictionaryLayout();
        }

        [RequiredParameter]
        public virtual DictionaryLayout Layout { get; set; }
        protected override void InitializeTarget() {
            // we setup thread here so that config is in place
            base.InitializeTarget();
            if (this.queuePath == null)
            {
                throw new NLog.NLogConfigurationException("no filesystem queue path provided to BatchedWebService target");
            }
            this.queue = new PersistentQueue(this.queuePath);
            this.backgroundThread = new Thread(new ThreadStart(runThread));
            this.backgroundThread.Start();
        }
        
        protected virtual void runThread()
        {
            try {
                while (true) { 
                    while (this.webService.testConnection()) {
                        // we got some connection
                        // make a dictionary which we later send in msgpack
                        var payLoad = new WebServicePayLoad();
                        payLoad.version = this.protocolversion;
                        payLoad.id = Guid.NewGuid().ToString();
                        // logs will be dictionaries stored in an array
                        payLoad.logs = new List<Dictionary<string,string>>();
                        // get logs from the queue and send them
                        using (var session = queue.OpenSession()) {
                            try {
                                int i = 0;
                                while (i<this.logsPerBatch) {
                                    var pack = session.Dequeue();
                                    // if task is null then queue is empty so we just submit
                                    if (pack == null) {
                                        break;
                                    }
                                    // unpack to dictionary and add to array
                                    var eventPayload = this.logEventSerializer.UnpackSingleObject(pack);
                                    payLoad.logs.Add(eventPayload);
                                    i++;
                                }
                                // if we havn't got any logs after this the queue is empty and we break the connection loop (to wait the long time)
                                if (payLoad.logs.Count == 0)
                                {
                                    break;
                                }
                                var dataToSend = this.logBatchSerializer.PackSingleObject(payLoad);
                                var resp = this.webService.sendData(dataToSend,payLoad.id);
                                if (resp) {
                                    // everything went well so we acknowledge the stuff we picked from the queue
                                     session.Flush();
                                }
                                else {
                                    // break the loop on error
                                    InternalLogger.Error("get unexpected response {0} from log server",resp);
                                    break;
                                }

                            } catch (Exception ex) {
                                InternalLogger.Error("error in messagepack target when creating batch {0} {1} {2}",ex.Message,ex.StackTrace,ex.ToString());
                                break;
                            }
                            
                        }
                        // we submitted a batch and now sleep before the next one
                        Thread.Sleep(this.waitBetweenBatch);
                    }
                    // connect failed wait some time before trying again
                    Thread.Sleep(this.waitBetweenConnections);
                }
            }
            catch (ThreadInterruptedException) {
                this.queue.Dispose();
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Something failed in MsgPackTarget background thread {0} {1} {2}", ex.Message, ex.ToString(), ex.StackTrace);
            }
        }
        public string url { get; set; }
        public string queuePath;
        public int protocolversion = 1;
        public int logsPerBatch = 100;
        public int waitBetweenBatch = 5000;
        public int waitBetweenConnections = 300000;
        protected override void Write(LogEventInfo logEvent)
        {
            using (var session = queue.OpenSession())
            {
                var payLoad = this.Layout.GetFormattedDict(logEvent);
                var packed = this.logEventSerializer.PackSingleObject(payLoad);
                session.Enqueue(packed);
                session.Flush();
            }
        }
        protected override void CloseTarget()
        {
            // gracefully shutdown thread

            base.CloseTarget();
            this.backgroundThread.Interrupt();
            while (this.backgroundThread.IsAlive) {
                // wait for stuff to finish
            }
            return;
        }

    }
}
