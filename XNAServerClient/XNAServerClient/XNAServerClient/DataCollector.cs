﻿using System;
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
        
        bool platformMoving;

        //three arraylists
        //store : GameTime, InfoName, data
        TimeSpan current;

        ArrayList timeTag;
        ArrayList info;
        ArrayList data;

        //how many times ball hits player platform 
        int rounds;
        bool dataCollect;

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

            targetPositionX = 0;
            movePlatformCom = false;
            //inital statistics collection
            
            platformMoving = false;
            rounds = 0;
            dataCollect = false;

            timeTag = new ArrayList();
            info = new ArrayList();
            data = new ArrayList();

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
            inputManager.Update();

            if (!start)
            { 
                /* prevent ball moving before start */
                ball.Velocity = new Vector2(0, 0);
                if (inputManager.KeyPressed(Keys.Space))
                {
                    if (!start && !end)
                    {
                        /* start game */
                        start = true;
                        ball.Velocity = new Vector2(-7, 10);
                    }
                    else if (!start && end)
                    {
                        Type newClass = Type.GetType("XNAServerClient.DataCollector");
                        ScreenManager.Instance.AddScreen((GameScreen)Activator.CreateInstance(newClass), inputManager);
                    }
                }
            }


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

                    ////if platform is moving while collade, add extra speed to ball
                    //if (platform_player.Velocity.X > 0)
                    //{
                    //    if (ball.Velocity.X > 0 || ball.Velocity.X < -5)
                    //        ball.Velocity = new Vector2(ball.Velocity.X + 5, ball.Velocity.Y);
                    //    /* I can only catch 22 */
                    //    /* so max speed 22 */
                    //    if (ball.Velocity.X > 22)
                    //        ball.Velocity = new Vector2(22, ball.Velocity.Y);
                    //}
                    //else if (platform_player.Velocity.X < 0)
                    //{
                    //    if (ball.Velocity.X > 5 || ball.Velocity.X < 0)
                    //        ball.Velocity = new Vector2(ball.Velocity.X - 5, ball.Velocity.Y);
                    //    /* I can only catch 22 */
                    //    /* so max speed 22 */
                    //    if (ball.Velocity.X < -22)
                    //        ball.Velocity = new Vector2(-22, ball.Velocity.Y);
                    //}

                    

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
                    current = gameTime.TotalGameTime;
                    //ball origin, player platform origin, ball velocity
                    timeTag.Add(current);
                    if (ball.Velocity.X == 0 && ball.Velocity.Y == 0)
                        info.Add("GameStart");
                    else
                        info.Add("Collision");
                    data.Add("BallOrigin\t" + ballOrigin.X + "," + ballOrigin.Y + "\tPlatOrigin\t"
                            + platformOrigin_player.X + "," + platformOrigin_player.Y + "\tBallVel\t"
                            + ball.Velocity.X + "," + ball.Velocity.Y);
                    rounds++;
                    //see how collision change ball state
                    //platform height is 25, so check -12 to 12 around origin.y
                    if ((ballOrigin.Y - platformOrigin_player.Y) <= -12)
                    {
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y) * -1);
                        //Console.WriteLine("Collading #2 " + gameTime.TotalGameTime);
                        MovingPlatformCom();
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
                    if (platform_com.Velocity.X > 0)
                    {
                        if (ball.Velocity.X > 0 || ball.Velocity.X < -5)
                            ball.Velocity = new Vector2(ball.Velocity.X + 5, ball.Velocity.Y);
                    }
                    else if (platform_com.Velocity.X < 0)
                    {
                        if (ball.Velocity.X > 5 || ball.Velocity.X < 0)
                            ball.Velocity = new Vector2(ball.Velocity.X - 5, ball.Velocity.Y);
                    }
                }
            }

            

            // AI 
            //move platform com when required
            if (movePlatformCom)
            {
                //we are at right position, stop moving
                if (targetPositionX >= platform_com.Position.X + platform_com.Dimension.X/5*2 && targetPositionX <= platform_com.Position.X + platform_com.Dimension.X/5*3)
                    movePlatformCom = false;
                else if (targetPositionX < platform_com.Position.X + platform_com.Dimension.X/5*2)
                    platform_com.Position = new Vector2(platform_com.Position.X - platform_com.MoveSpeed, platform_com.Position.Y);
                else if (targetPositionX > platform_com.Position.X + platform_com.Dimension.X/5*3)
                    platform_com.Position = new Vector2(platform_com.Position.X + platform_com.MoveSpeed, platform_com.Position.Y);
            }

            base.Update(gameTime);
            ball.Update(gameTime);
            platform_com.Update(gameTime);
            platform_player.Update(gameTime);

            /* check game end condition */
            //if part of ball image is below screen, then game end
            if (ball.Position.Y + ball.ImageHeight > ScreenManager.Instance.Dimensions.Y)
            {
                //pop in end game state
                current = gameTime.TotalGameTime;
                timeTag.Add(current);
                info.Add("GameEnd");
                data.Add("BallPos\t" + ball.Position.X + "," + ball.Position.Y 
                    + "\tPlatPos\t" + platform_player.Position.X + "," + platform_player.Position.Y 
                    + "\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y 
                    + "\tRounds\t" + (rounds-1) );
                /// <note>
                /// round - 1
                /// somehow, program goes through collision code(where round++)
                /// before I start game, after screen loading
                /// </note> 



                //clear some states
                start = false;
                end = true;
                
                //forze game state
                ball.Velocity = new Vector2(0, 0);
                platform_player.ControlByPlayer = false;

                if (!dataCollect)
                {
                    dataCollect = true;
                    //serilize data
                    //output path - file name
                    //see bin/x86/debug/
                    string path = @".\Data.txt";

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
                            for (int i = 0; i < timeTag.Count; i++)
                            {
                                str = timeTag[i] + "\t" + info[i] + "\t" + data[i];
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
                    //where the ball is, where ball is moving to
                    //where platform stoped (platform current position)
                    current = gameTime.TotalGameTime;
                    timeTag.Add(current);
                    info.Add("PlatformStop");
                    data.Add("BallPos\t" + ball.Position.X + "," + ball.Position.Y + "\tPlaXCoor\t" + platform_player.Position.X
                        + "\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y);
                }
            }
            else
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
                    current = gameTime.TotalGameTime;
                    timeTag.Add(current);
                    info.Add("PlatformMove");
                    data.Add("BallPos\t" + ball.Position.X + "," + ball.Position.Y + "\tPlaXCoor\t" + platform_player.Position.X
                        + "\tBallVel\t" + ball.Velocity.X + "," + ball.Velocity.Y);
                }
            }

        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
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

        /* move com platform to hit ball back */
        public void MovingPlatformCom()
        { 
            // hit position at Y 20 + platform.height
            // use current position and velocity to estimate (X, 20 + platform.height)
            Vector2 estPosition = ball.Position;
            //total distance ball needs to move on vertial
            float temp = ball.Position.Y - 20 - platform_com.Dimension.Y;
            //which takes how many updates (vertical speed, 10 per update)
            temp = temp / 10;
            //apply to harizontal, this coordinates is likely out side of windows
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
                     */
                { 
                    //esti pos - (distance to hit right window)
                    //distance left after ball hit right window border
                    //reverse direction
                    //estPosition.X = windowWidth - ball.Origin.X - (estPosition.X - (windowWidth - ball.Origin.X - ball.Position.X));

                    /*  
+                     * explain of subtract 100 in fomular below
+                     * I currently have no idea why the estimated position is worng
+                     * but it appears everytime the ball hits right window border
+                     * my estimation position shifts to right by 100 from real position
+                     */

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

        #endregion
    }
}
