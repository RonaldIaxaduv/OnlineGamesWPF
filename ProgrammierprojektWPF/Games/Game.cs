using System.Threading.Tasks;
using System;
using Communication;
using System.Collections.Generic;

namespace ProgrammierprojektWPF
{

    public abstract class Game
    {
        public Player[] players = new Player[2];
        public Board board;

        public enum GameResult { PlayerWon, Draw, Aborted }

        public enum GameLocation { Client, Server, Local } //Client: client running game of chomp, Server: server running game of chomp, Local: offline user running game of chomp
        public GameLocation gameLocation;
        public Client clientWrapper = null;
        public Server serverWrapper = null;

        public abstract Task turn(Player player); //a player's turn
        public abstract Task round(); //both players' turns

        protected async Task checkConnected()
        {
            while (true)
            {
                await Task.Delay(10000); //every 10s
                switch (gameLocation)
                {
                    case GameLocation.Client:
                        if (Connection.isConnected(clientWrapper?.sender))
                        {
                            await clientWrapper.sendObject(Commands.ClientCommands.RequestConnectionVerification, players);
                            if (await applyDeadline(clientWrapper.awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.ConnectionValid, Commands.ServerCommands.ServerError }), 25000) == true)
                            {
                                if (clientWrapper.serverMsgs.Contains(Commands.ServerCommands.ConnectionValid))
                                {
                                    clientWrapper.removeServerResponse(Commands.ServerCommands.ConnectionValid);
                                    if (clientWrapper.serverMsgs.Contains(Commands.ServerCommands.GameAborted))
                                    {
                                        //clientWrapper.removeServerResponse(Commands.ServerCommands.GameAborted); //don't remove this server response - it's needed for other awaitServerResponse tasks to end
                                        Console.WriteLine("Another player has aborted the game. Aborting game...");
                                        await abortGame();
                                        return;
                                    }
                                    else
                                    {
                                        //everything working as intended
                                        continue;
                                    }
                                }
                                else
                                {
                                    clientWrapper.removeServerResponse(Commands.ServerCommands.ServerError);
                                    Console.WriteLine("Connection could not be verified. Aborting game...");
                                    await abortGame();
                                    return;
                                }
                            }
                            else
                            {
                                Console.WriteLine("The server took too long to respond to the verification request. Aborting game...");
                                await abortGame();
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("The connection to the server has been severed. Aborting game...");
                            await abortGame();
                            return;
                        }

                    case GameLocation.Server:
                        if (Connection.isConnected(serverWrapper?.clientListener))
                        {
                            bool abort = false;
                            foreach (Player p in players)
                            {
                                if (!Connection.isConnected(p.connectionToClient.handler))
                                {
                                    Console.WriteLine("A user has been disconnected.");
                                    abort = true;
                                }
                                else if (p.connectionToClient.clientMsgs.Contains(Commands.ClientCommands.AbortGame))
                                {
                                    p.connectionToClient.removeClientResponse(Commands.ClientCommands.AbortGame);
                                    Console.WriteLine("A user has aborted the game.");
                                    abort = true;
                                }
                            }
                            if (abort)
                            {
                                Console.WriteLine("Aborting game...");
                                await abortGame();
                                return;
                            }
                            else
                            {
                                //everything working as intended
                                continue;
                            }
                        }
                        else
                        {
                            Console.WriteLine("The server's connection has been severed. Aborting game...");
                            await abortGame();
                            return;
                        }

                    default: //local: no connection checks necessary -> return immediately
                        return;
                }
            }
        }
        protected async Task<bool?> applyDeadline(Task task, int timeInMs)
        {
            //try
            //{ task.Start(); }
            //catch (Exception)
            //{ }
            await Task.WhenAny(task, Task.Delay(timeInMs));
            return task?.IsCompleted;
        }

        protected abstract void gameWon(GameResult result, Player player);
        public abstract Task abortGame();

        protected T[,] clone2DArray<T>(T[,] input)
        {
            T[,] output = new T[input.GetLength(0), input.GetLength(1)];

            for (int x = 0; x < input.GetLength(0); x++)
            {
                for (int y = 0; y < input.GetLength(1); y++)
                {
                    output[x, y] = input[x, y];
                }
            }

            return output;
        }
    }

}
