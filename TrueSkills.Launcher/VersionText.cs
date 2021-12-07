using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TrueSkills.Launcher
{
    public class VersionText
    {
        [JsonProperty("text")]
        public string Content { get; set; }
    }
}
