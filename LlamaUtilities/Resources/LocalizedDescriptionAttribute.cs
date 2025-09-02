using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlamaUtilities.LlamaUtilities.Localization
{
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        static string Localize(string key)
        {
            return Resources.Localization.ResourceManager.GetString(key);
        }

        public LocalizedDescriptionAttribute(string key): base(Localize(key))
        {
        }
    }
}
