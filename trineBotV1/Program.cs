using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

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

        static void Main(string[] args)
        {
            Console.Title = "Bot";
            Console.WriteLine("CTRL+C to exit.");

            Console.Write("Username: ");
            username = Console.ReadLine();

            Console.Write("Password: ");
            password = Console.ReadLine();

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



            isRunning = true;

            Console.WriteLine("Connecting to Steam...");

            steamClient.Connect();

            while(isRunning)
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
                    Username = username,
                    Password = password,
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

                authcode = Console.ReadLine();
                //isRunning = false;
                return;
            }
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to log in to Steam. {0}\n", callback.Result);
                isRunning = false;
                return;
            }
            Console.WriteLine("{0} successfully logged in.", username);
        }

        static void updateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentry file...");

            byte[] sentryHash = CryptoHelper.SHAHash(callback.Data);

            File.WriteAllBytes("sentry.bin", callback.Data);

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
                });

            Console.WriteLine("Done.");
        }

        static void onDisconnect(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("\n{0} disconnected from Steam, reconnecting in 5 seconds.\n", username);

            Thread.Sleep(5000);

            steamClient.Connect();
        }

        static void onLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }


    }
}
