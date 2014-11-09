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
        static string version = "0.1.1";

        static string username, password, authcode;

        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamUser steamUser;
        static SteamFriends steamFriends;

        static List<userPrefs> users;

        static bool isRunning = false;

        //static botInfo botInfoInstance = new botInfo();
        static botData botInstance;

        static void Main(string[] args)
        {
            Console.Title = "Bot";
            Console.WriteLine("CTRL+C to exit.");

            if (!File.Exists("configs.json") || !File.Exists("userPrefs.json"))
            {
                Console.Write("Username: ");
                username = Console.ReadLine();

                Console.Write("Password: ");
                password = Console.ReadLine();
                botInstance = new botData() { UserName = username, Password = password, overlord = "STEAM_0:1:52699854" };
                users = new List<userPrefs>();
            }
            else
            {
                // botData botInstance;// = new botData();
                string json = File.ReadAllText("configs.json");
                botInstance = JsonConvert.DeserializeObject<botData>(json);
                json = File.ReadAllText("userPrefs.json");
                users = JsonConvert.DeserializeObject<List<userPrefs>>(json);
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

            new Callback<SteamFriends.ChatInviteCallback>(onChatInvite, manager);
            new Callback<SteamFriends.ChatEnterCallback>(onChatEnter, manager);

            new Callback<SteamFriends.ClanStateCallback>(onClanState, manager);

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
            saveToFile();
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

        static void onClanState(SteamFriends.ClanStateCallback callback)
        {
            Console.WriteLine("onClanState was entered.");
        }

        static void onAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online); //set our status to Online when logged in
        }

        static void onFriendsList(SteamFriends.FriendsListCallback callback)
        {
            int friendCount = steamFriends.GetFriendCount();

            Console.WriteLine("{0} friends on friend list.", friendCount);
            Console.WriteLine("{0} groups joined.", steamFriends.GetClanCount());

            foreach (var friend in callback.FriendList)
            {
                if (friend.SteamID.AccountType == EAccountType.Clan)
                {
                    if (friend.Relationship == EFriendRelationship.RequestRecipient)
                    {
                        acceptClanInvite(friend.SteamID);
                    }
                }
                else if (friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    steamFriends.AddFriend(friend.SteamID);
                }
            }
            updateUserPrefs();
        }

        static void updateUserPrefs() //automatic users cleanup
        {
            int steamFriendCount = steamFriends.GetFriendCount();
            for (int k = 0; k < steamFriendCount; ++k )
            {
                SteamID comparisonID = steamFriends.GetFriendByIndex(k);
                if (!users.Exists(buddy => buddy.steamID == comparisonID))
                {
                    users.Add(new userPrefs() { steamID = comparisonID, subscribedToBroadcast = true });
                }
            }
            if (steamFriends.GetFriendCount() != users.Count())
            {
                bool touched = false;
                for (int i = 0; i < users.Count; ++i)
                {
                    touched = false;
                    for (int k = 0; k < steamFriendCount; ++k )
                    {
                        if (users[i].steamID == steamFriends.GetFriendByIndex(k))
                        {
                            touched = true;
                            break;
                        }
                    }
                    if (!touched)
                        users.RemoveAt(i);
                }
            }
            string json = JsonConvert.SerializeObject(users, Formatting.None);
            File.WriteAllText("userPrefs.json", json);
        }

        static void onFriendAdded(SteamFriends.FriendAddedCallback callback)
        {
            Console.WriteLine("Added new friend {0}, SteamID {1}.", callback.PersonaName, callback.SteamID);
            string response = "Added user " + callback.PersonaName + ", SteamID: " + callback.SteamID;
            steamFriends.SendChatMessage(callback.SteamID, EChatEntryType.ChatMsg, response);
        }

        static void onChatInvite(SteamFriends.ChatInviteCallback callback)
        {
            Console.WriteLine("Invited to chat with ID {0}, name {1}, and type {2}", callback.ChatRoomID, callback.ChatRoomName, callback.ChatRoomType);
            //steamFriends.JoinChat
        }

        static void onChatEnter(SteamFriends.ChatEnterCallback callback)
        {
            Console.WriteLine("onChatEnter activated");
            steamFriends.JoinChat(callback.ChatID);
        }

        static void onMessageReceive(SteamFriends.FriendMsgCallback callback)
        {
            if (callback.EntryType == EChatEntryType.ChatMsg)
            {
                if (callback.Message.Length > 1 && callback.Message[0] == '/')
                {
                    Console.WriteLine("Command {0} entered by {1}.", callback.Message, steamFriends.GetFriendPersonaName(callback.Sender));
                    parse_input(callback.Message.Remove(0,1), callback);
                }
            }
        }

        static void parse_input(string input, SteamFriends.FriendMsgCallback callback)
        {
            SteamID partner = callback.Sender;
            string[] command = input.Split(' ');
            int command_len = command.Length;
            if (command[0] == "bc" && command_len > 1)
            {
                if (command[1][0] == '@')
                {
                    parseBroadcastMessage(input.Remove(0, 4), partner); //remove 11 characters, which is "broadcast @"
                    return;
                }
            }
            else if (command[0] == "bc2" && command_len > 1)
            {
                if (command[1][0] == '@')
                {
                    overlordBroadcastMessage(input.Remove(0, 5), partner); //special broadcast for overlords only
                    return;
                }
            }
            switch (command_len)
            {
                case 1:
                    switch (command[0])
                    {
                        case "help":
                            printError(partner, "help", -1);
                            break;
                        case "amimaster":
                            amIMaster(partner);
                            break;
                        case "about":
                            printAbout(partner);
                            break;
                        case "yorick":
                            printLine(partner, "Alas, poor Yorick! I knew him, Horatio; a fellow of infinite jest, of most excellent fancy.");
                            break;
                        case "whoami":
                            whoAmI(partner);
                            break;
                        case "nukemaster":
                            OverNukeMaster(partner);
                            break;
                        case "togglebc":
                            toggleBroadcast(partner);
                            break;
                        default:
                            printError(partner, "", -1);
                            break;
                    }
                    break;
                case 2:
                    switch (command[0])
                    {
                        case "addmaster":
                            OverAddMaster(command[1], partner);
                            break;
                        case "removemaster":
                            OverRemoveMaster(command[1], partner);
                            break;
                        case "help":
                            help(command[1], partner);
                            break;
                        default:
                            printError(partner, "", -1);
                            break;
                    }
                    break;
                default:
                    printError(partner, "", -1);
                    break;
            }
            return;
        }

        static void toggleBroadcast(SteamID partner)
        {
            bool currentState = false;
            for (int i = 0; i < users.Count(); ++i)
            {
                if (partner == users[i].steamID)
                {
                    currentState = !users[i].subscribedToBroadcast;
                    users[i].subscribedToBroadcast = currentState;
                    break;
                }
            }
            if (currentState)
                printLine(partner, "You are now subscribed to broadcast notifications.");
            else
                printLine(partner, "You are no longer subscribed to broadcast notifications.");
            savePrefsToFile();
        }

        static void printAbout(SteamID partner)
        {
            printLine(partner, "\ntrinerOS is currently in version " + version + ".\ntrinerOS was created by trineroks in under 24 hours at HackSC.");
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

        static void help(string arg, SteamID partner)
        {
            int checkYourPrivilege = botInstance.hasMasterPrivileges(partner);
            switch(arg)
            {
                case "commands":
                    printLine(partner, "REGULAR: /help <arg>, /amimaster, /about, /yorick, /whoami, /togglebc");
                    if (checkYourPrivilege >= 0)
                    {
                        printLine(partner, "MASTER: /bc @message");
                    }
                    if (checkYourPrivilege == 1)
                    {
                        printLine(partner, "OVERLORD: /addmaster <arg>, /removemaster <arg>, /nukemaster");
                    }
                    break;
                case "amimaster":
                    printLine(partner, "amimaster (1) - Check if you have master privilege over this bot. \nUsage: /amimaster");
                    break;
                case "about":
                    printLine(partner, "about (1) - Check the current version of this bot as well as other information. \nUsage: /about");
                    break;
                case "yorick":
                    printLine(partner, "yorick (1) - What a shame he had to pass away. Usage: /yorick");
                    break;
                case "whoami":
                    printLine(partner, "whoami (1) - View your current username and, more importantly, your SteamID. \nUsage: /whoami");
                    break;
                case "nukemaster":
                    printLine(partner, "nukemaster (1) - Clear the entire master list from this bot. \nUsage: /nukemaster");
                    break;
                case "togglebc":
                    printLine(partner, "togglebc (1) - Opt in/opt out of other users' broadcasts. \nUsage: /togglebroadcast");
                    break;
                case "addmaster":
                    printLine(partner, "addmaster (1) - Add a new master to this bot. \nUsage: /addmaster <SteamID>");
                    break;
                case "removemaster":
                    printLine(partner, "removemaster (1) - Remove a master from this bot. \nUsage: /removemaster <SteamID>");
                    break;
                case "bc":
                    printLine(partner, "bc (1) - Broadcast a message to all users subscribed to this bot. \nUsage: /broadcast @<Message>");
                    break;
                case "bc2":
                    printLine(partner, "bc (2) - Broadcast a message to all users subscribed to this bot. This broadcast cannot be blocked and will be used sparingly. \nUsage: /broadcast2 @<Message>");
                    break;
                default:
                    printError(partner, "help", -1);
                    break;
            }
        }

        static void OverAddMaster(string steamID, SteamID partner)
        {
            if (botInstance.hasMasterPrivileges(partner) == 1)
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
                    saveToFile();
                }
            }
            else
                printError(partner, "", 1);
        }

        static void OverRemoveMaster(string steamID, SteamID partner)
        {
            if (botInstance.hasMasterPrivileges(partner) == 1)
            {
                if (botInstance.master.Remove(steamID))
                {
                    printLine(partner, steamID + " removed from masters list.");
                    saveToFile();
                }
                else
                    printLine(partner, "Unable to remove " + steamID + "; maybe it doesn't exist on the list or is typed incorrectly?");
            }
            else
                printError(partner, "", 1);
        }

        static void OverNukeMaster(SteamID partner)
        {
            if (botInstance.hasMasterPrivileges(partner) == 1)
            {
                printLine(partner, "Master list nuked.");
                botInstance.master.Clear();
                saveToFile();
            }
            else
                printError(partner, "", 1);
        }

        static void whoAmI(SteamID partner)
        {
            string partnerName = steamFriends.GetFriendPersonaName(partner);
            printLine(partner, "You are " + partnerName + " with the SteamID: " + partner.ToString());
        }

        static void printLine(SteamID recipient, string line)
        {
            steamFriends.SendChatMessage(recipient, EChatEntryType.ChatMsg, line);
        }

        static void printError(SteamID recipient, string command, int permIssue)
        {
            if (permIssue >= 0)
            {
                if (permIssue == 1)
                    printLine(recipient, "You do not have overlord privileges. Please contact trineroks our lord and savior.");
                else
                    printLine(recipient, "You do not have master privileges.");
                return;
            }
            else
                switch(command)
                {
                    case "help":
                        printLine(recipient, "Use \"/help commands\" for a list of commands, or \"/help <command>\" for help on a particular command.");
                        break;
                    default:
                        printLine(recipient, "Invalid command entered. Be wary of multiple spaces and/or typos. Try \"/help\".");
                        break;
                }
        }

        static bool overlordBroadcastMessage(string message, SteamID sender)
        {
            if (botInstance.hasMasterPrivileges(sender) == 1)
            {
                int friendCount = steamFriends.GetFriendCount();
                string senderName = steamFriends.GetFriendPersonaName(sender);
                SteamID recipient;
                if (friendCount == 0)
                    return false;
                for (int i = 0; i < users.Count(); ++i)
                {
                    recipient = users[i].steamID;
                    printLine(recipient, "Admin " + senderName + " broadcasts - " + message);
                }
                return true;
            }
            printError(sender, "", 1);
            return false;
        }

        static bool parseBroadcastMessage(string message, SteamID sender) //this is a master level command
        {
            if (botInstance.hasMasterPrivileges(sender) >= 0)
            {
                int friendCount = steamFriends.GetFriendCount();
                string senderName = steamFriends.GetFriendPersonaName(sender);
                SteamID recipient;
                userPrefs receiver;
                if (friendCount == 0)
                    return false;
                for (int i = 0; i < users.Count(); ++i)
                {
                    receiver = users[i];
                    if (receiver.subscribedToBroadcast)
                    {
                        recipient = receiver.steamID;
                        printLine(recipient, senderName + " broadcasts - " + message);
                    }
                }
                return true;
            }
            printError(sender, "", 0);
            return false;
        }

        static void saveToFile()
        {
            string json = JsonConvert.SerializeObject(botInstance, Formatting.Indented);
            File.WriteAllText("configs.json", json);
        }

        static void savePrefsToFile()
        {
            string json = JsonConvert.SerializeObject(users, Formatting.None);
            File.WriteAllText("userPrefs.json", json);
        }

        static void acceptClanInvite(SteamID group)
        {
            var acceptInvite = new ClientMsg<CMsgClanInviteAction>((int)EMsg.ClientAcknowledgeClanInvite);
            acceptInvite.Body.clanID = group.ConvertToUInt64();
            acceptInvite.Body.acceptInvite = true;
            steamClient.Send(acceptInvite);
        }
    }
}
