using System;
using System.Collections.Generic;
using System.Collections;
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
    class DataCollector : GameScreen
    {
        #region Variables

        Platform platform_player;
        Platform platform_com;

        Ball ball;

        SpriteFont font;

        bool start;
        bool end;
        //AI
        float targetPositionX;
        bool movePlatformCom;
            
        /* statistics */
        //if platform_player is moving
        bool platformMoving;
        TimeSpan startTime;

        //three arraylists
        //store : GameTime, InfoName, data
        TimeSpan current;

        ArrayList timeTag;
        ArrayList info;
        ArrayList data;

        //how many times ball hits player platform 
        int rounds;
        bool dataCollect;

        /* speed change */
        int accelCounter;

        /* test prediction */
        double show_prediction_1st = 0;
        double show_prediction_2nd = 0;
        double show_prediction_3rd = 0;
        double show_prediction_4th = 0;
        /* ball Y coor when paltform moves */
        double move_y = 0;
        //record prediction and real position
        bool hasPrediction_1;
        bool hasPrediction_2;
        bool hasPrediction_3;
        bool hasPrediction_4;

        ArrayList predictList_1st;
        ArrayList predictList_2nd;
        ArrayList predictList_3rd;
        ArrayList predictList_4th;
        //when ball moving downwards, if hit window bounds
        Vector2 windowEdge;
        //only record platform once
        bool platformHasMoved = false;

        //AI Patch
        enum Diffculty { Hard, VeryHard, ExtremeHard}
        Diffculty level; 

        //wrong position in AI patch
        //move to wrong pos first
        //then move to targetPositionX
        float targetWrongX;
        bool moveComPlatformWrong;
        //sometimes com platform needs to wait
        //for a short time than move to right position
        //so it can miss the ball
        bool noWait;
        #endregion

        #region XNA functions

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);

            if (font == null)
                font = content.Load<SpriteFont>("Font1");

            //load assets
            platform_player = new Platform();
            platform_player.LoadContent(Content, inputManager);

            platform_com = new Platform();
            platform_com.LoadContent(Content, inputManager);
            platform_com.Position = new Vector2(ScreenManager.Instance.Dimensions.X/2 - platform_com.Dimension.X / 2, 20);
            platform_com.ControlByPlayer = false;

            ball = new Ball();
            ball.LoadContent(Content, inputManager);
            //inital AI
            start = false;
            end = false;

            targetPositionX = 0f;
            movePlatformCom = false;
            //inital statistics collection
            
            platformMoving = false;
            rounds = 0;
            dataCollect = false;

            timeTag = new ArrayList();
            info = new ArrayList();
            data = new ArrayList();

            accelCounter = 0;

            hasPrediction_1 = false;
            hasPrediction_2 = false;
            hasPrediction_3 = false;
            hasPrediction_4 = false;
            predictList_1st = new ArrayList();
            predictList_2nd = new ArrayList();
            predictList_3rd = new ArrayList();
            predictList_4th = new ArrayList();
            windowEdge = new Vector2(0, 0);

            level = Diffculty.Hard;
            targetWrongX = 0f;
            moveComPlatformWrong = false;
            noWait = true;
        }

        public override void UnloadContent()
        {
            base.UnloadContent();
            platform_com.UnloadContent();
            platform_player.UnloadContent();
            ball.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {
            //current game time, used in data collection
            if (start)
                current = gameTime.TotalGameTime - startTime;
            inputManager.Update();

            if (!start)
            { 
                /* prevent ball moving before start */
                ball.Velocity = new Vector2(0, 0);

                //start state and re-start state
                if (inputManager.KeyPressed(Keys.Space))
                {
                    if (!start && !end)
                    {
                        /* start game */
                        start = true;
                        startTime = gameTime.TotalGameTime;
                        ball.Velocity = new Vector2(-5, 10);
                    }
                    else if (!start && end)
                    {
                        //reload game screen to restart
                        Type newClass = Type.GetType("XNAServerClient.DataCollector");
                        ScreenManager.Instance.AddScreen((GameScreen)Activator.CreateInstance(newClass), inputManager);
                    }
                }
            }

            //ball acceleration
            //update ball speed based on hits
            //ball speeds up every 4 hits
            if (accelCounter == 3)
            {
                if (ball.Velocity.X > 0)
                    ball.Velocity = new Vector2(ball.Velocity.X + 2, ball.Velocity.Y);
                else
                    ball.Velocity = new Vector2(ball.Velocity.X - 2, ball.Velocity.Y);
                accelCounter = 0;
                //after we accelerate the ball
                //we need to check where ball lands again
                if (ball.Velocity.Y < 0)
                    CalcComPlatformPosition();
            }

            //check collision below

            Rectangle platformRect_player = platform_player.Rectangle;
            Color[] platformColor_player = platform_player.ColorData;
            Rectangle ballRect = ball.Rectangle;
            Color[] ballColor = ball.ColorData;
            //ball collade with plaform_player
            if (ballRect.Intersects(platformRect_player))
            {
                //check pixel collision
                if (UpdateCollision(ballRect, ballColor, platformRect_player, platformColor_player))
                {
                    accelCounter++;

                    //when collade, collect prediction and actual data
                    //predictList.Add("#########");
                    predictList_1st.Add("prediction\t" + show_prediction_1st + "\treal\t" + move_y);
                    predictList_2nd.Add("prediction\t" + show_prediction_2nd + "\treal\t" + move_y);
                    predictList_3rd.Add("prediction\t" + show_prediction_3rd + "\treal\t" + move_y);
                    predictList_4th.Add("prediction\t" + show_prediction_4th + "\treal\t" + move_y);
                    
                    //if ball center is higher than platform, ball's velocity Y is negative 
                    //if ball center is lower than platform, ball's velocity Y is positive
                    //these two param used for statistics and collision 
                    Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
                    Vector2 platformOrigin_player = new Vector2(platform_player.Origin.X + platformRect_player.X, platform_player.Origin.Y + platformRect_player.Y);

                    /* 
                     * 
                     * before collision change ball state
                     * we need to collect some data
                     * 
                     * ball center position and platform center position
                     * above two reveal which part of platform player tends to hit ball (center or close to edge)
                     * 
                     * ball velocity
                     * if we can fix AI, we can apply different speed to ball, see how player performs 
                     * 
                     */
                    //current = gameTime.TotalGameTime - startTime;
                    //ball origin, player platform origin, ball velocity
                    timeTag.Add(current);
                    if (ball.Velocity.X == 0 && ball.Velocity.Y == 0)
                        info.Add("GameStart");
                    else
                        info.Add("Collision");
                    data.Add("BallOrigin\t" + ballOrigin.X + "," + ballOrigin.Y
                        +"\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y
                        +"\tPlatOrigin\t" + platformOrigin_player.X + "," + platformOrigin_player.Y
                        +"\tPlatVel\t" + platform_player.Velocity.X + "," + platform_player.Velocity.Y);
                    //update how many rounds we have played (1 player catch + 1 com catch = 1 round) 
                    rounds++;

                    //see how collision changes ball state
                    //platform height is 25, so check -12 to 12 around origin.y
                    if ((ballOrigin.Y - platformOrigin_player.Y) <= -12)
                    {
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y) * -1);
                        //Console.WriteLine("Collading #2 " + gameTime.TotalGameTime);

                        //calculate ball collision position for com
                        CalcComPlatformPosition();
                        if (level == Diffculty.ExtremeHard)
                            CalcComWrongPosition();
                        
                    }
                    else if ((ballOrigin.Y - platformOrigin_player.Y) >= 12)
                    {
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                    }
                    else if (Math.Abs(ballOrigin.Y - platformOrigin_player.Y) < 12)
                    {
                        ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);
                    }
                }
            }

            Rectangle platformRect_com = platform_com.Rectangle;
            Color[] platformColor_com = platform_com.ColorData;
            //ball collade with plaform com
            if (ballRect.Intersects(platformRect_com))
            {
                //check pixel collision
                if (UpdateCollision(ballRect, ballColor, platformRect_com, platformColor_com))
                {
                    //we have collected prediction data when ball collide with player platform
                    //now for next round of prediction, we need to reset everything
                    //reset prediction state
                    hasPrediction_1 = false;
                    hasPrediction_2 = false;
                    hasPrediction_3 = false;
                    hasPrediction_4 = false;
                    show_prediction_1st = 0;
                    show_prediction_2nd = 0;
                    show_prediction_3rd = 0;
                    show_prediction_4th = 0;
                    //move_y is observed position when player moved platform
                    move_y = 0;
                    //last time ball reached window edge/border
                    windowEdge = new Vector2(0, 0);
                    platformHasMoved = false;

                    //if ball center is higher than platform, ball's velocity Y is negative 
                    //if ball center is lower than platform, ball's velocity Y is positive
                    Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
                    Vector2 platformOrigin_com = new Vector2(platform_com.Origin.X + platformRect_com.X, platform_com.Origin.Y + platformRect_com.Y);

                    //platform height is 25, so check -12 to 12 around origin.y
                    if ((ballOrigin.Y - platformOrigin_com.Y) <= -12)
                    {
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y) * -1);
                    }
                    else if ((ballOrigin.Y - platformOrigin_com.Y) >= 12)
                    {
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                        //Console.WriteLine("Collading #1 " + gameTime.TotalGameTime);
                    }
                    else if (Math.Abs(ballOrigin.Y - platformOrigin_com.Y) < 12)
                    {
                        ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);
                    }

                    //if platform is moving while collade, add extra speed to ball
                    //if (platform_com.Velocity.X > 0)
                    //{
                    //    if (ball.Velocity.X > 0 || ball.Velocity.X < -5)
                    //        ball.Velocity = new Vector2(ball.Velocity.X + 5, ball.Velocity.Y);
                    //}
                    //else if (platform_com.Velocity.X < 0)
                    //{
                    //    if (ball.Velocity.X > 5 || ball.Velocity.X < 0)
                    //        ball.Velocity = new Vector2(ball.Velocity.X - 5, ball.Velocity.Y);
                    //}
                }
            }

            

            // AI 
            //move platform com when required

            //per Shen's request
            //we need to make AI sometimes misses ball
            //so we can investigate that
            //can a player realize who he/she is playing with
            //an computer AI or our predition models or real player

            //two ways to do this
            //limit start timing
            /*
             * we limit com platform start timing 
             * based on ball's current vertical postion
             * 
             * eg. com platform starts to move
             * while ball.pos.y > 200 and y < 300
             */ 


            //random hit/miss rate
            /*
             * generate a random number first
             * eg. Random(0,1)
             * if rnd > 0.5, next round hits
             * if rnd <= 0.5, next round misses
             * 
             * if miss, we calculate how many update 
             * platform_com needs to move to the right position
             * 
             * force platform wait until it is too late
             * then move the platform
             * 
             * OR
             * intentionally move platform to the other direction
             * then move back
             */ 

            //extrem hard: move to wrong direction then move back
            //very hard: move platform when it is too late
            //hard: just move it to right position
            if (movePlatformCom)
            {
                switch (level)
                {
                    case Diffculty.ExtremeHard:     
                        //move to the wrong position first
                        //then move to right position
                        if (moveComPlatformWrong)
                        {
                            //if we just let it do 
                            //MoveComPlatform(targetWrongX, 2);
                            //AI definately gonna miss the ball
                            //so we can give it a random
                            //directly set !moveComPlatformWrong
                            //then it moves back to right position
                            //while it was on its way to wrong position
                            //and still catches the ball

                            //if all random results do 
                            //MoveComPlatform(targetWrongX, 2);
                            //then miss the ball
                            //which is P^n
                            Random rnd = new Random();
                            //get a number between 1 to 10
                            int num = rnd.Next(1, 11);

                            //this if condition controls 
                            //how often AI can catches the ball
                            //change it as you want
                            //num < 6 is 50%
                            if (num < 6)
                                MoveComPlatform(targetWrongX, 2);
                            else
                            {
                                //go to right position
                                moveComPlatformWrong = false;
                                noWait = true;
                            }
                        }
                        else if (!moveComPlatformWrong && !noWait)
                        { 
                            //platform is already at left or right window edge
                            //but if we move back now, we still can catch the ball
                            //for targetWrongX is outside of screen
                            //thus we wait, keep checking ball position 
                            //and remaining distance between ball.x and right pos
                            //set noWait = true 
                            AICheckWaiting();
                        }
                        else if (!moveComPlatformWrong && noWait)
                        {
                            MoveComPlatform(targetPositionX, 1);
                        }

                        break;
                    case Diffculty.VeryHard:
                        

                        break;
                    case Diffculty.Hard:
                        MoveComPlatform(targetPositionX, 1);

                        break;
                }
            }

            //prediction record
            //check if ball hits window bounds
            if (ball.Position.X <= 0
                || ball.Position.X + ball.ImageWidth >= ScreenManager.Instance.Dimensions.X)
            {
                timeTag.Add(current);
                info.Add("WindowEdge");
                data.Add("BallPos\t" + ball.Position.X + "," + ball.Position.Y
                    + "\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y
                    + "\tPlaXCoor\t" + platform_player.Position.X
                    + "\tPlatVel\t" + platform_player.Velocity.X + "," + platform_player.Velocity.Y);
                if (ball.Velocity.Y > 0)
                {
                    windowEdge = ball.Position;
                }
            }

            base.Update(gameTime);
            ball.Update(gameTime);
            platform_com.Update(gameTime);
            platform_player.Update(gameTime);

            /* show estimated start move ball y position on screen */
            doPrediction();
            
            /* check game end condition */
            //if part of ball image is below screen, then game end

            if (ball.Position.Y + ball.ImageHeight > ScreenManager.Instance.Dimensions.Y
                || ball.Position.Y < 0)
            {
                //pop in end game state
                //current = gameTime.TotalGameTime - startTime; ;
                timeTag.Add(current);
                info.Add("GameEnd");
                data.Add("BallPos\t" + ball.Position.X + "," + ball.Position.Y
                    + "\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y
                    +"\tPlatCoor\t" + platform_player.Position.X + "," + platform_player.Position.Y
                    +"\tPlatVel\t" + platform_player.Velocity.X + "," + platform_player.Velocity.Y
                    + "\tRounds\t" + (rounds-1) );
                /// <note>
                /// round - 1
                /// somehow, program goes through collision code(where round++)
                /// before I start game, after screen loading
                /// </note> 



                //clear some states
                start = false;
                end = true;
                
                //freeze game state
                ball.Velocity = new Vector2(0, 0);
                platform_player.ControlByPlayer = false;

                if (!dataCollect)
                {
                    dataCollect = true;
                    //serilize data
                    //output path - file name
                    //see bin/x86/debug/
                    string path = @".\Data.txt";
                    string predictionPath_1 = @"./Prediction_1st.txt";
                    string predictionPath_2 = @"./Prediction_2nd.txt";
                    string predictionPath_3 = @"./Prediction_3rd.txt";
                    string predictionPath_4 = @"./Prediction_4th.txt";
                    //check file existance
                    if (!File.Exists(path))
                        File.Create(path).Dispose();
                    if (!File.Exists(predictionPath_1))
                    {
                        File.Create(predictionPath_1).Dispose();
                        File.Create(predictionPath_2).Dispose();
                        File.Create(predictionPath_3).Dispose();
                        File.Create(predictionPath_4).Dispose();
                    }
                    //file exist
                    if (File.Exists(path))
                    {
                        //append data  
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(path, true))
                        {
                            string str;
                            for (int i = 0; i < timeTag.Count; i++)
                            {
                                str = timeTag[i] + "\t" + info[i] + "\t" + data[i];
                                file.WriteLine(str);
                            }
                        }
                    }

                    //write predictions to file
                    //1st
                    if (File.Exists(predictionPath_1))
                    {
                        //append data  
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(predictionPath_1, true))
                        {
                            string str;
                            for (int i = 0; i < predictList_1st.Count; i++)
                            {
                                str = "" + predictList_1st[i];
                                file.WriteLine(str);
                            }
                        }
                    }
                    //2nd
                    if (File.Exists(predictionPath_2))
                    {
                        //append data  
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(predictionPath_2, true))
                        {
                            string str;
                            for (int i = 0; i < predictList_2nd.Count; i++)
                            {
                                str = "" + predictList_2nd[i];
                                file.WriteLine(str);
                            }
                        }
                    }
                    //3rd
                    if (File.Exists(predictionPath_3))
                    {
                        //append data  
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(predictionPath_3, true))
                        {
                            string str;
                            for (int i = 0; i < predictList_3rd.Count; i++)
                            {
                                str = "" + predictList_3rd[i];
                                file.WriteLine(str);
                            }
                        }
                    }
                    //4th
                    if (File.Exists(predictionPath_4))
                    {
                        //append data  
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(predictionPath_4, true))
                        {
                            string str;
                            for (int i = 0; i < predictList_4th.Count; i++)
                            {
                                str = "" + predictList_4th[i];
                                file.WriteLine(str);
                            }
                        }
                    }

                }
            }

            //collecting platform status

            /* no key is pressed, platform is not moving */

            /// <attention>
            ///
            /// Following part is written in fixed value
            /// change ball velocity if we tend to move ball
            /// to any directions
            /// 
            /// </attention>
            if (inputManager.KeyUp(Keys.Left) && inputManager.KeyUp(Keys.Right))
            {
                if (platformMoving)
                {
                    //update some states when stop move
                    platformMoving = false;
                    platform_player.Velocity = new Vector2(0, 0);
                    
                    //collect some data, when platform stop
                    //where the ball is, where the ball is moving to
                    //where platform stoped (platform current position)
                    //current = gameTime.TotalGameTime - startTime; ;
                    timeTag.Add(current);
                    info.Add("PlatformStop");
                    data.Add("BallPos\t" + ball.Position.X + "," + ball.Position.Y
                        +"\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y
                        +"\tPlaXCoor\t" + platform_player.Position.X
                        +"\tPlatVel\t" + platform_player.Velocity.X + "," + platform_player.Velocity.Y);
                }
            }
            else
            //we excludes the case that both keys are pressed at the same time
            //I'm the testee anyway
            {
                //platform wasn't moving in last update()
                //now it starts moving
                if (!platformMoving)
                {
                    //update state
                    platformMoving = true;
                    //work out velocity
                    if (inputManager.KeyDown(Keys.Left) && inputManager.KeyDown(Keys.Right))
                        platform_player.Velocity = new Vector2(0, 0);
                    else if (inputManager.KeyDown(Keys.Left) && inputManager.KeyUp(Keys.Right))
                        platform_player.Velocity = new Vector2(-10, 0);
                    else if (inputManager.KeyDown(Keys.Right) && inputManager.KeyUp(Keys.Left))
                        platform_player.Velocity = new Vector2(10, 0);

                    //collect some data, when player starts to move platform
                    //where the ball is, where the ball is heading to
                    //ball speed, platform pos
                    //current = gameTime.TotalGameTime - startTime; ;
                    timeTag.Add(current);
                    info.Add("PlatformMove");
                    data.Add("BallPos\t" + ball.Position.X + "," + ball.Position.Y
                        +"\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y
                        +"\tPlaXCoor\t" + platform_player.Position.X
                        +"\tPlatVel\t" + platform_player.Velocity.X + "," + platform_player.Velocity.Y);
                    //store Y position to display on screen later
                    //this is the actual Y coor when paltform moves
                    if (ball.Velocity.Y > 0 && !platformHasMoved)
                    {
                        move_y = ball.Position.Y;
                        platformHasMoved = true;
                    }
                }
            }

        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            spriteBatch.DrawString(font, "pred_3: " + show_prediction_3rd, new Vector2(20, 770), Color.Red);
            spriteBatch.DrawString(font, "pred_4: " + show_prediction_4th, new Vector2(20,750),Color.Red);
            spriteBatch.DrawString(font, "real: " + move_y, new Vector2(20, 730), Color.Red);

            ball.Draw(spriteBatch);
            platform_player.Draw(spriteBatch);
            platform_com.Draw(spriteBatch);

            if (!start && !end)
                spriteBatch.DrawString(font, "Press Space to Start..",
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Space to Start..").X/2, 
                        ScreenManager.Instance.Dimensions.Y/2), 
                    Color.White);

            if (!start && end)
                spriteBatch.DrawString(font, "Press Space to Re-Start..",
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Space to Re-Start..").X / 2,
                        ScreenManager.Instance.Dimensions.Y / 2),
                    Color.White);
        }

        #endregion

        #region private functions

        // check each pixel on two texture, looking for overlap
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

        #region AI
        /* move com platform to hit ball back */

        //this function only work out where ball lands 
        //when moving up to platform_com direction
        //then update a flag "movePlatformCom"

        //in Update() we check the flag
        //if flag is up, we move platform to targetPositionX 
        public void CalcComPlatformPosition()
        { 
            // hit position at Y 20 + platform.height
            // use current position and velocity to estimate (X, 20 + platform.height)
            Vector2 estPosition = ball.Position;
            //total distance ball needs to move on vertial
            float temp = ball.Position.Y - 20 - platform_com.Dimension.Y;
            //which takes how many updates (vertical speed, 10 per update)
            temp = temp / 10;
            //apply to harizontal, this coordinates is likely outside of windows
            estPosition.X = estPosition.X + ball.Velocity.X * temp;
            estPosition.Y = 20 + platform_com.Dimension.Y;

            int windowWidth = (int)ScreenManager.Instance.Dimensions.X;
            int windowHeight = (int)ScreenManager.Instance.Dimensions.Y;

            //search for estimate postion 
            while (true)
            {
                if (estPosition.X >= 0 && estPosition.X <= windowWidth ) //we got a collision point in window
                {
                    targetPositionX = estPosition.X;
                    //we have worked out a position
                    //now require to move
                    movePlatformCom = true;
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

                    estPosition.X = windowWidth + windowWidth - estPosition.X - 100;
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
        private void MoveComPlatform(float target, int flag)
        {
            //we are at right position, stop moving
            if (target >= platform_com.Position.X + platform_com.Dimension.X / 5 * 2 && target <= platform_com.Position.X + platform_com.Dimension.X / 5 * 3)
            {
                if (flag == 1)
                    movePlatformCom = false;
                else if (flag == 2)
                    moveComPlatformWrong = false;
            }
            else if (target < platform_com.Position.X + platform_com.Dimension.X / 5 * 2)
                platform_com.Position = new Vector2(platform_com.Position.X - platform_com.MoveSpeed, platform_com.Position.Y);
            else if (target > platform_com.Position.X + platform_com.Dimension.X / 5 * 3)
                platform_com.Position = new Vector2(platform_com.Position.X + platform_com.MoveSpeed, platform_com.Position.Y);
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
        private void CalcComWrongPosition()
        {
            //this is the distance we need to move to the right position
            float trueDistance = Math.Abs(targetPositionX - platform_com.Position.X);
            
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
            int updates = (int)Math.Ceiling((ball.Position.Y - 45) / 10);
            //so before collision, if we keeping moving
            //we then at least need to move a distance of 
            //updates * 10 - trueDistance (platform speed is 10f)
            //therefore the wrong position we need to move to is 
            //half of that distance: (updated * 10 - trueDistance) / 2
            //move to wrong pos + move back to where we are now + trueDistance
            //
            //varibles was casted, in case there was like 5.99, 5.98 
            //ceiling it to 6 which is not much different before we cast it
            //then platform may still be able to collided with ball
            //under some extreme conditions (CPU schedualing, threading and so on)
            //let's make it: (updates + 1) * 10 - trueDistance
            float targetWrongDistance = ((updates + 1) * 10 - trueDistance) / 2; 

            //work out which direction we move
            //just in case, give it >=, 1 at 580 possibility
            if (targetPositionX >= platform_com.Position.X)
            {
                //move to left (wrong direction)
                
                //check if the target position is outside of screen
                //to be safe, we check 0 + platform_com.Dimension.X/2
                //make sure MoveComPlatform() is able to move to that position
                if (platform_com.Position.X - targetWrongDistance < platform_com.Dimension.X / 2)
                { 
                    //we can simply set wrong position to 0
                    //check how many updates before collision
                    //and distance to right position
                    //in every Update()
                    //a little bit rigid

                    //cannot just use MoveComPlatform(target, flag)
                    //because it set moveComPlatformWrong to flase when at target
                    //then in Update() platform will move to right position

                    //unfortunately this method only called when collision happens
                    //we need to monitor remaining distance from ball to platform
                    //so that we know how long to wait at window edge
                    //flag up noWait
                    //in Update() check this flag
                    //set noWait to false to make platform wait and check 
                    noWait = false;
                    //see MoveComPlatform()
                    //that method keep moving platform 
                    //until target position is between 
                    //platform.x + 0.4 platformWidth and platform.x + 0.6 platformWidrth
                    //to be safe, we wait at 0 + 0.5 platformWidth
                    targetWrongX = 0 + platform_com.Dimension.X / 2;
                    moveComPlatformWrong = true;
                }
                else
                { 
                    //we can just move it
                    targetWrongX = platform_com.Position.X - targetWrongDistance;
                    moveComPlatformWrong = true;
                    //we don't need to put platform to wait
                    noWait = true;
                }
            }
            else if (targetPositionX < platform_com.Position.X)
            { 
                //move to right (wrong direction)

                //check if the target position is outside of screen
            }

            
        }

        //sometimes targetWrongX is outside of screen
        //so we need to make platform wait at position 0 or screenWidth-platformWidth
        //this method is called when at window edge 
        //keep check remaining updates before collision
        //set noWait to true when it is too late to catch the ball
        private void AICheckWaiting()
        {
            //platform_com position is at 20, and its height is 25
            //therefore lower side is 45
            //we calculates ball's Y and 45 
            //ball moves 10 per updates on vertical
            //use that to see how long we can move the platform
            //platform movespeed is also 10f
            
            //code should look like
            //remaining updates
            //float updates = (ball.Position.Y - 45) / 10;
            //distance platform can move
            //float distance = updates * 10;

            //shoule be 45 in if condition
            //to be safe, give it one more update interval
            float moveToRightDistance = Math.Abs(targetPositionX - platform_com.Position.X);
            if (moveToRightDistance > ball.Position.Y - 55)
                noWait = true;
        }
        #endregion

        #endregion

        #region Predictions

        public void doPrediction()
        {
            if (ball.Velocity.Y > 0)
            {
                /* calc distance between ball and palt, used in prediction */
                double db = Math.Pow(ball.Position.X - platform_player.Position.X, 2)
                    + Math.Pow(ball.Position.Y - platform_player.Position.Y, 2);
                db = Math.Sqrt(db);

                /* O colume in excel*/
                //db = db / Math.Abs(ball.Velocity.X);
                /* new start excel T col */
                //double updatetimes = (700 - ball.Position.Y) / 10;
                //updatetimes = updatetimes * ball.Velocity.X + ball.Position.X;
                //double db = Math.Pow(ball.Position.X - updatetimes, 2) + Math.Pow(ball.Position.Y - 700, 2);
                //db = Math.Sqrt(db);

                //take in singel independent variable
                db = predict_disToPlatform(db);

                if (db > 100 && db < 550 && Math.Abs(ball.Position.Y - db) <= 3 && !hasPrediction_1)
                {
                    show_prediction_1st = db;
                    hasPrediction_1 = true;
                }

                /*
                 * take in 2 variables
                 * distance between ball and platform 
                 * ball x speed
                 */
                db = predict_disToPlatform(db, (int)ball.Velocity.X);

                if (db > 100 && db < 550 && Math.Abs(ball.Position.Y - db) <= 3 && !hasPrediction_2)
                {
                    show_prediction_2nd = db;
                    hasPrediction_2 = true;
                }

                /*
                 * fourh param platfomr x position 
                 */
                db = predict_disToPlatform(db, (int)ball.Velocity.X, (int)ball.Position.X, (int)platform_player.Position.X);
                
                if (db > 100 && db < 550 && Math.Abs(ball.Position.Y - db) <= 3 && !hasPrediction_3)
                {
                    show_prediction_3rd = db;
                    hasPrediction_3 = true;
                }

                /*
                 * if ball hit window edge once when going down
                 */
                if (windowEdge.Y != 0)
                    db = predict_disToPlatform(db, (int)ball.Velocity.X,
                        (int)ball.Position.X, (int)platform_player.Position.X, (int)windowEdge.X);
                
                if (db > 100 && db < 550 && Math.Abs(ball.Position.Y - db) <= 3 && !hasPrediction_4)
                {
                    show_prediction_4th = db;
                    hasPrediction_4 = true;
                }
            }
        }

        public double predict_disToPlatform(double dis) 
        {
            //distance ball to platform vs ball y postion

            /* 
             * R Square     0.940077006273422
             * std err      29.1367380609093
             */
            double y = Math.Round(-0.989707185 * dis + 783.8653914, 3);

            return y;
        }

        /* 
         * double   distance between ball and platform
         * int      ball x volecity
         */
        public double predict_disToPlatform(double dis, int vel)
        {
            /* 
             * Adjusted R Square    0.943112200851591
             * std err              28.3030798445966
             */
            double y;
            //if concidering y velocity
            // y velocity is fixed, 10 per updates
            // thus y^2 = 100 
            double veld = Math.Pow(vel, 2) + 100;
            veld = Math.Sqrt(veld);
            y = Math.Round(728.9963297 + -0.9613464 * dis + 2.6610659 * veld, 3);
            return y;
        }
        

        /*
         * int posx     ball x position
         * int platx    platform x position
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
                + 0.0655089 * windowEdge.X, 3);

            return y;
        }

        #endregion
    }
}
