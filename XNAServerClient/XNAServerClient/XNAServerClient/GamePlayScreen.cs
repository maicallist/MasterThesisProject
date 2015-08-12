using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Net;


namespace XNAServerClient
{
    public class GamePlayScreen : GameScreen
    {
        #region Variables

        //game objects
        Platform platform_local;
        Platform platform_remote;
        Ball ball;

        //text member
        SpriteFont font;

        //game state
        bool gameStart;
        bool gameEnd;

        /* 4.0 */
        Color color;
        Color clearColor;
        GameState gameState;

        //Networking members
        NetworkSession session;
        AvailableNetworkSessionCollection availableSessions;
        int sessionIndex;
        AvailableNetworkSession availableSession;
        PacketWriter packetWriter;
        PacketReader packetReader;
        //host flag
        bool isServer;

        //XNA Lobby, Matching
        enum GameState { Menu, FindGame, PlayGame }
        enum SessionProperty { GameMode, SkillLevel, ScoreToWin }
        enum GameMode { Practice, Timed, CaptureTheFlag }
        enum SkillLevel { Beginner, Intermediate, Advanced }
        enum PacketType { Enter, Leave, Data }

        //store remote game state
        //process and render on local screen
        Char hostTag;
        Vector2 ballPos;
        Vector2 ballVel;
        Vector2 remotePlatformPos;
        Vector2 remotePlatformVel;
        //local state
        bool localPlatformMoving;
        //decide when to send packet
        bool sendPacket;
        //lag compensation algorithm
        enum LagCompensation { None, DeadReckoning, PlayPattern }
        LagCompensation lagCompen;
        bool lagFlag;

        //record dead reckoning 
        bool DRTestMode;
        bool deadMoving;
        //store platform moving on host
        ArrayList hostSide;
        ArrayList hostVel;
        //store host platform moving on client side
        ArrayList clientSide;
        ArrayList clientVel;

        //record time
        //global clock
        static DateTime startTimeInt;
        //local clock
        static TimeSpan startTime;
        static TimeSpan current;
        ArrayList timeTag;
        ArrayList timeTagInMillisec;

        //communication latency between
        //client and host
        //local start time
        static long initLagInMillisec;

        /*******************************************/
        /*all variables defined beliw are temproral*/
        /*******************************************/

        #endregion

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);
            
            //loading assets
            if (font == null)
                font = content.Load<SpriteFont>("Font1");

            platform_local = new Platform();
            platform_local.LoadContent(Content, inputManager);
            platform_remote = new Platform();
            platform_remote.LoadContent(Content, inputManager);
            platform_remote.ControlByPlayer = false;
            platform_remote.Position = new Vector2(ScreenManager.Instance.Dimensions.X / 2 - platform_remote.Dimension.X / 2, 20);

            ball = new Ball();
            ball.LoadContent(Content, inputManager);

            gameStart = false;
            gameEnd = false;
            localPlatformMoving = false;

            /* 4.0 */
            color = Color.White;
            clearColor = Color.CornflowerBlue;
            gameState = GameState.Menu;
            sessionIndex = 0;
            packetReader = new PacketReader();
            packetWriter = new PacketWriter();

            /* network variable */
            hostTag = '/';
            ballPos = new Vector2(0, 0);
            ballVel = new Vector2(0, 0);
            remotePlatformPos = new Vector2(0, 0);
            remotePlatformVel = new Vector2(0, 0);

            sendPacket = true;

            lagCompen = LagCompensation.PlayPattern;
            lagFlag = false;

            //record dead reckoning
            //make below true if you want to record DR data
            //remember change latency generator
            //apply it to client side
            // !isServer in if condition
            DRTestMode = false;

            deadMoving = false;
            clientSide = new ArrayList();
            hostSide = new ArrayList();
            hostVel = new ArrayList();
            clientVel = new ArrayList();

            timeTag = new ArrayList();
            timeTagInMillisec = new ArrayList();

            initLagInMillisec = 0;
        }

        public override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here

            //Release the session
            if (session != null)
                session.Dispose();

            base.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {
            //get current time for recording
            if(gameStart)
                current = gameTime.TotalGameTime - startTime;
            /*
             * only sned data when necessary
             * collade with platform
             * move platform
             */
            sendPacket = false;

            inputManager.Update();
            UpdateInput(gameTime);

            //check game start state
            if (!gameStart && session != null && session.SessionState == NetworkSessionState.Lobby)
            {
                if (inputManager.KeyPressed(Keys.Y) && isServer)
                {
                    session.StartGame();
                    //notify all clients
                    foreach (LocalNetworkGamer gamer in session.LocalGamers)
                    {
                        //tell all clients to start game
                        if (isServer)
                            packetWriter.Write('g');
                        // Send it to all remote gamers.
                        gamer.SendData(packetWriter, SendDataOptions.InOrder);
                    }

                    ball.Velocity = new Vector2(-7, -10);
                    gameStart = true;
                    //record local game start time
                    startTime = gameTime.TotalGameTime;
                    //record internet game start time
                    startTimeInt = NTP.GetNetworkTime();
                    sendPacket = true;
                    
                }
            }

            //check game end state
            //here might be a little tricky
            //because we are implementing a P2P game(not entirely)
            //therefore we only check ball position at local platform side
            //and let remote player update ball position at remote platform side
            if (gameStart && !gameEnd)
            {
                //800 - 755 (top side of platform) to 800 - 780
                if (ball.Position.Y + ball.ImageHeight >= 
                    ScreenManager.Instance.Dimensions.Y - 20)
                {
                    //Console.WriteLine("Local: " + session.LocalGamers.Count);
                    //Console.WriteLine("That local is host? " + session.LocalGamers[0].IsHost);
                    //Console.WriteLine("All: " + session.AllGamers.Count);
                    //Console.WriteLine("Remote: " + session.RemoteGamers);
                    gameEnd = true;
                    foreach (LocalNetworkGamer gamer in session.LocalGamers)
                    {
                        //tell all remote gamer to end game
                        packetWriter.Write('e');
                        // Send it to all remote gamers.
                        gamer.SendData(packetWriter, SendDataOptions.InOrder);
                    }
                }
            }

            //check restart input
            if (gameStart && gameEnd)
            { 
            
            }

            //update local platform velocity for transmission
            if (inputManager.KeyUp(Keys.Left) && inputManager.KeyUp(Keys.Right))
            {
                if (localPlatformMoving)
                {
                    //update some states when stop move
                    localPlatformMoving = false;
                    platform_local.Velocity = new Vector2(0, 0);
                    sendPacket = true;
                }
            }
            else
            {
                //platform wasn't moving in last update()
                //now it starts moving
                if (!localPlatformMoving)
                {
                    //update state
                    localPlatformMoving = true;
                    //work out velocity
                    if (inputManager.KeyDown(Keys.Left) && inputManager.KeyDown(Keys.Right))
                        platform_local.Velocity = new Vector2(0, 0);
                    else if (inputManager.KeyDown(Keys.Left) && inputManager.KeyUp(Keys.Right))
                        platform_local.Velocity = new Vector2(-10, 0);
                    else if (inputManager.KeyDown(Keys.Right) && inputManager.KeyUp(Keys.Left))
                        platform_local.Velocity = new Vector2(10, 0);
                    sendPacket = true;
                }
            }

            #region collect dead reckoning performance
            //press p to store dead reckoning data during game
            if (inputManager.KeyPressed(Keys.P))
            {
                //serialize data
                //output path - file name
                //see bin/x86/debug/
                string path;
                if (isServer)
                    path = @".\Remote.txt";
                else
                    path = @".\Local.txt";
                
                //check file existance
                if (!File.Exists(path))
                    File.Create(path).Dispose();

                //file exist
                if (File.Exists(path))
                {
                    //append data  
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter(path, true))
                    {
                        string str;

                        //write in clock sync info
                        str = "Start\t" + startTime + "\tGlobal\t" + startTimeInt;
                        if (isServer)
                            str += "\tInitalLagInMillisecond\t" + initLagInMillisec;
                        file.WriteLine(str);

                        if (isServer)
                        {
                            for (int i = 0; i < hostSide.Count; i++)
                            {
                                str = timeTag[i] + "\t" + hostSide[i] + "\t" + hostVel[i] + "\t" + timeTagInMillisec[i];
                                file.WriteLine(str);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < clientSide.Count; i++)
                            {
                                str = timeTag[i] + "\t" + clientSide[i] + "\t" + clientVel[i] + "\t" + timeTagInMillisec[i];
                                file.WriteLine(str);
                            }
                        }
                    }
                }
            }
            #endregion


            /* check collision with local platform */
            Rectangle platformRect_local = platform_local.Rectangle;
            Color[] platformColor_local = platform_local.ColorData;
            Rectangle ballRect = ball.Rectangle;
            Color[] ballColor = ball.ColorData;

            //ball collade with plaform_player
            if (ballRect.Intersects(platformRect_local))
            {
                //collision happens, update state to remote side
                sendPacket = true;

                //check pixel collision
                if (UpdateCollision(ballRect, ballColor, platformRect_local, platformColor_local))
                {
                    //if ball center is higher than platform, ball's velocity Y is negative 
                    //if ball center is lower than platform, ball's velocity Y is positive
                    Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
                    Vector2 platformOrigin_local = new Vector2(platform_local.Origin.X + platformRect_local.X, platform_local.Origin.Y + platformRect_local.Y);

                    //platform height is 25, so check -12 to 12 around origin.y
                    if ((ballOrigin.Y - platformOrigin_local.Y) <= -12)
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y) * -1);
                    else if ((ballOrigin.Y - platformOrigin_local.Y) >= 12)
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                    else if (Math.Abs(ballOrigin.Y - platformOrigin_local.Y) < 12)
                        ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);
                }
            }


            /* under not lagging situation */
            /* this part of code should only work for server */
            Rectangle platformRect_remote = platform_remote.Rectangle;
            Color[] platformColor_remote = platform_remote.ColorData;
            //ball collade with plaform_player
            if (ballRect.Intersects(platformRect_remote))
            {
                //check pixel collision
                if (UpdateCollision(ballRect, ballColor, platformRect_remote, platformColor_remote))
                {
                    //if ball center is higher than platform, ball's velocity Y is negative 
                    //if ball center is lower than platform, ball's velocity Y is positive
                    Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
                    Vector2 platformOrigin_remote = new Vector2(platform_remote.Origin.X + platformRect_remote.X, platform_local.Origin.Y + platformRect_remote.Y);

                    //platform height is 25, so check -12 to 12 around origin.y
                    if ((ballOrigin.Y - platformOrigin_remote.Y) <= -12)
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y) * -1);
                    else if ((ballOrigin.Y - platformOrigin_remote.Y) >= 12)
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                    else if (Math.Abs(ballOrigin.Y - platformOrigin_remote.Y) < 12)
                        ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);
                }
            }

            base.Update(gameTime);
            ball.Update(gameTime);
            platform_local.Update(gameTime);
            //update remote platform
            if (lagCompen == LagCompensation.DeadReckoning)
                DeadReckoning();
            //Console.WriteLine(remotePlatformVel.X);
            platform_remote.Update(gameTime);
            
            //record dead reckoning
            //we check remote platform in receive packet function (host side)
            //let's check client side local platform
            //if platform moves, record it.

            //on host side, we check remote platform (coded in receivePacket())
            //that's where we get remote platform data

            //on client side, we check local platform so that we can compare the same platform
            //thus, do it below
            if (!isServer)
            {
                //skip the case that player presses two buttons at the same time
                //I'm not gonna do that
                //and this game is not for trail or anyone else
                if (inputManager.KeyDown(Keys.Left) || inputManager.KeyDown(Keys.Right))
                {
                    if (!deadMoving)
                    {
                        deadMoving = true;
                        timeTag.Add(current);
                        clientSide.Add(ball.Position.Y);
                        clientVel.Add(ball.Velocity.Y);
                        timeTagInMillisec.Add(current.TotalMilliseconds);
                    }
                }
                else
                {
                    if (inputManager.KeyUp(Keys.Left) || inputManager.KeyUp(Keys.Right))
                    {
                        deadMoving = false;
                    }
                }
            }

            //testing lag below
            //because we record client local state and host remote state
            //so here, we only apply lag to client side

            //Quote from MSDN
            //The latency simulation is applied on the sending machine, 
            //so if you set this property differently on each machine, 
            //only outgoing packets will be affected by the local value of the property.

            //Latency introduced through this setting is not included in the RoundtripTime property, 
            //which always just reports the physical network latency.

            //Packets sent without any ordering guarantee will be given 
            //a random latency normally distributed around the specified value, 
            //introducing packet reordering as well as raw latency. 
            //Packets sent using SendDataOptions.InOrder will be delayed without reordering.

            //we use SendDataOptions.InOrder
            //see SendPackets()

            //*********************************************************************
            //*                        IMPORTANCE NOTICE                          *
            //*remember apply lag to client when you want to record DR (!isServer)*
            //*apply lag to host in trail (isServer)                              *
            //*********************************************************************
            if (session != null && isServer)
            {
                var rnd = new Random();
                //create a number, 1 <= int <= 1000
                int randomLag = rnd.Next(1, 1001);


                //apply lag
                TimeSpan lagh = new TimeSpan(0, 0, 0, 0, 8);
                session.SimulatedLatency = lagh;

                //in this block we generate latency
                //if we are host 
                //first send a 'l' char
                //then we delay message for a period of time

                //thus only clients gets delayed reading host state
                //host still has no problem reading client states

                //##########################
                //therefore it is important that
                //test subjects only play on client machine


            }

            if (session != null)
            {
                //when we have change on ball or platform, update to remote side

                //generate lag here
                //mark server lag flag after send 1st l tag
                //mark server consisCheck after send 2nd l tag
                
                //we send packet in following situation
                //no lag compensation algorithm applied
                //collision with platform, local platform oriented
                //platform state change, local oriented
                if (lagCompen == LagCompensation.None || sendPacket)
                {
                    SendPackets(PacketType.Data);
                    //if we send packet because some conditions are satisfied
                    //then remove flag after send packet
                    if (sendPacket)
                        sendPacket = false;
                    //test random lag generator
                    /*
                     * test func sometimes cannot receive response from npt server
                     * which result in program halt
                     * an exception handler can avoid this situation
                     * 
                     * my task is only to validate weather the random
                     * latency generator working or not
                     * So! if you need the handler, implement it below
                     */
                    //TestLatency();
                }
                ReceivePackets(gameTime);

                //Update the NetworkSession
                session.Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (clearColor == Color.DarkBlue || clearColor == Color.DarkGreen || clearColor == Color.DarkRed || clearColor == Color.DarkGoldenrod)
            {
                Game1.myGameInstance.GraphicsDevice.Clear(clearColor);
            }

            #region Display Session Info
            string sessionInformation = "";
            GamePadState gamePadStat = GamePad.GetState(PlayerIndex.One);
            string state = "";
            switch (gameState)
            {
                case GameState.Menu:
                    if (gamePadStat.IsConnected)
                        state = "Menu\n\n" + "Press X to sign in\n" + "Press LB to Host\n" + "Press RB to Join";
                    else
                        state = "Menu\n\n" + "Press Left Shift to sign in\n" + "Press H to Host\n" + "Press F to Join";
                    break;
                case GameState.FindGame:
                    {
                        if (gamePadStat.IsConnected)
                            state = "Finding Game\n\n" + "Press A to enter game\n" + "Press B to return to menu";
                        else
                            state = "Finding Game\n\n" + "Press Enter to enter game\n" + "Press ESC to return to menu";

                        //If we have an availableSession, draw the details to the screen
                        if (availableSession != null)
                        {
                            string HostGamerTag = availableSession.HostGamertag;
                            int GamersInSession = availableSession.CurrentGamerCount;
                            int OpenPrivateGamerSlots = availableSession.OpenPrivateGamerSlots;
                            int OpenPublicGamerSlots = availableSession.OpenPublicGamerSlots;
                            sessionInformation = "Session available from gamertag " + HostGamerTag +
                                "\n" + GamersInSession + " players already in this session. \n" +
                                +OpenPrivateGamerSlots + " open private player slots available. \n" +
                                +OpenPublicGamerSlots + " public player slots available.";
                        }
                        break;
                    }
                case GameState.PlayGame:
                    {
                        switch (session.SessionState)
                        {
                            case NetworkSessionState.Lobby:
                                {
                                    state = "Lobby\n\n";
                                    if (isServer)
                                        state += "Press Y to launch the game\n" + "Press ESC to return to the lobby";
                                    break;
                                }
                            case NetworkSessionState.Playing:
                                {
                                    state = "Playing Game";
                                    if (isServer)
                                        state += " - Host\n\n" + "Press ESC to return to the lobby";
                                    else
                                        state += " - Client\n\n" + "Press ESC to return to the menu";
                                    /* game start, I don't want to see menu */
                                    state = "";
                                    break;
                                }
                        }
                    }
                    break;

            }

            //Draw sessionInformation
            if (gameState == GameState.FindGame)
                spriteBatch.DrawString(font, sessionInformation, new Vector2(30, ScreenManager.Instance.Dimensions.Y/4), color);

            //Draw the current state
            if (!gameStart)
                spriteBatch.DrawString(font, state, new Vector2(30, ScreenManager.Instance.Dimensions.Y/2), color);

            #endregion

            base.Draw(spriteBatch);
            ball.Draw(spriteBatch);
            platform_local.Draw(spriteBatch);
            platform_remote.Draw(spriteBatch);

            if (gameEnd)
            {
                if (gameStart && gameEnd)
                    spriteBatch.DrawString(font, "Press Space to Re-Start..",
                        new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Space to Re-Start..").X / 2,
                            ScreenManager.Instance.Dimensions.Y / 2),
                        Color.White);
            }
        }

        /* check each pixel on two texture, looking for overlap */
        public bool UpdateCollision(Rectangle rect1, Color[] colorData1, Rectangle rect2, Color[] colorData2)
        {
            int top = Math.Max(rect1.Top, rect2.Top);
            int bottom = Math.Min(rect1.Bottom, rect2.Bottom);
            int left = Math.Max(rect1.Left, rect2.Left);
            int right = Math.Min(rect1.Right, rect2.Right);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    Color color1 = colorData1[(x - rect1.Left) + (y - rect1.Top) * rect1.Width];
                    Color color2 = colorData2[(x - rect1.Left) + (y - rect2.Top) * rect2.Width];

                    if (color1.A != 0 && color2.A != 0)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks for user input and reacts accordingly
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        void UpdateInput(GameTime gameTime)
        {
            #region Handle Input
            GamePadState currentState = GamePad.GetState(PlayerIndex.One);
            
            //gamepad checks
            if (currentState.IsConnected)
            {

                switch (gameState)
                {
                    case GameState.Menu:
                        {
                            if (currentState.Buttons.X == ButtonState.Pressed)
                                SignIn();
                            if (currentState.Buttons.LeftShoulder == ButtonState.Pressed)
                            {
                                if (session == null)
                                    HostGame();
                            }
                            if (currentState.Buttons.RightShoulder == ButtonState.Pressed)
                            {
                                if (session == null)
                                    FindGame();
                            }
                            break;
                        }
                    case GameState.FindGame:
                        {
                            if (currentState.Buttons.B == ButtonState.Pressed)
                                gameState = GameState.Menu;

                            else if (currentState.Buttons.A == ButtonState.Pressed)
                                JoinGame();

                            break;
                        }
                    case GameState.PlayGame:
                        {
                            switch (session.SessionState)
                            {
                                case NetworkSessionState.Lobby:
                                    {
                                        if (currentState.Buttons.Y == ButtonState.Pressed && isServer)
                                            session.StartGame();
                                        else if (currentState.Buttons.B == ButtonState.Pressed)
                                        {
                                            session.Dispose();
                                            session = null;
                                            gameState = GameState.Menu;
                                        }
                                        break;
                                    }
                                case NetworkSessionState.Playing:
                                    {
                                        //if (currentState.Buttons.B == ButtonState.Pressed && bHost)
                                        //    session.EndGame();
                                        if (currentState.Buttons.A == ButtonState.Pressed && isServer)
                                            clearColor = Color.DarkGreen;
                                        else if (currentState.Buttons.X == ButtonState.Pressed && isServer)
                                            clearColor = Color.DarkBlue;
                                        else if (currentState.Buttons.Y == ButtonState.Pressed && isServer)
                                            clearColor = Color.DarkGoldenrod;
                                        else if (currentState.Buttons.B == ButtonState.Pressed && isServer)
                                            clearColor = Color.DarkRed;
                                        else if (currentState.Buttons.Back == ButtonState.Pressed)
                                        {
                                            session.Dispose();
                                            session = null;
                                            gameState = GameState.Menu;
                                            clearColor = Color.CornflowerBlue;
                                        }
                                        break;
                                    }
                            }
                            break;

                        }
                }
            }
            else if (!currentState.IsConnected)     //keyboard checks
            {
                switch (gameState)
                {
                    case GameState.Menu:
                        {
                            if (inputManager.KeyPressed(Keys.LeftShift))
                                SignIn();
                            if (inputManager.KeyPressed(Keys.H))
                            {
                                if (session == null)
                                    HostGame();
                                Console.WriteLine("Session Created..");
                            }
                            if (inputManager.KeyPressed(Keys.F))
                            {
                                if (session == null)
                                    FindGame();
                            }
                            break;
                        }
                    case GameState.FindGame:
                        {
                            if (inputManager.KeyPressed(Keys.Escape))
                                gameState = GameState.Menu;

                            else if (inputManager.KeyPressed(Keys.Enter))
                                JoinGame();

                            break;
                        }
                    case GameState.PlayGame:
                        {
                            switch (session.SessionState)
                            {
                                case NetworkSessionState.Lobby:
                                    {
                                        if (inputManager.KeyPressed(Keys.Y) && isServer)
                                            session.StartGame();
                                        else if (inputManager.KeyPressed(Keys.Escape))
                                        {
                                            session.Dispose();
                                            session = null;
                                            gameState = GameState.Menu;
                                        }
                                        break;
                                    }
                                case NetworkSessionState.Playing:
                                    {
                                        //if (currentState.Buttons.B == ButtonState.Pressed && bHost)
                                        //    session.EndGame();
                                        if (inputManager.KeyPressed(Keys.V) && isServer)
                                            clearColor = Color.DarkGreen;
                                        else if (inputManager.KeyPressed(Keys.B) && isServer)
                                            clearColor = Color.DarkBlue;
                                        else if (inputManager.KeyPressed(Keys.N) && isServer)
                                            clearColor = Color.DarkGoldenrod;
                                        else if (inputManager.KeyPressed(Keys.M) && isServer)
                                            clearColor = Color.DarkRed;
                                        else if (inputManager.KeyPressed(Keys.Escape))
                                        {
                                            session.Dispose();
                                            session = null;
                                            gameState = GameState.Menu;
                                            clearColor = Color.CornflowerBlue;
                                        }
                                        break;
                                    }
                            }
                            break;

                        }
                }
            }
            #endregion
        }

        /// <summary>
        /// HostGame is responsible for initializing a network connection and setting up a session
        /// </summary>
        protected void HostGame()
        {
            if (SignedInGamer.SignedInGamers.Count == 0)
                SignIn();

            else if (SignedInGamer.SignedInGamers.Count == 1)
            {
                NetworkSessionProperties sessionProperties = new NetworkSessionProperties();

                sessionProperties[(int)SessionProperty.GameMode]
                    = (int)GameMode.Practice; // Game mode
                sessionProperties[(int)SessionProperty.SkillLevel]
                    = (int)SkillLevel.Beginner; // Score to win
                sessionProperties[(int)SessionProperty.ScoreToWin]
                    = 100;

                int maximumGamers = 4;  // The maximum supported is 31
                int privateGamerSlots = 0;
                int maximumLocalPlayers = 2;


                // Create the session
                session = NetworkSession.Create(
                    NetworkSessionType.SystemLink,
                    maximumLocalPlayers, maximumGamers, privateGamerSlots,
                    sessionProperties);
               
                isServer = true;
                session.AllowHostMigration = false;
                session.AllowJoinInProgress = false;

                /* 
                 * XNA provides latency simulation on SystemLink Game 
                 * to test the Internet features without connecting 
                 * to the Internet.
                 */
                /* days, hours, minutes, seconds, milliseconds */
                //TimeSpan latency = new TimeSpan(0, 0, 0, 0, 800);
                //session.SimulatedLatency = latency;
                gameState = GameState.PlayGame;
            }
        }

        /// <summary>
        /// FindGame is responsible for searching for available sessions
        /// </summary>
        protected void FindGame()
        {
            if (SignedInGamer.SignedInGamers.Count == 0)
                SignIn();

            else if (SignedInGamer.SignedInGamers.Count == 1)
            {
                gameState = GameState.FindGame;
                int maximumLocalPlayers = 2;
                NetworkSessionProperties searchProperties = new NetworkSessionProperties();
                searchProperties[(int)SessionProperty.GameMode] = (int)GameMode.Practice;
                searchProperties[(int)SessionProperty.SkillLevel] = (int)SkillLevel.Beginner;

                availableSessions = NetworkSession.Find(
                    NetworkSessionType.SystemLink, maximumLocalPlayers, searchProperties);

                if (availableSessions.Count != 0)
                    availableSession = availableSessions[sessionIndex];
                Console.WriteLine("Trying to find a game, Available Session : " + availableSessions.Count);

                isServer = false;
            }
        }

        /// <summary>
        /// JoinGame is responsible for joining an available session
        /// </summary>
        protected void JoinGame()
        {
            if (availableSessions.Count - 1 >= sessionIndex)
            {
                session = NetworkSession.Join(availableSessions[sessionIndex]);

                AddNetworkingEvents();
                gameState = GameState.PlayGame;
            }
        }
        /// <summary>
        /// SendPackets is responsible for writing all player information 
        /// into a packet that is sent to the host or clients
        /// </summary>
        void SendPackets(PacketType packetType)
        {
            switch (packetType)
            {
                case PacketType.Data:
                    {
                        foreach (LocalNetworkGamer gamer in session.LocalGamers)
                        {
                            //mark host and client
                            if (isServer)
                                packetWriter.Write('h');
                            else
                                packetWriter.Write('k');
                            packetWriter.Write(ball.Position);
                            packetWriter.Write(ball.Velocity);
                            packetWriter.Write(platform_local.Position);
                            packetWriter.Write(platform_local.Velocity);

                            // Send it to all remote gamers.
                            gamer.SendData(packetWriter, SendDataOptions.InOrder);
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// ReadPackets is responsible for reading and storing all information 
        /// from a received packet.
        /// </summary>
        void ReceivePackets(GameTime gameTime)
        {
            NetworkGamer sender;

            foreach (LocalNetworkGamer gamer in session.LocalGamers)
            {
                // Keep reading while packets are available.
                while (gamer.IsDataAvailable)
                {
                    //Read a single packet.
                    //Even if we are the host, we must read to clear the queue
                    gamer.ReceiveData(packetReader, out sender);

                    //read one char, see what data this packet contains
                    /*
                     * 'h': host game state
                     * 'k': client game state
                     * 'l': lag flag
                     * 'p': ping test //only enable this to test if random lag function is working
                     *      you should not receive this char in real game
                     *      NTP server is unstable, causing timedout exception
                     *      
                     * 'g': client side starts game, send NTP time to host
                     * 'r': host receives client NTP time, 
                     *      process it and compare to local NTP time
                     *      work out how much time between two side started the game
                     * 'e': game end      
                     */ 
                    hostTag = packetReader.ReadChar();
                    /* normal packet */
                    /* keep reading following message */

                    if ((hostTag == 'k' && isServer) || (hostTag == 'h' && !isServer))
                    {
                        ballPos = packetReader.ReadVector2();
                        ballVel = packetReader.ReadVector2();
                        remotePlatformPos = packetReader.ReadVector2();
                        remotePlatformVel = packetReader.ReadVector2();

                        //record dead reckoning
                        //figure out wether platform is moving or not
                        //on host side, we check remote platform
                        //on client side, we check local platform (coded somewhere else in Update() )

                        //remeber
                        //this position/velocity is still the pos/vel on host screen
                        //to compare this pos/vel to client pos/vel
                        //you need to invert it manully
                        //see void ProcessReceivedPacket()
                        if (isServer)
                        {
                            //first, let's check previous state of deadMoving
                            //then check received remotePlatformVel, weather causing a state change
                            if (!deadMoving && remotePlatformVel.X != 0)
                            {
                                deadMoving = true;
                                //  #record
                                //what we see is opposite to the other side
                                //because all players see their platform at bottom
                                //thus we need to work out how does screen look like on the other side
                                //top platform y = 20, height 25
                                //bot platform y = 755
                                //thus ball to top platform : ball.y - 45
                                //invert position : 755 - ball + 45
                                timeTag.Add(current);
                                hostSide.Add(ball.Position.Y);
                                hostVel.Add(ball.Velocity.Y);
                                timeTagInMillisec.Add(current.TotalMilliseconds);
                            }
                            else if (deadMoving && remotePlatformVel.X == 0)
                                deadMoving = false;

                            //now we have cleared when platform moves and when it stop
                            //record ball position when platform moves
                            //this is on host side, host has the one true state of the ball
                            //so we don't need to process packet info
                            //record at #record above
                        }

                        //we have the data from remote side
                        //but we need to work out where to render objects on local screen
                        ProcessReceivedPacket();
                    }
                    else if (hostTag == 'l')
                    {
                        /* current packet indicates latencty simulation, no more packets coming in */
                        /* or indicate latency has returned to normal */
                        //as a recevier, if lag ends, consischeck
                        if (!isServer)
                            lagFlag = true;

                        /* 
                         *  ATTITION
                         *  as receiver, may be we need send a packet
                         *  to update server, right after lag.
                         *  if so, do it below.
                         */

                    }
                    else if (hostTag == 'p' && isServer)
                    {
                        /* Only Used When Testing Random Lag */

                        //sometimes client sends a ping request with NTP time
                        //if we are the host, we request a NTP immediately when we received this char 
                        //and compare NTP with the NTP(client) in ping packet
                        //to work out client to host single trip time

                        //A single tick represents one hundred nanoseconds
                        //There are 10,000 ticks in a millisecond

                        DateTime nowDT = NTP.GetNetworkTime();
                        long hostTicks = NTP.ElapsedTicks(nowDT);

                        byte[] bytes = packetReader.ReadBytes(8);
                        long clientTicks = BitConverter.ToInt64(bytes, 0);

                        Console.WriteLine("Possible Latency(CTH) : " + (hostTicks - clientTicks)/10000 + " ms");
                    }
                    else if (hostTag == 'g' && !isServer)
                    { 
                        //we have received a game start notification from host
                        ball.Velocity = new Vector2(-7, -10);
                        gameStart = true;

                        //if we are recording DR data 
                        if (DRTestMode)
                        {
                            //record local game start time
                            startTime = gameTime.TotalGameTime;
                            //record internet game start time
                            startTimeInt = NTP.GetNetworkTime();
                            sendPacket = true;
                            SendStartIntToHost();
                        }
                    }
                    else if (hostTag == 'r' && isServer)
                    {
                        //client sends back their internet start time
                        //if we are the host, work out 
                        //client to host single trip time
                        byte[] bytes = packetReader.ReadBytes(8);
                        long clientTicks = BitConverter.ToInt64(bytes, 0);

                        long hostTicks = NTP.ElapsedTicks(startTimeInt);
                        initLagInMillisec = (clientTicks - hostTicks)/10000;
                    }
                    else if (hostTag == 'e')
                    { 
                        //we have received a game end flag
                        ball.Velocity = new Vector2(0, 0);
                        gameEnd = true;
                    }
                }
            }
        }

        /// <summary>
        /// Process new update right after receive packet
        /// host controls the ball
        /// 
        /// reverse opponent platform position
        /// so it can appera on top of screen
        /// </summary>
        void ProcessReceivedPacket()
        {
            float screenWidth = ScreenManager.Instance.Dimensions.X;
            float screenHeight = ScreenManager.Instance.Dimensions.Y;

            /* if there is no lag, we let the host decides whether a collision happens or not */

            /* h tag indicates packet is from host, sync state */
            /* remote side position is always inverted */
            /* for non host, we also need to invert ball position */

            //Ball position may not be inverted perfectly, I have a feeling
            
            
            /* Waiting to be further tested!!! */
            if (hostTag == 'h' && !isServer && !lagFlag)
            {
                //Console.WriteLine("Got a packet from host");
                ball.Position = new Vector2(screenWidth, screenHeight) - (ballPos + ball.Origin) - ball.Origin;
                ball.Velocity = ballVel * new Vector2(-1, -1);

                platform_remote.Position =
                    new Vector2(screenWidth - remotePlatformPos.X - platform_remote.Dimension.X, screenHeight - platform_remote.Dimension.Y - remotePlatformPos.Y);
                platform_remote.Velocity = remotePlatformVel * new Vector2(-1, -1);
            }
            /* k tag indicates packets is from a client */
            /* without considering lagging, we only update remote platform position */
            else if (hostTag == 'k' && isServer && !lagFlag)
            {
                //Console.WriteLine("Got a packet from client");
                platform_remote.Position =
                    new Vector2(screenWidth - remotePlatformPos.X - platform_remote.Dimension.X, screenHeight - platform_remote.Dimension.Y - remotePlatformPos.Y);
                platform_remote.Velocity = remotePlatformVel * new Vector2(-1, -1);
            }

            /* 
             * if there was a lag, we need to enable remote prediction on both side 
             * which should be in
             * 
             * void Update() 
             * 
             * flag up in receivepacket()
             * lag = !lag;
             */ 
        }

        /// <summary>
        /// SignIn displays the Xbox360 Guide
        ///
        static protected void SignIn()
        {
            if (!Guide.IsVisible)
                Guide.ShowSignIn(1, false); //true require online profile
        }

        protected void AddNetworkingEvents()
        {
            session.GamerJoined += new EventHandler<GamerJoinedEventArgs>(session_GamerJoined);
            session.GamerLeft += new EventHandler<GamerLeftEventArgs>(session_GamerLeft);
            session.GameStarted += new EventHandler<GameStartedEventArgs>(session_GameStarted);
            session.GameEnded += new EventHandler<GameEndedEventArgs>(session_GameEnded);
            session.SessionEnded += new EventHandler<NetworkSessionEndedEventArgs>(session_SessionEnded);
        }

        protected void RemoveNetworkingEvents()
        {
            session.GamerJoined -= new EventHandler<GamerJoinedEventArgs>(session_GamerJoined);
            session.GamerLeft -= new EventHandler<GamerLeftEventArgs>(session_GamerLeft);
            session.GameStarted -= new EventHandler<GameStartedEventArgs>(session_GameStarted);
            session.GameEnded -= new EventHandler<GameEndedEventArgs>(session_GameEnded);
            session.SessionEnded -= new EventHandler<NetworkSessionEndedEventArgs>(session_SessionEnded);
        }

        #region Handlers
        //Event Handling
        void session_GamerJoined(object sender, GamerJoinedEventArgs e)
        {
        }
        void session_GamerLeft(object sender, GamerLeftEventArgs e)
        {
        }
        void session_GameStarted(object sender, GameStartedEventArgs e)
        {
        }
        void session_GameEnded(object sender, GameEndedEventArgs e)
        {
        }
        void session_SessionEnded(object sender, NetworkSessionEndedEventArgs e)
        {
            session.Dispose();
            session = null;
        }
        /* This is currently unused.
        void session_HostChanged(object sender, HostChangedEventArgs e)
        {
        }
        */
        #endregion

        #region Core of Project

        public void DeadReckoning()
        {
            //this method allows to reduce packet sending rate
            //remote platform moves based on last updated remotePlatformVel

            //Console.WriteLine("Dead Reckoning : " + platform_remote.Position.X + " with " + remotePlatformVel.X);
            if (remotePlatformVel != null)
                platform_remote.Position += (remotePlatformVel * new Vector2(-1, -1));
        }

        public void Prediction()
        { 
        
        }
        #endregion

        /************************************/
        /**********test functions************/
        /***********delete later*************/

        //if client, request a NTP and send it to host
        //so host can work out client to host trip time
        void TestLatency()
        {
            foreach (LocalNetworkGamer gamer in session.LocalGamers)
            {
                if (!isServer)
                {
                    DateTime pingSent = NTP.GetNetworkTime();
                    long ticks = NTP.ElapsedTicks(pingSent);
                    byte[] bytes = BitConverter.GetBytes(ticks);
                    packetWriter.Write('p');
                    packetWriter.Write(bytes);
                    // Send it to all remote gamers.
                    gamer.SendData(packetWriter, SendDataOptions.InOrder);
                }   
            }
        }

        //client send NTP start time to host
        void SendStartIntToHost()
        {
            foreach (LocalNetworkGamer gamer in session.LocalGamers)
            {
                if (!isServer)
                {
                    long ticks = NTP.ElapsedTicks(startTimeInt);
                    byte[] bytes = BitConverter.GetBytes(ticks);
                    packetWriter.Write('r');
                    packetWriter.Write(bytes);
                    // Send it to all remote gamers.
                    gamer.SendData(packetWriter, SendDataOptions.InOrder);
                }
            }
        }
    }
}