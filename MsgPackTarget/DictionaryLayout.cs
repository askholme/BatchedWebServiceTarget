using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog.Layouts;
using NLog.Config;

namespace NLog.Layouts
{
    [NLogConfigurationItem]
    public class DictionaryLayoutAttribute
    {
        public DictionaryLayoutAttribute() : this(null, null) { }
        public DictionaryLayoutAttribute(string name, string layout)
        {
            this.Name = name;
            this.Layout = layout;
        }
        [RequiredParameter]
        public string Name { get; set; }
        [RequiredParameter]
        public Layout Layout { get; set; }
    }
    [Layout("DictionaryLayout")]
    public class DictionaryLayout : Layout
    {
        [ArrayParameter(typeof(DictionaryLayoutAttribute), "attribute")]
        public IList<DictionaryLayoutAttribute> Attributes { get; private set; }
        public DictionaryLayout()
        {
            this.Attributes = new List<DictionaryLayoutAttribute>();
        }
        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            throw new Exception("GetFormattedMessage must not be called on MsgPackLayout");
        }
        public Dictionary<String, String> GetFormattedDict(LogEventInfo logEvent)
        {
            // we use the json wrapper to create elements
            var output = new Dictionary<String, String>();
            foreach (var col in this.Attributes)
            {
                output.Add(col.Name,col.Layout.Render(logEvent));
            }
            return output;
        }
    }
}
