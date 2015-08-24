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
        //EH_AI see single player mode, extreme hard level
        enum LagCompensation { None, DeadReckoning, PlayPattern, EH_AI}
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

        int lagCounter;
        bool lagIndicator;
        bool AIControl;

        bool hasPrediCatch;
        bool hasPrediCenter;

        bool calcCollisionPos;
        bool movePlatformRemote;
        bool movePlatformCenter;

        //X coordinate where AI needs to move
        float targetPositionX;

        //extreme hard level in single player mode
        bool checkMoveWrong;
        bool moveRemoteWrong;
        float targetWrongX;

        int moveWrongTimer;

        //because ball true state is the host state
        //so sometimes client may see 
        //ball is bounced back 
        //without collision
        
        //we use following bool
        //record current update state
        //and check this variable 
        //in next update
        bool ballFlyingUp;

        Random rnd;
        //4th
        Vector2 windowEdge;
        //following data type used
        //in statePack reversePosition(statePack state)
        //because our prediction model is based on 
        //the platform at bottom
        //so we need to convert data for the platform at top
        public struct statePack
        {
            Vector2 ballVel;
            Vector2 ballPos;
            Vector2 platPos;
            Vector2 windEdg;

            public statePack(Vector2 vel, Vector2 posb, Vector2 posp, Vector2 wdwe)
            {
                ballVel = vel;
                ballPos = posb;
                platPos = posp;
                windEdg = wdwe;
            }

            public Vector2 bVel
            {
                get { return ballVel; }
                set { ballVel = value; }
            }

            public Vector2 bPos
            {
                get { return ballPos; }
                set { ballPos = value; }
            }

            public Vector2 pPos
            {
                get { return platPos; }
                set { platPos = value; }
            }

            public Vector2 wEdg
            {
                get { return windEdg; }
                set { windEdg = value; }
            }
        }

        statePack currentState;
        //every time we switch between
        //AI and human, we record it
        ArrayList AISwitch;
        bool prevAIControlling;
        /*******************************************/
        /*all variables defined beliw are temproral*/
        /*******************************************/
        string testStr = "";
        string testStr2 = "";

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

            lagCompen = LagCompensation.EH_AI;
            lagFlag = false;

            DRTestMode = false;

            //record dead reckoning
            //make below true if you want to record DR data
            //remember change latency generator
            //apply it to client side
            // !isServer in if condition

            deadMoving = false;
            clientSide = new ArrayList();
            hostSide = new ArrayList();
            hostVel = new ArrayList();
            clientVel = new ArrayList();

            timeTag = new ArrayList();
            timeTagInMillisec = new ArrayList();

            initLagInMillisec = 0;

            lagCounter = 420;
            lagIndicator = false;
            AIControl = false;

            hasPrediCatch = false;
            hasPrediCenter = false;
            windowEdge = new Vector2(0, 0);

            calcCollisionPos = false;
            movePlatformRemote = false;
            movePlatformCenter = false;
            targetPositionX = 0f;

            checkMoveWrong = false;
            moveRemoteWrong = false;
            targetWrongX = 0f;
            rnd = new Random();

            AISwitch = new ArrayList();
            prevAIControlling = false;
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
            /*****test variables******/
            testStr = "";
            /*************************/
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
            if (!gameStart && session != null && session.AllGamers.Count > 1)
            {
                if (inputManager.KeyPressed(Keys.Y) && isServer)
                {
                    if (session.SessionState == NetworkSessionState.Lobby)
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

                    ball.Velocity = new Vector2(-7, 10);
                    gameStart = true;
                    sendPacket = true;

                    //if we are collecting DR data
                    //then we need to get a NTP time
                    //when game started
                    //and host notifies clients 
                    //that gets a NTP time too
                    if (DRTestMode)
                    {
                        //record local game start time
                        startTime = gameTime.TotalGameTime;
                        //record internet game start time
                        startTimeInt = NTP.GetNetworkTime();
                    }
                }
            }

            //check restart key press
            //restore game state
            if (inputManager.KeyPressed(Keys.R))
                restart();

            //check game exit key press
            if (inputManager.KeyPressed(Keys.P) && !DRTestMode
                && AISwitch.Count != 0)
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
            if (inputManager.KeyPressed(Keys.P) && DRTestMode)
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

            //check if ball is at window edge
            //for 4th prediction model
            if (ball.Position.X <= 0
                || ball.Position.X + ball.ImageWidth >= ScreenManager.Instance.Dimensions.X)
            {
                if (ball.Velocity.Y < 0)
                    windowEdge = ball.Position;
            }
            
            //do XNA update for objects
            base.Update(gameTime);
            ball.Update(gameTime);
            platform_local.Update(gameTime);
            //update remote platform
            //if (lagCompen == LagCompensation.DeadReckoning)
                DeadReckoning();

            //when we update platform_remote info
            //we now have to check whether AI controls the platform
            //if so, in platform.cs, disable player control
            platform_remote.Update(gameTime);

            //update ballFlyingUp used in AI
            if (!isServer && ballFlyingUp && ball.Velocity.Y > 0)
            {
                //in last update, ball was going up
                //but now ball is going down
                ballFlyingUp = false;
                
                //when ball is flying to remote platform
                //we need to calculate Collision
                //
                windowEdge = new Vector2(0, 0);
                if (AIControl)
                {
                    calcCollisionPos = false;
                    movePlatformRemote = false;
                    hasPrediCatch = false;
                    movePlatformRemote = false;
                    targetPositionX = 0f;

                    checkMoveWrong = false;
                    moveRemoteWrong = false;
                    targetWrongX = 0f;
                }
            }
            else if (!isServer && !ballFlyingUp && ball.Velocity.Y < 0)
            {
                ballFlyingUp = true;

                if (AIControl)
                {
                    movePlatformCenter = false;
                    hasPrediCenter = false;
                    
                    //calculate collision position
                    if (!calcCollisionPos)
                    {
                        CalcCollisionPosition();
                        calcCollisionPos = true;
                    }
                }
            }

            //After we have updated all state
            //let's check current state
            //to enable AI controls

            //@param AIControl
            //use this variable on host side
            //to indicate network latency generation

            //we have lagFlag is true
            //but AIControl is false
            //means we just received lagFlag
            //do a state check
            //and flag AIControl, let AI do the trick
            //
            if (lagFlag && !AIControl && !isServer)
            {
                testStr += "Prestate_check ";

                //reset all flags
                calcCollisionPos = false;
                hasPrediCatch = false;
                movePlatformRemote = false;
                movePlatformCenter = false;
                hasPrediCenter = false;
                targetPositionX = 0f;
                checkMoveWrong = false;
                targetWrongX = 0f;
                windowEdge = new Vector2(0, 0);

                //tell AI start playing the remote platfrom
                AIControl = true;
            }

            //on the other hand
            //if we have done the pre-state check
            //and AI has the green light
            //shopping time!
            if (lagFlag && AIControl && !isServer)
            {
                testStr += "AI_control ";
                //append switch info
                if (!prevAIControlling)
                {
                    AISwitch.Add("" + current + "\tAI Controlling");
                    prevAIControlling = true;
                }

                if (lagCompen == LagCompensation.PlayPattern)
                {
                    //if ball is flying  towards 
                    //the remote platform
                    //& we still don't have
                    //the prediction - then predict
                    currentState = new statePack(ball.Velocity, ball.Position,
                        platform_remote.Position, windowEdge);
                    if (ball.Velocity.Y < 0 && !hasPrediCatch)
                    {
                        doPrediction(currentState);
                    }
                    //if ball is flying to local
                    //and we haven't got center prediction
                    else if (ball.Velocity.Y > 0 && !hasPrediCenter)
                    {
                        doPrediction_MoveToCenter(currentState);
                    }

                    //if we know where we need to move
                    //just move it

                    //after we know where collision position is
                    //we set movePlatformRemote true
                    //but we still need to keep checking
                    //our prediction model
                    //if our model comes back with a match
                    //we move platform
                    if (movePlatformRemote && hasPrediCatch)
                    {
                        MoveRemotePlatform(targetPositionX, 1);
                    }
                    else if (movePlatformCenter && hasPrediCenter)
                    {
                        MoveRemotePlatform(ScreenManager.Instance.Dimensions.X / 2, 3);
                    }
                }
                else if (lagCompen == LagCompensation.EH_AI)
                {
                    //calc wrong x
                    if (checkMoveWrong == false)
                    {
                        CalcRemoteWrongPosition();
                        checkMoveWrong = true;
                    }

                    //move to wrongx first
                    if (movePlatformRemote && moveRemoteWrong)
                    {
                        int num = rnd.Next(1, 101);
                        if (num < 95)
                        {
                            MoveRemotePlatform(targetWrongX, 2);
                            moveWrongTimer--;
                        }
                        else
                        {
                            moveRemoteWrong = false;
                            moveWrongTimer = 0;
                        }
                    }
                    else if (!moveRemoteWrong && moveWrongTimer > 0)
                    {
                        moveWrongTimer--;
                    }
                    //move to collision position
                    else if (movePlatformRemote && !moveRemoteWrong && moveWrongTimer <= 0)
                    {
                        MoveRemotePlatform(targetPositionX, 1);
                    }
                }
            }
            else
            {
                testStr += "Player_Control ";
                if (!isServer && prevAIControlling)
                {
                    AISwitch.Add("" + current + "\tPlayer Controlling");
                    prevAIControlling = false;
                }
            }

            //record dead reckoning
            //we check remote platform in receive packet function (host side)
            //let's check client side local platform
            //if platform moves, record it.

            //on host side, we check remote platform (coded in receivePacket())
            //that's where we get remote platform data

            //on client side, we check local platform so that we can compare the same platform
            //thus, do it below
            if (!isServer && DRTestMode)
            {
                //skip the case that player presses two keys at same time
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
                int randomLag = 0;

                if (lagIndicator)
                    randomLag = rnd.Next(450, 1001);
                else
                    randomLag = 0;
                //else
                //    randomLag = rnd.Next(0, 2);

                //at here, we check how lag we are
                //if lag is high enough
                //switch to AI control
                //otherwise player controls

                //AI = true disables player control
                //in platform.cs class

                //remember we have a variable call lagFlag
                //see ProcessReceivedPacket()
                //we use lagFlag to control whether
                //program applies received state to local
                //since we decide only apply lag to host side
                //so we can't change lagFlag on host 
                //and only clients change that flag, enable AI features
                if (randomLag >= 450)
                {
                    //mark on host side
                    //so we remember 
                    //what state we are in 
                    AIControl = true;
                    //tell client lag
                    tellClientLag('l');
                }
                else
                {
                    AIControl = false;
                    tellClientLag('n');
                }
                //apply lag
                TimeSpan lagh = new TimeSpan(0, 0, 0, 0, randomLag);
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

                testStr += "Ping_" + session.SimulatedLatency.TotalMilliseconds + " ";
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

            //check game end state
            //here might be a little tricky
            //because we are implementing a P2P game(not entirely)
            //therefore we only check ball position at local platform side
            //and let remote player update ball position at remote platform side
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

            if (!gameStart && !gameEnd && session != null
                && session.SessionState == NetworkSessionState.Playing && isServer)
                spriteBatch.DrawString(font, "Press Y to Re-Start..",
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Y to Re-Start..").X / 2,
                        ScreenManager.Instance.Dimensions.Y / 2 - 30),
                    Color.White);

            if (!gameStart && !gameEnd && session != null
                && session.SessionState == NetworkSessionState.Playing && !isServer)
                spriteBatch.DrawString(font, "Wait Host to Start..",
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Wait Host to Start..").X / 2,
                        ScreenManager.Instance.Dimensions.Y / 2 - 30),
                    Color.White);

            spriteBatch.DrawString(font, testStr,
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Player_Control Ping_").X / 2,
                        ScreenManager.Instance.Dimensions.Y / 2),
                    Color.White);

            spriteBatch.DrawString(font, testStr2,
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Down ").X / 2,
                        ScreenManager.Instance.Dimensions.Y / 2 + 30),
                    Color.White);

            //because draw method is called 60 times pre second
            //therefor to can use this feature as a clock
            //to generate periodic lag

            //first we count down
            //when counter is zero 
            //we reset the counter and 
            //start generate lag
            //repeat this loop
            if (isServer && gameStart && !gameEnd)
            {
                lagCounter--;
                if (lagCounter == 0 && lagIndicator)
                {
                    lagIndicator = false;
                    lagCounter = 420;
                }
                else if (lagCounter == 0 && !lagIndicator)
                {
                    lagIndicator = true;
                    lagCounter = 180;
                    //lagCounter = 9999;
                }
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
                     * 'n': lag flag down
                     * 'p': ping test //only enable this to test if random lag function is working
                     *      you should not receive this char in real game
                     *      NTP server is unstable, causing timedout exception
                     *      
                     * 'g': client side starts game, send NTP time to host
                     * 'r': host receives client NTP time, 
                     *      process it and compare to local NTP time
                     *      work out how much time between two side started the game
                     * 'e': game end      
                     * 'f': start game
                     */ 
                    hostTag = packetReader.ReadChar();
                    /* normal packet */
                    /* keep reading following message */

                    //rewrite following section in switch-case style
                    //may improve performance
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
                        //we are the client
                        //and we have received a lag flag
                        //it's time to enable our AI to replace player

                        //since lag flag is up
                        //in ProcessReceivedPacket()
                        //we have already stop process incoming packet
                        //from the host

                        //before we do anything, in Update(), let's check current state
                        //is the ball flying to platform_remote? or not?
                    }
                    else if (hostTag == 'n')
                    {
                        if (!isServer)
                        {
                            lagFlag = false;
                            AIControl = false;
                        }
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

                        Console.WriteLine("Possible Latency(CTH) : " + (hostTicks - clientTicks) / 10000 + " ms");
                    }
                    else if (hostTag == 'g' && !isServer)
                    {
                        //we have received a game start notification from host
                        ball.Velocity = new Vector2(-7, -10);
                        gameStart = true;
                        AISwitch.Add("" + current + "\tGameStart");
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
                        initLagInMillisec = (clientTicks - hostTicks) / 10000;
                    }
                    else if (hostTag == 'e')
                    {
                        //we have received a game end flag
                        ball.Velocity = new Vector2(0, 0);
                        gameEnd = true;
                    }
                    else if (hostTag == 'f')
                    {
                        gameStart = false;
                        gameEnd = false;

                        ball.Position = new Vector2(ScreenManager.Instance.Dimensions.X / 2 - ball.ImageWidth / 2, 200);
                        ball.Velocity = new Vector2(0, 0);

                        platform_local.Position = new Vector2(ScreenManager.Instance.Dimensions.X / 2 - platform_local.Dimension.X / 2,
                            ScreenManager.Instance.Dimensions.Y - 20 - platform_local.Dimension.Y);
                        platform_local.Velocity = new Vector2(0, 0);
                        platform_remote.Position = platform_remote.Position = new Vector2(ScreenManager.Instance.Dimensions.X / 2 
                            - platform_remote.Dimension.X / 2, 20);
                        platform_remote.Velocity = new Vector2(0, 0);
                        
                        localPlatformMoving = false;
                        hostTag = '/';
                        ballPos = new Vector2(0, 0);
                        ballVel = new Vector2(0, 0);
                        remotePlatformPos = new Vector2(0, 0);
                        remotePlatformVel = new Vector2(0, 0);

                        sendPacket = true;

                        lagCompen = LagCompensation.EH_AI;
                        lagFlag = false;

                        DRTestMode = false;

                        timeTag = new ArrayList();
                        timeTagInMillisec = new ArrayList();

                        initLagInMillisec = 0;

                        lagCounter = 420;
                        lagIndicator = false;
                        AIControl = false;

                        hasPrediCatch = false;
                        hasPrediCenter = false;
                        windowEdge = new Vector2(0, 0);

                        calcCollisionPos = false;
                        movePlatformRemote = false;
                        movePlatformCenter = false;
                        targetPositionX = 0f;

                        checkMoveWrong = false;
                        moveRemoteWrong = false;
                        targetWrongX = 0f;
                        rnd = new Random();
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
                platform_remote.Position =
                    new Vector2(screenWidth - remotePlatformPos.X - platform_remote.Dimension.X, screenHeight - platform_remote.Dimension.Y - remotePlatformPos.Y);
                platform_remote.Velocity = remotePlatformVel * new Vector2(-1, -1);
            }
        }

        //whenever host decide to create lag
        //it needs to send a packet to client 
        //then clients know they need 
        //to activate AI Controls
        //
        //Yeah! I'm cheating
        void tellClientLag(char c)
        {

            foreach (LocalNetworkGamer gamer in session.LocalGamers)
            {
                //tell all clients lag is comming
                packetWriter.Write(c);
                // Send it to all remote gamers.
                gamer.SendData(packetWriter, SendDataOptions.InOrder);
            }
        }

        void restart()
        {
            if (!isServer && AISwitch.Count > 0)
            {
                //record when player end game
                AISwitch.Add("" + current + "\tPlayerTerminates");
                //output AISwitch array
                string path = @"./PlayerToAI.txt";
                if (!File.Exists(path))
                    File.Create(path).Dispose();
                //file exist
                if (File.Exists(path))
                {
                    //append data  
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter(path, true))
                    {
                        string str;
                        for (int i = 0; i < AISwitch.Count; i++)
                        {
                            str = "" + AISwitch[i];
                            file.WriteLine(str);
                        }
                    }
                }
                //clear array
                AISwitch.Clear();
            }
            //I'm lazy
            //we're not telling client lag
            //either host or client can use this 
            //send messages
            //we just need a new letter
            tellClientLag('f');
        }

        #region XNA-Xbox live service
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
        #endregion

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

        //following method can predict 
        //when to move platform 
        //while ball is flying to platform_remote
        public void doPrediction(statePack state)
        {
            state = invertPosition(state);

            //calc distance from patform to ball
            double db = Math.Pow(state.bPos.X - state.pPos.X, 2)
                + Math.Pow(state.bPos.Y - state.pPos.Y, 2);
            db = Math.Sqrt(db);

            double result;
            //check 3rd model
            result = predict_disToPlatform(db, (int)state.bVel.X, (int)state.bPos.X, (int)state.pPos.X);
            if (Math.Abs(result - state.bPos.Y) <= 3 && !hasPrediCatch)
                hasPrediCatch = true;

            //check 4th model
            result = predict_disToPlatform(db, (int)state.bVel.X, (int)state.bPos.X,
                (int)state.pPos.X, (int)state.wEdg.Y);
            if (Math.Abs(result - state.bPos.Y) <= 3 && !hasPrediCatch)
                hasPrediCatch = true;
            //fail safe
            //sometimes prediction model 
            //cannot give us the result
            //force AI moves the platform
            if (state.bPos.Y >= 550)
                hasPrediCatch = true;
        }


        /*
         * following 2 method 
         * are the prediction models
         */ 
        public double predict_disToPlatform(double dis, int vel, int posx, int platx)
        {
            /* 
             * Adjusted R Square    0.94762887427949
             * std err              27.156267937301
             */
            double y;
            double veld = Math.Pow(vel, 2) + 100;
            veld = Math.Sqrt(veld);
            y = Math.Round(706.3240273 + -0.965004 * dis + 2.2408532 * veld
                + 0.0410435 * posx + 0.1021678 * platx, 3);

            return y;
        }

        /*
         * ball y position when ball hit left or right window bounds (ball.velocity.y > 0 only)
         */
        public double predict_disToPlatform(double dis, int vel, int posx, int platx, int edge)
        {
            /* 
             * Adjusted R Square    0.948060344493643
             * std err              25.9697490413051
             */
            double y;
            y = Math.Round(706.5644181 + -0.9503573 * dis
                + 0.0497304 * posx + 0.1652617 * platx
                + 0.0655089 * windowEdge.Y, 3);

            return y;
        }

        //we first reverse position for com
        //and apply our prediction model
        private void doPrediction_MoveToCenter(statePack state)
        {
            state = invertPosition(state);
            //calc distance first
            double db = Math.Pow(state.bPos.X - state.pPos.X, 2)
                + Math.Pow(state.bPos.Y - state.pPos.Y, 2);
            db = Math.Sqrt(db);
            //calc vel
            double vel = Math.Pow(state.bVel.X, 2) + Math.Pow(state.bVel.Y, 2);
            vel = Math.Sqrt(vel);

            db = db * -0.779862391 + vel * 5.494813831 + state.bPos.X * 0.128871623
                + state.pPos.X * -0.11275278 + 654.4130811;

            if (Math.Abs(db - state.bPos.Y) <= 3 || state.bPos.Y < 400)
            {
                hasPrediCenter = true;
                movePlatformCenter = true;
            }
        }

        //the player profile we use
        //our prediction model is based on 
        //single player mode which player is at bottom
        //thus, we need to reverse position
        //so prediction can be used for 
        //remote platform (at top of screen)
        private statePack invertPosition(statePack state)
        {
            // 0 to 580 screen width
            float screenWidth = ScreenManager.Instance.Dimensions.X;
            float screenHeight = ScreenManager.Instance.Dimensions.Y;
            state.bVel = new Vector2(state.bVel.X * -1, state.bVel.Y * -1);
            state.bPos = new Vector2(screenWidth - state.bPos.X, screenHeight - state.bPos.Y);
            //top platform y = 20, height 25
            //bot platform y = 755
            //thus ball to top platform : ball.y - 45
            //invert position : 755 - ball + 45
            state.pPos = new Vector2(screenWidth - state.pPos.X, screenHeight - state.pPos.Y);
            state.wEdg = new Vector2(screenWidth - state.wEdg.X, screenHeight - state.wEdg.Y);

            return state;
        }
        #endregion

        #region AI

        /* move com platform to hit ball back */
        //this function only work out where ball lands 
        //when moving up to platform_com direction
        //then update a flag "movePlatformCom"

        //in Update() we check the flag
        //if flag is up, we move platform to targetPositionX 
        public void CalcCollisionPosition()
        {
            // hit position at Y 20 + platform.height
            // use current position and velocity to estimate (X, 20 + platform.height)
            Vector2 estPosition = ball.Position;
            //total distance ball needs to move on vertial
            float temp = ball.Position.Y - platform_remote.Position.Y - platform_remote.Dimension.Y;
            //which takes how many updates (vertical speed, 10 per update)
            temp = temp / 10;
            //apply to harizontal, this coordinates is likely outside of windows
            estPosition.X = estPosition.X + ball.Velocity.X * temp + ball.ImageWidth/2;
            estPosition.Y = 20 + platform_remote.Dimension.Y;

            int windowWidth = (int)ScreenManager.Instance.Dimensions.X;
            int windowHeight = (int)ScreenManager.Instance.Dimensions.Y;

            //search for estimate postion 
            while (true)
            {
                if (estPosition.X >= 0 && estPosition.X <= windowWidth) //we got a collision point in window
                {
                    targetPositionX = estPosition.X;
                    //we have worked out a position
                    //now request to move
                    //flag up 
                    movePlatformRemote = true;
                    break;
                }
                else if (estPosition.X > windowWidth) //ball hits right windows border
                /*
                 * note 
                 * casuing imperfect 
                 * waiting to be further investigated 
                 * 
                 * ## has a temporal fix
                 */
                {
                    //esti pos - (distance to hit right window)
                    //distance left after ball hit right window border
                    //reverse direction
                    //estPosition.X = windowWidth - ball.Origin.X - (estPosition.X - (windowWidth - ball.Origin.X - ball.Position.X));

                    /*  
                     * explain of subtract 100 in fomular below
                     * I currently have no idea why the estimated position is worng
                     * but it appears everytime the ball hits right window border
                     * my estimation position shifts to right by 100 from real position
                     */

                    estPosition.X = windowWidth - (estPosition.X - windowWidth);
                    //estPosition.X = windowWidth - ball.Origin.X - estPosition.X + windowWidth - ball.Origin.X - ball.Position.X;
                }
                else if (estPosition.X < 0)
                {
                    //total distance - distance to hit window bounds
                    //ball.Position.X - estPosition.X - ball.Position.X + ball.Origin.X
                    //turn remaining distance to positive number
                    estPosition.X = Math.Abs(estPosition.X);
                }
            }
        }

        //move com platform to where ball lands on top of screen
        //this method serves both move to right positio and move to wrong position
        //which uses targetPositionX and targetWrongX
        //boolean controller are
        //bool movePlatformCom and bool moveComPlatformWrong
        //send flag 1 for movePlatformCom
        //send flag 2 for moveComPlatfromWrong
        private void MoveRemotePlatform(float target, int flag)
        {
            //we are at right position, stop moving
            if (target >= platform_remote.Position.X + platform_remote.Dimension.X / 5 * 2 && target <= platform_remote.Position.X + platform_remote.Dimension.X / 5 * 3)
            {
                switch (flag)
                {
                    case 1:
                        movePlatformRemote = false;
                        break;
                    case 2:
                        moveRemoteWrong = false;
                        break;
                    case 3:
                        movePlatformCenter = false;
                        break;
                }
            }
            else if (target < platform_remote.Position.X + platform_remote.Dimension.X / 5 * 2)
                platform_remote.Position = new Vector2(platform_remote.Position.X - platform_remote.MoveSpeed, platform_remote.Position.Y);
            else if (target > platform_remote.Position.X + platform_remote.Dimension.X / 5 * 3)
                platform_remote.Position = new Vector2(platform_remote.Position.X + platform_remote.MoveSpeed, platform_remote.Position.Y);
        }

        //when this method is called
        //we have already work out the right targetPositionX
        //we now need to move platform_com to wrong direction
        //then move it to targetPositionX
        //So it can miss the ball

        //two ways
        //make platform stop at a wrong position for sometime
        //or
        //make platform keep moving to wrong direction
        //until there is not enough time for it gets to right position
        //
        //note the 2nd way
        //that position may be outside of our screen

        //so..let's get this done

        //this method is called in extreme hard mode
        //every time when com platform receives movePlatformCom flag
        //AI first calculates a worng position in wrong direction
        //if com platform successfully reaches this wrong position
        //then it will miss ball on its way back to right collision position

        //during moving to wrong position
        //in every update we generate a random number
        //if the number is in range, we keep moving
        //otherwise we abort moving to wrong position
        //and start moving to the right collision position
        private void CalcRemoteWrongPosition()
        {
            //work out how much distance we need to move
            //from current position to collision position
            float trueDistance = 0;
            if (targetPositionX > platform_remote.Position.X + platform_remote.Dimension.X / 5 * 3)
                trueDistance = targetPositionX - platform_remote.Position.X - platform_remote.Dimension.X / 5 * 3;
            else if (targetPositionX < platform_remote.Position.X + platform_remote.Dimension.X / 5 * 2)
                trueDistance = platform_remote.Position.X + platform_remote.Dimension.X / 5 * 2 - targetPositionX;
            //convert distance to how many updates 
            //platform move speed is 10f
            int tmp = (int)Math.Ceiling(trueDistance / 10);
            //figure out how many updates are there before collision
            /*
             * MSDN exmaple of Math.Ceiling(double)
             * The example displays the following output to the console: 
             * Value          Ceiling          Floor 
             *  7.03                8              7 
             *  7.64                8              7 
             *  0.12                1              0 
             *  -7.1               -7             -8 
             *  
             * why it returns a double? hmm..
             */
            //work out how many updates before collision
            moveWrongTimer = (int)Math.Ceiling((ball.Position.Y - 45) / 10);
            moveWrongTimer -= tmp;
            //havlve remaining timer, 
            //that is the distance we are going to move
            //to wrong direction

            moveWrongTimer /= 2;
           
            //see collision detection
            //i believe due to 
            //CD is not well written, we need extra 190 distance
            //to ensure platform will miss the ball
            //but it appears this offset has 
            //impact on AI
            //which causes it catches ball for few rounds
            //and often misses ball at round 2, 7 and 11

            //temporal fix
            moveWrongTimer += rnd.Next(17, 20);

            //move to a wrong position
            if (targetPositionX > platform_remote.Position.X + platform_remote.Dimension.X / 5 * 3)
            {
                //move to left (wrong direction)
                targetWrongX = platform_remote.Position.X + platform_remote.Dimension.X / 5 * 2
                    - moveWrongTimer * 10;
                if (targetWrongX < platform_remote.Dimension.X / 5 * 2)
                    targetWrongX = platform_remote.Dimension.X / 5 * 2;
            }
            else if (targetPositionX < platform_remote.Position.X + platform_remote.Dimension.X / 5 * 2)
            {
                //move to right (wrong direction)
                targetWrongX = platform_remote.Position.X + platform_remote.Dimension.X / 5 * 3
                    + moveWrongTimer * 10;
                if (targetWrongX > ScreenManager.Instance.Dimensions.X
                    - platform_remote.Dimension.X / 5 * 2)
                    targetWrongX = ScreenManager.Instance.Dimensions.X
                        - platform_remote.Dimension.X / 5 * 2;
            }
            moveRemoteWrong = true;
        }


        #endregion

        /************************************/
        /**********test functions************/
        /***********delete later*************/
        /************************************/
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