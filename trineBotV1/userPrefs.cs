using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using SteamKit2;

namespace trineBotV1
{
    class userPrefs
    {
        public SteamID steamID;
        public bool subscribedToBroadcast = true;
    }
}
