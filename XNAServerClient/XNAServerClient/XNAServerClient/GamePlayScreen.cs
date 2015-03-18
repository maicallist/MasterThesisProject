using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace XNAServerClient
{
    public class GamePlayScreen : GameScreen
    {
        Platform platform;
        Ball ball;

        bool start;

        SpriteFont font;

        public static int score;

        //background
        //LayeredBackground background;
        //Texture2D backLayer;
        //nothing on frontLayer
        //Texture2D frontLayer;

        //game state
        bool ending;

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);

            start = false;
            ending = false;

            score = 100;

            if (font == null)
                font = content.Load<SpriteFont>("Font1");

            platform = new Platform();
            platform.LoadContent(Content, inputManager);
            ball = new Ball();
            ball.LoadContent(Content, inputManager);
            //brickSet = new BrickSet();
            //brickSet.Loadcontent(Content, inputManager);

            //background = new LayeredBackground(Game1.myGameInstance.Graphics, ball, inputManager);
            //backLayer = content.Load<Texture2D>("Background/bg1");
            //background.AddLayer(backLayer, 1.0f, 40f);
        }

        public override void UnloadContent()
        {
            base.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {
            //prevent ball moving before start
            if (!start)
                ball.Velocity = new Vector2(0, 0);

            //background.Update(gameTime, platform.Position);

            Rectangle platformRect = platform.Rectangle;
            Color[] platformColor = platform.ColorData;
            Rectangle ballRect = ball.Rectangle;
            Color[] ballColor = ball.ColorData;
            //ball collade with plaform
            if (ballRect.Intersects(platformRect))
            {
                //check pixel collision
                if (UpdateCollision(ballRect, ballColor, platformRect, platformColor))
                {
                    //if ball center is higher than platform, ball's velocity Y is negative 
                    //if ball center is lower than platform, ball's velocity Y is positive
                    //Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
                    //Vector2 platformOrigin = new Vector2(platform.Origin.X + platformRect.X, platform.Origin.Y + platformRect.Y);
                    Vector2 ballOrigin = ball.Origin + ball.Position;
                    Vector2 platformOrigin = platform.Origin + platform.Position;

                    //platform height is 25, so check -12 to 12 around origin.y
                    //if ((ballOrigin.Y - platformOrigin.Y) <= -12)
                    //    ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y * -1);
                    //else if ((ballOrigin.Y - platformOrigin.Y) >= 12.5)
                    //    ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                    //else if (Math.Abs(ballOrigin.Y - platformOrigin.Y) < 12)
                    //    ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);

                    if (ballOrigin.X >= platform.Position.X && ballOrigin.X <= platform.Position.X + platform.Dimension.X)
                    {
                        if (ballOrigin.Y < platformOrigin.Y)
                            ball.Velocity = new Vector2(ball.Velocity.X, -Math.Abs(ball.Velocity.Y));
                        else if (ballOrigin.Y > platformOrigin.Y)
                            ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                    }
                    else
                    { 
                        if (ballOrigin.X < platformOrigin.X)
                            ball.Velocity = new Vector2(-Math.Abs(ball.Velocity.X), ball.Velocity.Y);
                        else if (ballOrigin.X > platformOrigin.X)
                            ball.Velocity = new Vector2(Math.Abs(ball.Velocity.X), ball.Velocity.Y);
                    }

                    //if platform is moving while collade, add extra speed to ball
                    //if (inputManager.KeyDown(Keys.Left, Keys.A))
                    //    ball.Velocity = new Vector2(ball.Velocity.X - 5, ball.Velocity.Y);
                    //else if (inputManager.KeyDown(Keys.Right, Keys.D))
                    //    ball.Velocity = new Vector2(ball.Velocity.X + 5, ball.Velocity.Y);
                }
            }
            
            //check collade with brick
            //Rectangle brickRect;
            //Color[] brickColor;
            //for (int i = 0; i < brickSet.Count; i++)
            //{ 
            //    brickRect = brickSet.At(i).Rectangle;
            //    brickColor = brickSet.At(i).ColorData;
            //    if (ballRect.Intersects(brickRect))
            //    { 
            //        //check the pixel
            //        if (UpdateCollision(ballRect, ballColor, brickRect, brickColor))
            //        {
            //            //update ball velocity, similar to platformer
            //            Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
            //            Vector2 brickOrigin = new Vector2(brickSet.At(i).Origin.X + brickRect.X, brickSet.At(i).Origin.Y + brickRect.Y);

            //            if ((ballOrigin.Y - brickOrigin.Y) <= -17)
            //                ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y * -1);
            //            else if ((ballOrigin.Y - brickOrigin.Y) >= 17)
            //                ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
            //            else if (Math.Abs(ballOrigin.Y - brickOrigin.Y) < 17)
            //                ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);

            //            //update brick effect to ball
            //            if (brickSet.At(i).BrickName.Equals("b2"))
            //            {
            //                if (ball.Velocity.Y > 0)
            //                    ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y + 5);
            //                else
            //                    ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y - 5);
            //            }
            //            else if (brickSet.At(i).BrickName.Equals("b3"))
            //            {
            //                if (ball.Velocity.Y > 5)
            //                    ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y - 5);
            //                else if (ball.Velocity.Y < -5)
            //                    ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y + 5);
            //            }

            //            //delete brick
            //            brickSet.RemoveAt(i);
            //            score += 10;
            //        }
            //    }
            //}

            base.Update(gameTime);
            platform.Update(gameTime);
            ball.Update(gameTime);
            //brickSet.Update(gameTime);

            if (inputManager.KeyPressed(Keys.Space) && !start)
            {
                start = true;
                ball.Velocity = new Vector2(-7, -10);
            }

            ////end game
            //if (brickSet.Count == 0)
            //{
            //    ending = true;
            //    if (inputManager.KeyPressed(Keys.Space))
            //    {
            //        Type newClass = Type.GetType("IndividualGame.GamePlayScreen");
            //        ScreenManager.Instance.AddScreen((GameScreen)Activator.CreateInstance(newClass), inputManager);
            //    }
            //    else if (inputManager.KeyPressed(Keys.Escape))
            //    {
            //        Type newClass = Type.GetType("IndividualGame.TitleScreen");
            //        ScreenManager.Instance.AddScreen((GameScreen)Activator.CreateInstance(newClass), inputManager);
            //    }
            //}
        }

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

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            //background.Draw();
            ball.Draw(spriteBatch);
            platform.Draw(spriteBatch);
            //brickSet.Draw(spriteBatch);

            int displayScore = score - ball.HitGround * 5;
            if (!start)
                spriteBatch.DrawString(font, "Press Space to Start..", 
                    new Vector2(ScreenManager.Instance.Dimensions.X/2 - font.MeasureString("Press Space to Start..").X/2, ScreenManager.Instance.Dimensions.Y/2), Color.White);
            if (start)
                spriteBatch.DrawString(font, "Score " + displayScore, new Vector2(10, 770), Color.White);

            if (ending)
            {
                spriteBatch.DrawString(font, "Press Space to Restart..",
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Space to Restart..").X / 2, ScreenManager.Instance.Dimensions.Y / 2), Color.White);
                spriteBatch.DrawString(font, "Press ESC to Title..",
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press ESC to Title..").X / 2, ScreenManager.Instance.Dimensions.Y / 2), Color.White);
            }
        }
    }
}
