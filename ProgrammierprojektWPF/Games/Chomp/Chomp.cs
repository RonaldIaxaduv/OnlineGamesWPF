using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using Communication;

namespace ProgrammierprojektWPF
{

    public class Chomp : Game, Loggable
    {
        public Stack<TurnData> TurnStack { get; set; }
        private int firstPlayer;
        private const int DIFF = 3; //difficulty (number of turns that the AI calculates in advance)
        public ChompMenu myWindow = null;
        public Point myChoice;

        public Chomp(ChompSpecifications specs)
        {
            Console.WriteLine("Starting new game of Chomp... Specifications:\n{0}", specs.ToString());
            this.gameLocation = specs.GameLocation;

            this.clientWrapper = specs.clientWrapper;
            this.serverWrapper = specs.serverWrapper;

            //prepare board
            try
            { this.board = new ChompBoard(specs.BoardSize); }
            catch (Exception e)
            { throw e; }

            //prepare players
            this.players = specs.Players;

            this.firstPlayer = specs.First;

            //prepare stack
            TurnStack = new Stack<TurnData>();

            //prepare window
            if (gameLocation != GameLocation.Server)
            { myWindow = new ChompMenu(specs.BoardSize, this); myWindow.Show(); }

            //start game and start checking the connection regularly
            checkConnected();
            round();
        }

        public override async Task round()
        {
            Console.WriteLine("New round.");
            for (int i = 0, selection = firstPlayer; i < players.Length; i++, selection = (selection + 1) % 2) //selection switches between 0 and 1 (players.Length-1 times, i.e. once)
            {
                Console.WriteLine("{0}'s turn.", players[selection].Name);
                try
                { await turn(players[selection]); }
                catch (Exception e)
                {
                    Console.WriteLine("The game has been aborted. Reason:\n{0}\n{1}\n{2}", e.Message, e.Source, e.StackTrace);
                    await abortGame(); //will cancel the game afterwards
                    return;
                }                
                if (!((ChompBoard)board).squares[0, 0])
                {
                    gameWon(GameResult.PlayerWon, players[selection]); //will cancel the game afterwards
                    return;
                }
                Console.WriteLine("Turn over.");
            }
            Console.WriteLine("Round over.");
            round(); //next round runs async, i.e. this method will finish before the next round is over, i.e. no overflow (due to recursive method calls) will occur
        }
        public override async Task turn(Player player)
        {
            switch (player.type)
            {
                case Player.playerType.LocalComputer:
                    Console.WriteLine("Local computer's turn...");
                    myWindow?.Dispatcher.Invoke(new Action(() => myWindow.Active = false));
                    Console.WriteLine("Getting point...");
                    Point choice = computerTurn();
                    ((ChompBoard)board).snap(choice);
                    await Task.Delay(100); //needed to redraw window
                    Console.WriteLine("Done.");
                    addTurn(player, choice);
                    if (gameLocation == GameLocation.Server)
                    { await player.connectionToClient?.sendObject(Commands.ServerCommands.ReceiveTurnData, getTurnDataPortable(TurnStack.Peek())); }
                    else if (gameLocation == GameLocation.Client)
                    { await player.connectionToServer?.sendObject(Commands.ClientCommands.SubmitTurnData, getTurnDataPortable(TurnStack.Peek())); }
                    return;

                case Player.playerType.RemoteComputer:
                case Player.playerType.RemoteHuman:
                    Console.WriteLine("Remote player's/computer's turn...");
                    myWindow?.Dispatcher.Invoke(new Action(() => myWindow.Active = false));
                    Console.WriteLine("Waiting for point...");
                    var remotePt = await awaitTurn(player);
                    if (remotePt == new Point(-1, -1))
                    { throw new Exception("An error occured while waiting for the remote player's/computer's turn."); }
                    ((ChompBoard)board).snap(remotePt);
                    await Task.Delay(100); //needed to redraw window
                    Console.WriteLine("Done.");
                    addTurn(player, remotePt);
                    return;

                case Player.playerType.LocalHuman:
                    Console.WriteLine("Local player's turn...");
                    myWindow?.Dispatcher.Invoke(new Action(() => myWindow.Active = true));
                    Console.WriteLine("Waiting for point...");
                    var localPt = await awaitTurn(player);
                    if (localPt == new Point(-1, -1)) //window has been closed
                    { throw new Exception("An error occured while waiting for the local player's/computer's turn."); }
                    ((ChompBoard)board).snap(localPt);
                    await Task.Delay(100); //needed to redraw window
                    Console.WriteLine("Done.");
                    addTurn(player, localPt);
                    if (gameLocation == GameLocation.Client && player.connectionToServer != null)
                    { await player.connectionToServer?.sendObject(Commands.ClientCommands.SubmitTurnData, getTurnDataPortable(TurnStack.Peek())); }
                    return;
            }
        }
        private async Task<Point> awaitTurn(Player player)
        {
            switch (player.type)
            {
                case Player.playerType.RemoteComputer:
                case Player.playerType.RemoteHuman:
                    if (gameLocation == GameLocation.Client)
                    {
                        await clientWrapper?.awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.ReceiveTurnData, Commands.ServerCommands.ServerError, Commands.ServerCommands.GameAborted });
                        if (clientWrapper?.serverMsgs.Contains(Commands.ServerCommands.ReceiveTurnData) == true)
                        {
                            clientWrapper?.removeServerResponse(Commands.ServerCommands.ReceiveTurnData);
                            var opponentTurn = getTurnData((TurnDataPortable)clientWrapper?.receivedData);
                            if (!Equals(opponentTurn.player, player))
                            {
                                Console.WriteLine("The wrong player's turn has been received. Aborting game...");
                                await abortGame();
                                return new Point(-1, -1);
                            }
                            return opponentTurn.coords;
                        }
                        else
                        {
                            clientWrapper?.removeServerResponse(Commands.ServerCommands.ServerError);
                            clientWrapper?.removeServerResponse(Commands.ServerCommands.GameAborted);
                            Console.WriteLine("An error has occurred while trying to receive the opponent's turn. Aborting game...");
                            await abortGame();
                            return new Point(-1, -1);
                        }
                    }
                    else //gameLocation == GameLocation.Server (GameLocation.Solo doesn't support remote users) -> host (server)
                    {
                        player.connectionToClient?.awaitClientResponse(new List<Commands.ClientCommands>() { Commands.ClientCommands.SubmitTurnData, Commands.ClientCommands.ClientError, Commands.ClientCommands.AbortGame });
                        if (player.connectionToClient?.clientMsgs.Contains(Commands.ClientCommands.SubmitTurnData) == true)
                        {
                            player.connectionToClient?.removeClientResponse(Commands.ClientCommands.SubmitTurnData);
                            var opponentTurn = getTurnData((TurnDataPortable)player.connectionToClient?.receivedData);
                            if (!Equals(opponentTurn.player, player))
                            {
                                Console.WriteLine("The wrong player's turn has been received. Aborting game...");
                                await abortGame();
                                return new Point(-1, -1);
                            }
                            return opponentTurn.coords;
                        }
                        else
                        {
                            player.connectionToClient?.removeClientResponse(Commands.ClientCommands.ClientError);
                            player.connectionToClient?.removeClientResponse(Commands.ClientCommands.AbortGame);
                            Console.WriteLine("An error has occurred while trying to receive the opponent's turn. Aborting game...");
                            await abortGame();
                            return new Point(-1, -1);
                        }
                    }

                case Player.playerType.LocalHuman:
                    myChoice = new Point(-1, -1); //will be set in myWindow
                    while ((myChoice.X < 0 || myChoice.Y < 0) && myWindow != null)
                    {
                        await Task.Delay(500);
                    }
                    if (myChoice.X < 0 || myChoice.Y < 0) //window has been closed
                    {
                        Console.WriteLine("User closed the window.");
                        await abortGame();
                        return new Point(-1, -1);
                    }
                    await Task.Delay(200); //needed to redraw window
                    return myChoice;

                default: //LocalComputer and LocalHuman don't require server connection
                    return new Point(-1, -1);
            }
        }

        private TurnDataPortable getTurnDataPortable(TurnData data)
        {
            if (players[0].Name == data.player.Name)
            { return new TurnDataPortable(players[1].Name, data.player.Name, data.coords); }
            else
            { return new TurnDataPortable(players[0].Name, data.player.Name, data.coords); }
        }
        private TurnData getTurnData(TurnDataPortable data)
        {
            if (players[0].Name == data.playerName)
            { return new TurnData(players[0], data.coords); }
            else
            { return new TurnData(players[1], data.coords); }
        }

        private Point computerTurn()
        {
            ChompBoard thisBoard = (ChompBoard)board;
            Point destination = getWinning(thisBoard.squares); //look for several simple win conditions

            if (destination == new Point(-1, -1))
            {
                for (int degree = DIFF; degree > 0; degree--)
                {
                    try
                    { destination = getBiasedRandomRecursive(thisBoard.squares, degree); }
                    catch (Exception e)
                    { Console.WriteLine(degree + " failed: " + e.Message); continue; }

                    if (destination != new Point(-1, -1)) return destination;
                }
                Console.WriteLine("getFullRandom"); return getFullRandom(thisBoard.squares);

            }
            else Console.WriteLine("getWinningPoint");

            return destination;
        }
        private Point getWinning(bool[,] fields)
        {
            //this function handles several simple win conditions. if a constellation isn't handled, (-1,-1) is returned.

            if (!(fields[0, 1] || fields[1, 0]))
            { return new Point(0, 0); } //-> win

            Rectangle rect = containedRectangle(fields);

            if (rect.Size != new Size(0, 0)) //remaining area contains a rectangle (and nothing else)
            {
                //1 unit-wide row:
                if (rect.Size.Width == 1) //only vertical strip on the left remaining
                {
                    if (rect.Size.Height > 2) return new Point(0, 2); //-> win
                                                                      //else return new Point(0, 1); //-> loss (only one choice)
                    else return new Point(-1, -1);
                }
                else if (rect.Size.Height == 1) //only horizontal strip at the top remaining
                {
                    if (rect.Size.Width > 2) return new Point(2, 0); //-> win
                                                                     //else return new Point(1, 0); //-> loss (only one choice)
                    else return new Point(-1, -1);
                }

                //squares:
                if (rect.Size.Width == rect.Size.Height)
                {
                    if (rect.Size.Width == 2) return new Point(0, 1); //-> win
                    else return new Point(1, 1); //-> win
                }

                //other rectangles:
                if (rect.Size.Width == 2) return new Point(0, 1); //-> win
                else if (rect.Size.Height == 2) return new Point(1, 0); //-> win

                //rest: too complex
            }
            else //no (single) rectangle contained
            {
                //get sizes of the outer rows
                int topSize = getTopRowSize(fields);
                int leftSize = getLeftRowSize(fields);

                //two 1 unit-wide rows:
                if (fields[0, 1] && fields[1, 0] && !fields[1, 1])
                {
                    if ((topSize + leftSize - 1) % 2 == 1) //uneven number of remaining fields
                    {
                        if (topSize == leftSize) //rows are of the same size
                        {
                            if (topSize < 3) return new Point(1, 0); //-> win
                                                                     //else return new Point(-1, -1); //-> loss (several choices)
                        }
                    }
                    else //uneven number of remaining fields and rows are of different sizes or even number remaining fields
                    {
                        if (topSize == 2) return new Point(0, 1); //-> win
                        else if (leftSize == 2) return new Point(1, 0); //-> win

                        if (topSize > leftSize) return new Point(leftSize, 0); //-> win
                        else if (leftSize > topSize) return new Point(0, topSize); //-> win
                    }

                    //rest: too complex
                }
                else //not two 1 unit-wide rows (nor single rectangle) -> more complex area
                {
                    if (topSize == 2) return new Point(0, 1); //-> win
                    else if (leftSize == 2) return new Point(1, 0); //-> win
                    else if (topSize == leftSize) return new Point(1, 1); //-> win

                    //rest: too complex
                }
            }

            return new Point(-1, -1);
        }
        private Point getBiasedRandomRecursive(bool[,] fields, int degree)
        {
            //this function select a random viable point in fields that doesn't result in a non-negative output of aaronsChoice (i.e. the other player is less likely to make a winning snap with their next turn). if there are none, (-1,-1) is returned.
            //degree: number of possible turns that the function calculates in advance; >0

            if (degree < 1)
            { throw new Exception("Invalid value of degree."); }

            List<Point> viable = new List<Point>();

            //get viable
            if (!(fields[0, 1] || fields[1, 0]))
            {
                return new Point(0, 0);
            }

            for (int x = 0; x < fields.GetLength(0); x++)
            {
                for (int y = 0; y < fields.GetLength(1); y++)
                {
                    if (fields[x, y])
                    {
                        if (x == 0 && y == 0)
                        { continue; }

                        bool[,] snapped = clone2DArray<Boolean>(fields);
                        for (int snapX = x; snapX < fields.GetLength(0); snapX++) //get fields after snapping at [x,y]
                        {
                            for (int snapY = y; snapY < fields.GetLength(1); snapY++)
                            {
                                if (snapped[snapX, snapY])
                                { snapped[snapX, snapY] = false; } //set all squares to be active
                                else
                                { continue; } //all squares behind one that has already been broken off are false -> skip to next line
                            }
                        }

                        if (degree > 1)
                        {
                            if (getWinning(snapped) == new Point(-1, -1)) //take a further look at the usual viable points
                            {
                                if (getBiasedRandomRecursive(snapped, degree - 1) == new Point(-1, -1)) //next player would not have a good answer (i.e. the game is basically won) -> remember
                                { viable.Add(new Point(x, y)); }  //Console.WriteLine("Taken (degree " + degree + ")."); }
                            }
                        }
                        else //degree == 1 -> like the original biasedRandom
                        {
                            if (getWinning(snapped) == new Point(-1, -1))
                            { viable.Add(new Point(x, y)); }
                        }
                    }
                }
            }

            if (viable.Count < 1) return new Point(-1, -1);
            else return viable[(new Random()).Next(viable.Count)];
        }
        private Point getFullRandom(bool[,] fields)
        {
            //this function returns a random viable point in fields

            ChompBoard thisBoard = (ChompBoard)board;
            List<Point> viable = new List<Point>();

            //get viable 
            if (!(fields[0, 1] || fields[1, 0]))
            { return new Point(0, 0); }

            for (int x = 0; x < fields.GetLength(0); x++)
            {
                for (int y = 0; y < fields.GetLength(1); y++)
                {
                    if (fields[x, y] && !(x == 0 && y == 0))
                    { viable.Add(new Point(x, y)); }
                }
            }

            //select and return random viable point
            return viable[(new Random()).Next(viable.Count)];
        }

        private Rectangle containedRectangle(bool[,] fields)
        {
            //if fields contains a (single) rectangle, it is returned by this function. otherwise an empty rectangle is returned.

            int width = getTopRowSize(fields); int height = getLeftRowSize(fields);

            //if there is more than one rectangle, a corner has been snapped out of this rectangle -> its bottom right corner isn't active anymore
            if (fields[width - 1, height - 1]) return new Rectangle(0, 0, width, height);
            else return new Rectangle(0, 0, 0, 0);
        }
        private int getTopRowSize(bool[,] fields)
        {
            //returns the number of active fields in the top row of fields

            int output = 0;
            for (int x = 0; x < fields.GetLength(0); x++)
            {
                if (fields[x, 0]) output++;
                else break;
            }
            return output;
        }
        private int getLeftRowSize(bool[,] fields)
        {
            //returns the number of active fields in the left row of fields

            int output = 0;
            for (int y = 0; y < fields.GetLength(1); y++)
            {
                if (fields[0, y]) output++;
                else break;
            }
            return output;
        }

        protected override void gameWon(GameResult result, Player player)
        {
            Console.WriteLine("Result of the game: {0}", result);
            switch (result)
            {
                case GameResult.Aborted:
                    Console.WriteLine("The game has been aborted.");
                    myWindow?.Dispatcher.Invoke(new Action(() =>
                    {
                        myWindow.gameOver(result, false);
                    }));
                    break;
                case GameResult.Draw:
                    Console.WriteLine("The game is a draw.");
                    myWindow?.Dispatcher.Invoke(new Action(() =>
                    {
                        myWindow.gameOver(result, false);
                    }));
                    break;
                case GameResult.PlayerWon:
                    if (player != null)
                    {
                        Console.WriteLine(player.Name + " has won the game.");
                        myWindow?.Dispatcher.Invoke(new Action(() =>
                        {
                            myWindow.gameOver(result, player.type == Player.playerType.LocalHuman);
                        }));
                    }
                    else
                    {
                        Console.WriteLine("An unknown player has won the game (bug).");
                    }
                    break;
                default:
                    break;
            }
            if (gameLocation != GameLocation.Server && clientWrapper != null)
            {
                clientWrapper.IngameInf = Client.IngameStatus.NotIngame;
                clientWrapper?.myWindow.Dispatcher.Invoke(new Action(() => ((ClientMenu)clientWrapper.myWindow).toggleGameButtons(true)));
            }

            //if (player == null)
            //{
            //    Console.WriteLine("The game is a draw.");
            //    myWindow?.Dispatcher.Invoke(new Action(() =>
            //    {
            //        myWindow.gameOver(null);
            //    }));
            //}
            //Console.WriteLine(player.Name + " has won the game.");
            //if (gameLocation == GameLocation.Server)
            //{
            //    //WIP: write in lbSystemMessages
            //}
            //myWindow?.Dispatcher.Invoke(new Action(() =>
            //{
            //    myWindow.gameOver(player.type == Player.playerType.LocalHuman);
            //}));
        }
        public override async Task abortGame()
        {
            switch (gameLocation)
            {
                case GameLocation.Client:
                    await clientWrapper?.sendObject(Commands.ClientCommands.AbortGame, players);
                    myWindow?.Owner?.Dispatcher.Invoke(new Action(() => ((ClientMenu)myWindow.Owner).toggleGameButtons(true)));
                    break;

                case GameLocation.Server:
                    foreach (Player p in players)
                    {
                        await serverWrapper?.getConnectionFromUsername(p.Name)?.sendCommand(Commands.ServerCommands.GameAborted);
                    }
                    break;

                default:
                    try
                    { myWindow?.Owner?.Dispatcher.Invoke(new Action(() => ((ClientMenu)myWindow.Owner).toggleGameButtons(true))); }
                    catch (Exception)
                    { }                    
                    break;
            }
            gameWon(GameResult.Aborted, null); //will close the window
        }

        public void addTurn(Player player, Point coords)
        {
            TurnStack.Push(new TurnData(player, coords));
        }
        public void removeTurn()
        {
            try
            { TurnStack.Pop(); }
            catch (Exception e)
            { Console.WriteLine("Error: " + e.Message); }
        }
    }

}
