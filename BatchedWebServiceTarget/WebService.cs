using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using MsgPack.Serialization;
using MsgPack;
namespace BatchedWebService
{
    public struct WebServicePayLoad
    {
        [MessagePackMember(0, Name = "version")]
        public int version;
        [MessagePackMember(1, Name = "logs")]
        public List<Dictionary<string, string>> logs;
        [MessagePackRuntimeDictionaryKeyType, MessagePackMember(2, Name = "id")]
        public string id;
    }
    public class PayLoadSerializer : MessagePackSerializer<WebServicePayLoad>
    {
        public PayLoadSerializer(SerializationContext ownerContext) : base(ownerContext) { }
        protected override void PackToCore(Packer packer, WebServicePayLoad objectTree)
        {
            var dict = new Dictionary<string,object>();
            dict["version"] = objectTree.version;
            dict["logs"] = objectTree.logs;
            dict["id"] = objectTree.id;
            packer.PackDictionary<string, object>(dict);
        }

        protected override WebServicePayLoad UnpackFromCore(Unpacker unpacker)
        {
            // we don't really do unpacking
            throw new Exception("unpacking not supported");
            return new WebServicePayLoad { version = 1 };
        }
        public static MessagePackSerializer<WebServicePayLoad> getSerializer()
        {
            var context = new SerializationContext();
            context.Serializers.RegisterOverride<WebServicePayLoad>(new PayLoadSerializer(context));
            return MessagePackSerializer.Get<WebServicePayLoad>(context);
        }
    }
    public interface IBatchWebService
    {
        void setUrl(string Url);
        bool testConnection();
        bool sendData(byte[] batchData, string id);
    }
    public class BatchWebService : IBatchWebService
    {
        protected string url;
        public void setUrl(string Url)
        {
            this.url = Url;
        }
        public bool testConnection()
        {
            WebRequest req = HttpWebRequest.Create(this.url);
            req.Proxy = null;
            using (WebResponse resp = req.GetResponse())
            {
                using (Stream stream = resp.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    String responseString = reader.ReadToEnd();
                    if (responseString == "ok")
                        return true;
                }
            }
            return false;
        }
        public bool sendData(byte[] batchData, string id)
        {
            WebRequest req = HttpWebRequest.Create(this.url);
            req.Method = "PUT";
            req.ContentType = "application/msgpack";
            req.ContentLength = batchData.Length;
            var reqStream = req.GetRequestStream();
            reqStream.Write(batchData, 0, batchData.Length);
            reqStream.Close();
            using (WebResponse resp = req.GetResponse())
            {
                using (Stream stream = resp.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    String responseString = reader.ReadToEnd();
                    return responseString.Equals(id);
                }
            }
        }
    }
}
