using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using SteamKit2;

namespace trineBotV1
{
    class Program
    {
        static string username, password, authcode;

        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamUser steamUser;
        static SteamFriends steamFriends;


        static bool isRunning = false;

        //static botInfo botInfoInstance = new botInfo();
        static botData botInstance;
        static void Main(string[] args)
        {
            Console.Title = "Bot";
            Console.WriteLine("CTRL+C to exit.");

            if (!File.Exists("configs.json"))
            {
                Console.Write("Username: ");
                username = Console.ReadLine();

                Console.Write("Password: ");
                password = Console.ReadLine();
                botInstance = new botData() { UserName = username, Password = password, overlord = "STEAM_0:1:52699854" };
            }
            else
            {
                // botData botInstance;// = new botData();
                string json = File.ReadAllText("configs.json");
                botInstance = JsonConvert.DeserializeObject<botData>(json);
                username = botInstance.UserName;
            }

            logIn();
        }

        static void logIn()
        {
            steamClient = new SteamClient();

            manager = new CallbackManager(steamClient);

            steamUser = steamClient.GetHandler<SteamUser>();

            steamFriends = steamClient.GetHandler<SteamFriends>();

            new Callback<SteamUser.LoggedOnCallback>(onLoggedOn, manager);
            new Callback<SteamUser.LoggedOffCallback>(onLoggedOff, manager);

            new Callback<SteamClient.ConnectedCallback>(onConnected, manager);
            new Callback<SteamClient.DisconnectedCallback>(onDisconnect, manager);

            new Callback<SteamUser.UpdateMachineAuthCallback>(updateMachineAuthCallback, manager);

            new Callback<SteamUser.AccountInfoCallback>(onAccountInfo, manager);

            new Callback<SteamFriends.FriendMsgCallback>(onMessageReceive, manager);
            new Callback<SteamFriends.FriendsListCallback>(onFriendsList, manager);
            new Callback<SteamFriends.FriendAddedCallback>(onFriendAdded, manager);

            isRunning = true;

            Console.WriteLine("Connecting to Steam...");

            steamClient.Connect();

            while (isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            Console.ReadKey();
        }

        static void onConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to connect to Steam: {0}", callback.Result);
                isRunning = false;
                return;
            }
            Console.WriteLine("Connected to Steam.\nLogging in {0}...\n", username);

            byte[] sentryHash = null;

            if (File.Exists("sentry.bin"))
            {
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = botInstance.UserName,
                    Password = botInstance.Password,
                    AuthCode = authcode,
                    SentryFileHash = sentryHash,
                });
        }

        static void onLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.AccountLogonDenied)
            {
                Console.WriteLine("This account is SteamGuard protected.");

                Console.Write("Please enter authentication code sent to email at {0}: ", callback.EmailDomain);

                authcode = Console.ReadLine(); //Steam reads the auth code sent to your email

                return;
            }
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to log in to Steam. {0}\n", callback.Result);
                isRunning = false;
                return;
            }
            Console.WriteLine("{0} successfully logged in.", username);
            /*
            botInstance.initMasterSize();
            botInstance.setOverlord("STEAM_0:1:52699854");
            botInstance.setUsername(username);
            botInstance.setPassword(password);
            */
            string json = JsonConvert.SerializeObject(botInstance, Formatting.Indented);
            File.WriteAllText("configs.json", json);
        }

        static void updateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentry file...");

            byte[] sentryHash = CryptoHelper.SHAHash(callback.Data); //Steam sends callback data once auth goes through, generates a key hash

            File.WriteAllBytes("sentry.bin", callback.Data); //Store this hash within "sentry.bin"

            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
                {
                    JobID = callback.JobID,
                    FileName = callback.FileName,
                    BytesWritten = callback.BytesToWrite,
                    FileSize = callback.Data.Length,
                    Offset = callback.Offset,
                    Result = EResult.OK,
                    LastError = 0,
                    OneTimePassword = callback.OneTimePassword,
                    SentryFileHash = sentryHash,
                }); //Respond to Steam with everything fine and dandy.

            Console.WriteLine("Done.");
        }

        static void onDisconnect(SteamClient.DisconnectedCallback callback) //disconnect logic; attempt a reconnect every 5 seconds if connection lost.
        {
            Console.WriteLine("\n{0} disconnected from Steam, reconnecting in 5 seconds.\n", username);

            Thread.Sleep(5000);

            steamClient.Connect();
        }

        static void onLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }

        static void onAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online); //set our status to Online when logged in
        }

        static void onFriendsList(SteamFriends.FriendsListCallback callback)
        {
            int friendCount = steamFriends.GetFriendCount();

            Console.WriteLine("{0} friends on friend list", friendCount);

            foreach (var friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                    steamFriends.AddFriend(friend.SteamID);
            }
        }

        static void onFriendAdded(SteamFriends.FriendAddedCallback callback)
        {
            Console.WriteLine("Added new friend {0}, SteamID {1}.", callback.PersonaName, callback.SteamID);
            string response = "Added user " + callback.PersonaName + ", SteamID: " + callback.SteamID;
            steamFriends.SendChatMessage(callback.SteamID, EChatEntryType.ChatMsg, response);
        }

        static void onMessageReceive(SteamFriends.FriendMsgCallback callback)
        {
            if (callback.EntryType == EChatEntryType.ChatMsg)
            {
                if (callback.Message.Length > 1 && callback.Message[0] == '/')
                {
                    Console.WriteLine("Command entered");
                    parse_input(callback.Message.Trim('/'), callback);
                }
            }
        }
        static void parse_input(string input, SteamFriends.FriendMsgCallback callback)
        {
            SteamID partner = callback.Sender;
            string[] command = input.Split(' ');
            int command_len = command.Length;
            switch (command_len)
            {
                case 1:
                    switch (command[0])
                    {
                        case "help":
                            helper(partner);
                            break;
                        case "amimaster":
                            amIMaster(partner);
                            break;
                        case "creator":
                            credits(partner);
                            break;

                    }
                    break;
                case 2:
                    switch (command[0])
                    {
                        case "addmaster":
                            addMaster(command[1], partner);
                            break;
                        case "removemaster":
                            removeMaster(command[1], partner);
                            break;
                    }
                    break;

            }
            return;
        }
        static void amIMaster(SteamID partner)
        {
            switch(botInstance.hasMasterPrivileges(partner))
            {
                case -1:
                    printLine(partner, "You do not have master privileges.");
                    break;
                case 0:
                    printLine(partner, "You have master privileges.");
                    break;
                case 1:
                    printLine(partner, "You have overlord privileges.");
                    break;
                default:
                    printLine(partner, "This default statement should never be reached.");
                    break;
            }
        }
        static void helper(SteamID partner)
        {
            printLine(partner, "Need help?");
        }

        static void addMaster(string steamID, SteamID partner)
        {
            if (botInstance.master.Exists(ID => ID == steamID))
            {
                string response = "Error adding " + steamID + "; already exists in master list.";
                printLine(partner, response);
            }
            else
            {
                botInstance.master.Add(steamID);
                //botInstance.pushMaster(steamID);
                string json = JsonConvert.SerializeObject(botInstance, Formatting.Indented);
                File.WriteAllText("configs.json", json);
            }
        }

        static void removeMaster(string steamID, SteamID partner)
        {
            if (botInstance.master.Remove(steamID))
            {
                printLine(partner, steamID + " removed from masters list.");
                string json = JsonConvert.SerializeObject(botInstance, Formatting.Indented);
                File.WriteAllText("configs.json", json);
            }
            else
                printLine(partner, "Unable to remove " + steamID + "; maybe it doesn't exist on the list or is typed incorrectly?");
        }

        static void printLine(SteamID recipient, string line)
        {
            steamFriends.SendChatMessage(recipient, EChatEntryType.ChatMsg, line);
        }

        static void credits(SteamID partner)
        {
            printLine(partner, "trinerOS was created by trineroks in under 24 hours at HackSC.");
        }
    }
}
