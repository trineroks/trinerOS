using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using SteamKit2;

namespace trineBotV1
{
    class botData
    {

        public botData()
        {
            //init here
            
        }

        public string UserName;
        public string Password;
        public List<string> master = new List<string>(15); //Davi's arbitrary number
        public string overlord; //All hail the overlord
        public int masterSize=0;

        public string getMaster(int index)
        {
            if (index >= masterSize)
                return "NULL";
            return master[index];
        }

        //public int pushMaster(string steamID) //overlord only
        //{
        //    if (masterSize >= 15)
        //        return -1; //Exceeded master limit
        //    foreach (string ID in master)
        //    {
        //        if (ID == steamID)
        //            return 0; //No copies of masters allowed
        //    }
        //    masterSize++;
        //    if (masterSize != 1)
        //    {
        //        for (int i = masterSize - 1; i > 0; --i)
        //            master[i] = master[i - 1];
        //    }
        //    master[0] = steamID;
        //    return 1; //Master added
        //}

        public bool popMaster(string steamID) //overlord only
        {
            if (masterSize <= 0)
                return false;
            int index = 0;
            foreach (string ID in master)
            {
                if (ID == steamID)
                {
                    if (index == 14)
                    {
                        master.RemoveAt(index);
                    }
                    for (int i = index; i < masterSize; ++i)
                    {
                        master[i] = master[i + 1];
                    }
                    master.RemoveAt(masterSize - 1);
                    masterSize--;
                    return true;
                }
                index++;
            }
            return false;
        }

        public void nukeMaster() //overlord only
        {
            master.Clear();
        }

        public int hasMasterPrivileges(SteamID steamID)
        {
            string stringID = steamID.ToString();
            //string stringID = steamID.ConvertToString;
            if (stringID == overlord) //because all hail the overlord
                return 1;
            else
            {
                if (master.Exists(ID => ID == stringID))
                    return 0; //you have master privileges
                return -1;
            }
        }
    }
}
