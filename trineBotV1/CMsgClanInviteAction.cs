using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using SteamKit2;
using SteamKit2.Internal;

//Thank you SteamBot

namespace trineBotV1 
{
    class CMsgClanInviteAction : ISteamSerializableMessage, ISteamSerializable
    {
        public ulong clanID = 0;
        public bool acceptInvite = true;

        EMsg ISteamSerializableMessage.GetEMsg()
        {
            return EMsg.ClientAcknowledgeClanInvite;
        }

        public CMsgClanInviteAction() { }

        void ISteamSerializable.Serialize(Stream stream)
        {
            try
            {
                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(clanID);
                writer.Write(acceptInvite);
            }
            catch
            {
                throw new IOException();
            }
        }
        void ISteamSerializable.Deserialize(Stream stream)
        {
            try
            {
                BinaryReader reader = new BinaryReader(stream);
                clanID = reader.ReadUInt64();
                acceptInvite = reader.ReadBoolean();
            }
            catch
            {
                throw new IOException();
            }
        }
    }
}
