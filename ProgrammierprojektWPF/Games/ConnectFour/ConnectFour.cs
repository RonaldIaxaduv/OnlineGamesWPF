using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Communication;

namespace ProgrammierprojektWPF
{
    public class ConnectFour : Game, Loggable
    {
        public Stack<TurnData> TurnStack { get; set; }
        private int firstPlayer;
        private const int DIFF = 6; //difficulty (number of turns that the AI calculates in advance)
        public ConnectFourMenu myWindow = null;
        public int myChoice;
        private readonly Color Player1Colour = Color.ForestGreen;
        private readonly Color Player2Colour = Color.DarkRed;
        
        public ConnectFour(ConnectFourSpecifications specs)
        {
            Console.WriteLine("Starting new game of Connect Four... Specifications:\n{0}", specs.ToString());
            this.gameLocation = specs.GameLocation;

            this.clientWrapper = specs.clientWrapper;
            this.serverWrapper = specs.serverWrapper;

            //prepare board
            try
            { this.board = new ConnectFourBoard(specs.BoardSize); }
            catch (Exception e)
            { throw e; }

            //prepare players
            this.players = specs.Players;

            this.firstPlayer = specs.First;

            //prepare stack
            TurnStack = new Stack<TurnData>();

            //prepare window
            if (gameLocation != GameLocation.Server)
            { myWindow = new ConnectFourMenu(specs.BoardSize, this); myWindow.Show(); }

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

                if (TurnStack.Count > 0)
                {
                    var lastPt = TurnStack.Peek().coords;
                    if (checkWon(((ConnectFourBoard)board).squares, lastPt.X, lastPt.Y)) //check if game is won
                    {
                        gameWon(GameResult.PlayerWon, players[selection]); //will cancel the game afterwards
                        return;
                    }
                    if (checkFull(((ConnectFourBoard)board).squares)) //check if board is full
                    {
                        gameWon(GameResult.Draw, null);
                        return;
                    }
                }
                Console.WriteLine("Turn over.");
            }
            Console.WriteLine("Round over.");
            round(); //next round runs async, i.e. this method will finish before the next round is over, i.e. no overflow (due to recursive method calls) will occur
        }
        public override async Task turn(Player player)
        {
            sbyte playerNr = Equals(player, players[0]) ? (sbyte)0 : (sbyte)1;
            switch (player.type)
            {
                case Player.playerType.LocalComputer:
                    Console.WriteLine("Local computer's turn...");
                    myWindow?.Dispatcher.Invoke(new Action(() => myWindow.Active = false));
                    Console.WriteLine("Getting column...");
                    int choice = computerTurn(playerNr);
                    Point comPt = ((ConnectFourBoard)board).PlaceInColumn(choice, playerNr);
                    await Task.Delay(100); //needed to redraw window
                    Console.WriteLine("Done.");
                    addTurn(player, comPt);
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
                    var remoteCol = await awaitTurn(player);
                    if (remoteCol < 0)
                    { throw new Exception("An error occured while waiting for the remote player's/computer's turn."); }
                    Point remotePt = ((ConnectFourBoard)board).PlaceInColumn(remoteCol, playerNr);
                    await Task.Delay(100); //needed to redraw window
                    Console.WriteLine("Done.");
                    addTurn(player, remotePt);
                    return;

                case Player.playerType.LocalHuman:
                    Console.WriteLine("Local player's turn...");
                    myWindow?.Dispatcher.Invoke(new Action(() => myWindow.Active = true));
                    Console.WriteLine("Waiting for point...");
                    var localCol = await awaitTurn(player);
                    if (localCol < 0) //window has been closed
                    { throw new Exception("An error occured while waiting for the local player's/computer's turn."); }
                    Point localPt = ((ConnectFourBoard)board).PlaceInColumn(localCol, playerNr);
                    await Task.Delay(100); //needed to redraw window
                    Console.WriteLine("Done.");
                    addTurn(player, localPt);
                    if (gameLocation == GameLocation.Client && player.connectionToServer != null)
                    { await player.connectionToServer?.sendObject(Commands.ClientCommands.SubmitTurnData, getTurnDataPortable(TurnStack.Peek())); }
                    return;
            }
        }
        private async Task<int> awaitTurn(Player player)
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
                                return -1;
                            }
                            return opponentTurn.coords.X; //only column needed
                        }
                        else
                        {
                            clientWrapper?.removeServerResponse(Commands.ServerCommands.ServerError);
                            clientWrapper?.removeServerResponse(Commands.ServerCommands.GameAborted);
                            Console.WriteLine("An error has occurred while trying to receive the opponent's turn. Aborting game...");
                            await abortGame();
                            return -1;
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
                                return -1;
                            }
                            return opponentTurn.coords.X; //only column needed
                        }
                        else
                        {
                            player.connectionToClient?.removeClientResponse(Commands.ClientCommands.ClientError);
                            player.connectionToClient?.removeClientResponse(Commands.ClientCommands.AbortGame);
                            Console.WriteLine("An error has occurred while trying to receive the opponent's turn. Aborting game...");
                            await abortGame();
                            return -1;
                        }
                    }

                case Player.playerType.LocalHuman:
                    myChoice = -1; //will be set in myWindow
                    while ((myChoice < 0) && myWindow != null)
                    {
                        await Task.Delay(500);
                    }
                    if (myChoice < 0) //window has been closed
                    {
                        Console.WriteLine("User closed the window.");
                        await abortGame();
                        return -1;
                    }
                    await Task.Delay(200); //needed to redraw window
                    return myChoice;

                default: //LocalComputer and LocalHuman don't require server connection
                    return -1;
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

        private int computerTurn(sbyte playerNr)
        {
            var thisBoard = (ConnectFourBoard)board;
            int destination = getWinning(thisBoard.squares, playerNr); //look for several simple win conditions

            if (destination < 0)
            {
                destination = getWinPreventing(thisBoard.squares, playerNr);
                if (destination < 0)
                {
                    for (int degree = DIFF; degree > 0; degree--)
                    {
                        try
                        { destination = getBiasedRandomRecursive(thisBoard.squares, playerNr, degree); }
                        catch (Exception e)
                        { Console.WriteLine(degree + " failed: " + e.Message); continue; }

                        if (destination >= 0) return destination;
                    }
                    Console.WriteLine("getFullRandom"); return getFullRandom(thisBoard.squares);
                }
                else Console.WriteLine("getWinPreventing");

            }
            else Console.WriteLine("getWinning");

            return destination;
        }
        private int getWinning(sbyte[,] fields, sbyte playerNr)
        {
            //this function handles simple win conditions. if a constellation isn't handled, -1 is returned.

            //immediate win
            var winningCol = winningColumns(fields, playerNr); //placing the piece in one of these columns will result in a win
            if (winningCol.Count > 0)
            { return winningCol[(new Random()).Next(winningCol.Count)]; } //-> win

            return -1;

            //try to create two (or more) winning columns at once so that the opponent cannot win no matter where they place their piece
            winningCol = new List<int>();
            for (int column = 0; column < fields.GetLength(0); column++)
            {
                if (fields[column, 0] < 0) //column not full yet
                {
                    var newFields = clone2DArray<sbyte>(fields);
                    for (int row = newFields.GetLength(1) - 1; row >= 0; row--)
                    {
                        if (newFields[column, row] < 0)
                        {
                            newFields[column, row] = playerNr;
                            break;
                        }
                    }
                    if (winningColumns(newFields, playerNr).Count >= 2)
                    { winningCol.Add(column); }
                }
            }
            if (winningCol.Count > 0)
            { return winningCol[(new Random()).Next(winningCol.Count)]; } //-> win

            return -1;
        }
        private int getWinPreventing(sbyte[,] fields, sbyte playerNr)
        {
            //keep opponent from winning (if possible)
            var otherPlayerNr = (playerNr == 0 ? (sbyte)1 : (sbyte)0);
            var winningCol = winningColumns(fields, otherPlayerNr);
            if (winningCol.Count > 0)
            { return winningCol[(new Random()).Next(winningCol.Count)]; } //if winningCol.Count = 1 : opponent can't win

            //prevent opponent from building 2x2-blocks (see explanation in checkBumpOnEvenSurface method)
            var bump = checkBumpOnEvenSurface(fields, otherPlayerNr);
            if (bump >= 0)
            { return bump; }

            return -1;
        }
        private int getBiasedRandomRecursive(sbyte[,] fields, sbyte playerNr, int degree)
        {
            //this function select a random viable column in fields that doesn't result in a non-negative output of getWinning (i.e. the other player is less likely to create a win condition with their next turn). if there are none, -1 is returned.
            //degree: number of possible turns that the function calculates in advance; >0

            //WIP: this method still outputs columns that allow the opponent to win during their next turn!

            if (degree < 1)
            { throw new Exception("Invalid value of degree."); }

            var viable = new List<int>();
            sbyte otherPlayerNr = (playerNr == 0 ? (sbyte)1 : (sbyte)0);

            //get viable
            for (int column = 0; column < fields.GetLength(0); column++)
            {
                if (fields[column, 0] < 0) //column must not be full
                {
                    var newFields = clone2DArray<sbyte>(fields);
                    for (int row = newFields.GetLength(1) - 1; row >= 0; row--) //get fields after adding the new piece
                    {
                        if (newFields[column, row] < 0)
                        { newFields[column, row] = playerNr; break; }
                    }

                    if (degree > 1)
                    {
                        //if (getWinning(newFields, playerNr) == -1) //take a further look at the usual viable points
                        //{
                        //if (getBiasedRandomRecursive(newFields, otherPlayerNr, degree - 1) == -1) //next player would not have a good answer (i.e. the game is basically won) -> remember
                        //{ viable.Add(column); }  //Console.WriteLine("Taken (degree " + degree + ")."); }
                        //}
                        if (getWinning(newFields, otherPlayerNr) == -1) //opponent can't win immediately during their turn -> take a further look at other viable columns
                        {
                            if (winningColumns(newFields, playerNr).Count >= 2) //player has more than one option to win with this column -> opponent cannot win -> prioritise
                            { viable.Clear(); viable.Add(column); break; }
                            else if (getBiasedRandomRecursive(newFields, otherPlayerNr, degree - 1) == -1) //opponent would not have a good answer (i.e. the game is basically won) -> remember
                            { viable.Add(column); }
                        }
                    }
                    else //degree == 1 -> like the original biasedRandom
                    {
                        //if (getWinning(newFields, playerNr) == -1)
                        //{ viable.Add(column); }
                        if (getWinning(newFields, otherPlayerNr) == -1)
                        {
                            if (winningColumns(newFields, playerNr).Count >= 2) //player has more than one option to win with this column -> opponent cannot win -> prioritise
                            { viable.Clear(); viable.Add(column); break; }
                            else
                            { viable.Add(column); }
                        }
                    }
                }
            }

            if (viable.Count < 1) return -1;
            else return viable[(new Random()).Next(viable.Count)];
        }
        private int getFullRandom(sbyte[,] fields)
        {
            //this function returns a random viable column in fields

            var thisBoard = (ConnectFourBoard)board;
            var viable = new List<int>();

            for (int col = 0; col < fields.GetLength(0); col++)
            {
                for (int row = fields.GetLength(1) - 1; row >= 0; row--)
                {
                    if (fields[col, row] < 0)
                    { viable.Add(col); break; }
                }
            }

            //select and return random viable point
            return viable[(new Random()).Next(viable.Count)];
        }

        private List<int> winningColumns(sbyte[,] fields, sbyte playerNr)
        {
            var output = new List<int>();

            for (int column = 0; column < fields.GetLength(0); column++)
            {
                //determine row that the piece would be placed in
                int row = -1;
                for (int i = fields.GetLength(1) - 1; i >= 0; i--)
                {
                    if (fields[column, i] < 0)
                    {
                        row = i;
                        break;
                    }
                }

                if (row >= 0)
                {
                    var newFields = clone2DArray<sbyte>(fields);
                    newFields[column, row] = playerNr;
                    if (checkWon(newFields, column, row))
                    { output.Add(column); }
                }
            }

            return output;
        }
        private bool checkWon(sbyte[,] fields, int column, int row)
        {
            if (fields != null && fields[column, row] >= 0) //space must not be empty
            {
                //check for horizontal combo
                int count = 0;
                for (int x = Math.Max(0, column - 3), max = Math.Min(fields.GetLength(0), column + 4); count < 4 && x < max; x++)
                {
                    if (fields[x, row] == fields[column, row])
                    { count++; }
                    else
                    { count = 0; }
                }
                if (count >= 4)
                { /*Console.WriteLine("Won (horizontal): {0} | {1}", column, row);*/ return true; }

                //check for vertical combo
                count = 0;
                for (int y = Math.Max(0, row - 3), max = Math.Min(fields.GetLength(1), row + 4); count < 4 && y < max; y++)
                {
                    if (fields[column, y] == fields[column, row])
                    { count++; }
                    else
                    { count = 0; }
                }
                if (count >= 4)
                { /*Console.WriteLine("Won (vertical): {0} | {1}", column, row);*/ return true; }

                //check for diagonal combo (top left - bottom right)
                count = 0;
                for (int offset = -3; count < 4 && offset <= 3; offset++)
                {
                    if (column + offset >= 0 && column + offset < fields.GetLength(0) && row + offset >= 0 && row + offset < fields.GetLength(1))
                    {
                        if (fields[column + offset, row + offset] == fields[column, row])
                        { count++; }
                        else
                        { count = 0; }
                    }
                }
                if (count >= 4)
                { /*Console.WriteLine("Won (diagonal 1): {0} | {1}", column, row);*/ return true; }

                //check for diagonal combo (bottom left - top right)
                count = 0;
                for (int offset = -3; count < 4 && offset <= 3; offset++)
                {
                    if (column + offset >= 0 && column + offset < fields.GetLength(0) && row - offset >= 0 && row - offset < fields.GetLength(1))
                    {
                        if (fields[column + offset, row - offset] == fields[column, row])
                        { count++; }
                        else
                        { count = 0; }
                    }
                }
                if (count >= 4)
                { /*Console.WriteLine("Won (diagonal 2): {0} | {1}", column, row);*/ return true; }
            }

            return false;
        }
        private bool checkFull(sbyte[,] fields)
        {
            for (int column = 0; column < fields.GetLength(0); column++)
            {
                if (fields[column, 0] < 0)
                { return false; }
            }
            return true;
        }
        private int checkBumpOnEvenSurface(sbyte[,] fields, sbyte playerNr)
        {
            //returns leftmost column of a matching surface (area where all pieces reach up to the same row) where there are 2 pieces of a certain player next to each other forming a bump
            //with this situation, the player could build a 2x2 block. afterwards, they would have automatically won:
            //- place pieces on top of the block (-> have to be countered, otherwise won)
            //- place pieces above another on one side of the block (-> have to be countered, otherwise won)
            //-> situation: 3 pieces next to each other with a free slot on each end -> game won
            //minimum size of an area that can be used like this: 5x4 units (with the bump being in the centre)

            //int minWidth = 3;
            //int minHeight = 4;

            var columnHeights = new int[fields.GetLength(0)];            
            for (int column = 0; column < fields.GetLength(0); column++)
            {
                columnHeights[column] = getColumnHeight(fields, column);
            }

            for (int i = 0; i <= fields.GetLength(0) - 3; i++)
            {
                if (columnHeights[i] >= 4)
                {
                    if ((columnHeights[i + 1] == columnHeights[i] + 1 && columnHeights[i + 2] == columnHeights[i] + 2) || (columnHeights[i] == columnHeights[i + 1] + 2 && columnHeights[i + 2] == columnHeights[i] + 1)) //start of 1-2 unit-high bump
                    {
                        //bump found at i+1 to i+2
                        Console.WriteLine("BLING BLING BLING! 1");
                        if (columnHeights[i + 1] < columnHeights[i + 2])
                        { return i + 1; }
                        else if (columnHeights[i + 1] > columnHeights[i + 2])
                        { return i + 2; }
                        else
                        { return i + 1 + ((new Random()).Next() % 2); } //random i+2 or i+3
                    }
                    else if ((columnHeights[i] == columnHeights[i + 2] + 1 && columnHeights[i + 1] == columnHeights[i + 2] + 2) || (columnHeights[i] == columnHeights[i + 2] + 2 && columnHeights[i + 1] == columnHeights[i + 2] + 1)) //start of 1-2 unit-high bump
                    {
                        //bump found at i to i+1
                        Console.WriteLine("BLING BLING BLING! 2");
                        if (columnHeights[i] < columnHeights[i + 1])
                        { return i; }
                        else if (columnHeights[i] > columnHeights[i + 1])
                        { return i + 1; }
                        else
                        { return i + ((new Random()).Next() % 2); } //random i or i+1
                    }
                }
            }

            return -1;
        }
        private int getColumnHeight(sbyte[,] fields, int column)
        {
            for (int row = fields.GetLength(1) - 1; row >= 0; row--)
            {
                if (fields[column, row] < 0)
                { return fields.GetLength(1) - row - 1; }
            }
            return fields.GetLength(1);
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

        public void addTurn(Player player, Point point)
        {
            TurnStack.Push(new TurnData(player, point));
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
