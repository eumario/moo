﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace moo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Moo!");
            CancellationTokenSource cts = new CancellationTokenSource();

            Console.Out.WriteLine("Initializing sqlite storage provider");
            SqliteStorageProvider storageProvider = new SqliteStorageProvider();
            storageProvider.Initialize();
            ThingRepository.setStorageProvider(storageProvider);

            Console.Out.WriteLine("Loading initial pillars");
            Player consolePlayer = LoadSandbox();

            Console.Out.WriteLine("Loading built-in actions");
            LoadBuiltInActions();

            Task consoleTask = Task.Factory.StartNew(async () =>
            {
                await Console.Out.WriteLineAsync("Starting console interface");
                do
                {
                    String input = await Console.In.ReadLineAsync();
                    consolePlayer.receiveInput(input + "\r\n");
                } while (true);
            });

            do
            {
                Parallel.ForEach(Player.all(), async (player) =>
                {
                    CommandResult command = await player.popCommand();
                    if (default(CommandResult).Equals(command))
                        return;

                    await Console.Out.WriteLineAsync($"{player.name}(#{player.id}): {command.raw}");

                    var result = await CommandHandler.HandleHumanCommandAsync(player, command, cts.Token);
                    if (!result.isSuccess)
                    {
                        await player.sendOutput("ERROR: " + result.reason);
                    }
                });

            } while (!cts.IsCancellationRequested);
        }

        static void LoadBuiltInActions()
        {

            foreach (var actionType in new Action[] { new @Load(), new Look(), new @Save() })
            {
                Action actionObject = ThingRepository.Insert(actionType);
                CommandHandler.actions.TryAdd(actionObject.id, actionObject);
            }
        }

        static Player LoadSandbox()
        {
            Room aether = Room.Make("The Aether");
            aether.internalDescription = "You see a massless, infinite black void stretching forever in all directions and across all time.";
            HostPlayer.make("God", aether);
            var player = ConsolePlayer.make("Intrepid Hero", aether);

            Script.Make("foo", "\"foo\" \"bar\" \"baz\" 2 rotate 2 rotate 3 rotate -2 rotate 3 pick POP POP POP POP");

            Script.Make("test-rotate1", "\"a\" \"b\" \"c\" \"d\" 4 rotate");
            Script.Make("test-rotate2", "\"a\" \"b\" \"c\" \"d\" -4 rotate");
            Script.Make("test-put", "\"a\" \"b\" \"c\" \"d\" \"e\" 3 put");

            return player;
        }
    }
}