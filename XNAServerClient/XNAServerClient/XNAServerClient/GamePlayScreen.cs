using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        Platform platform_local;
        Platform platform_remote;
        Ball ball;

        SpriteFont font;

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
        bool isServer;

        bool lag;

        enum GameState { Menu, FindGame, PlayGame }
        enum SessionProperty { GameMode, SkillLevel, ScoreToWin }
        enum GameMode { Practice, Timed, CaptureTheFlag }
        enum SkillLevel { Beginner, Intermediate, Advanced }
        enum PacketType { Enter, Leave, Data }

        //public static struct AppDataUnit
        //{
        //    /* 'h' for host, 'k' for online gamer, 'l'indicates lagging tag */
        //    Char hostTag;
        //    Vector2 ballPos;
        //    Vector2 ballVel;
        //    Vector2 remotePlatformPos;
        //    Vector2 remotePlatformVel;
        //}
        Char hostTag;
        Vector2 ballPos;
        Vector2 ballVel;
        Vector2 remotePlatformPos;
        Vector2 remotePlatformVel;


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

            /* 4.0 */
            color = Color.White;
            clearColor = Color.CornflowerBlue;
            gameState = GameState.Menu;
            sessionIndex = 0;
            packetReader = new PacketReader();
            packetWriter = new PacketWriter();

            lag = false;
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
            inputManager.Update();
            UpdateInput(gameTime);

            base.Update(gameTime);
            ball.Update(gameTime);
            platform_local.Update(gameTime);
            platform_remote.Update(gameTime);

            if (session != null)
            {
                SendPackets(PacketType.Data);
                ReceivePackets();

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
            spriteBatch.DrawString(font, state, new Vector2(30, ScreenManager.Instance.Dimensions.Y/2), color);
            #endregion

            base.Draw(spriteBatch);
            ball.Draw(spriteBatch);
            platform_local.Draw(spriteBatch);
            platform_remote.Draw(spriteBatch);
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
        void ReceivePackets()
        {
            NetworkGamer sender;

            foreach (LocalNetworkGamer gamer in session.LocalGamers)
            {
                // Keep reading while packets are available.
                while (gamer.IsDataAvailable)
                {
                    // Read a single packet.
                    //Even if we are the host, we must read to clear the queue
                    gamer.ReceiveData(packetReader, out sender);

                    hostTag = packetReader.ReadChar();
                    ballPos = packetReader.ReadVector2();
                    ballVel = packetReader.ReadVector2();
                    remotePlatformPos = packetReader.ReadVector2();
                    remotePlatformVel = packetReader.ReadVector2();
                    ProcessReceivedPacket();
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
            if (hostTag == 'h' && !isServer)
            {
                ball.Position = new Vector2(screenWidth, screenHeight) - ballPos;
                ball.Velocity = ballVel * new Vector2(-1, -1);
                platform_remote.Position =
                    new Vector2(screenWidth - remotePlatformPos.X - platform_remote.Dimension.X, screenHeight - platform_remote.Dimension.Y - remotePlatformPos.Y);
                platform_remote.Velocity = remotePlatformVel * new Vector2(-1, -1);
            }
            else if (hostTag == 'k' && isServer)
            {
                platform_remote.Position =
                    new Vector2(screenWidth - remotePlatformPos.X - platform_remote.Dimension.X, screenHeight - platform_remote.Dimension.Y - remotePlatformPos.Y);
                platform_remote.Velocity = remotePlatformVel * new Vector2(-1, -1);
            }
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

        //Event Handling
        void session_GamerJoined(object sender, GamerJoinedEventArgs e)
        {
            Console.WriteLine("A new Gamer just joined the game.");
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


    }
}