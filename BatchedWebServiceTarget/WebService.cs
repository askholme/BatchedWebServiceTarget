using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
namespace BatchedWebService
{
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
            req.ContentType = "application/octet-stream";
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
