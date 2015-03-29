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
    class DataCollector : GameScreen
    {
        #region Variables

        Platform platform_player;
        Platform platform_com;

        Ball ball;

        SpriteFont font;

        bool start;

        /* statistics */


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

            start = false;

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
                    /* start game */
                    start = true;
                    ball.Velocity = new Vector2(-7, -10);
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
                    //if ball center is higher than platform, ball's velocity Y is negative 
                    //if ball center is lower than platform, ball's velocity Y is positive
                    Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
                    Vector2 platformOrigin_player = new Vector2(platform_player.Origin.X + platformRect_player.X, platform_player.Origin.Y + platformRect_player.Y);

                    //platform height is 25, so check -12 to 12 around origin.y
                    if ((ballOrigin.Y - platformOrigin_player.Y) <= -12)
                        ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y * -1);
                    else if ((ballOrigin.Y - platformOrigin_player.Y) >= 12)
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                    else if (Math.Abs(ballOrigin.Y - platformOrigin_player.Y) < 12)
                        ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);

                    //if platform is moving while collade, add extra speed to ball
                    if (inputManager.KeyDown(Keys.Left, Keys.A))
                        ball.Velocity = new Vector2(ball.Velocity.X - 5, ball.Velocity.Y);
                    else if (inputManager.KeyDown(Keys.Right, Keys.D))
                        ball.Velocity = new Vector2(ball.Velocity.X + 5, ball.Velocity.Y);
                }
            }

            Rectangle platformRect_com = platform_com.Rectangle;
            Color[] platformColor_com = platform_com.ColorData;
            //ball collade with plaform
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
                        ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y * -1);
                    else if ((ballOrigin.Y - platformOrigin_com.Y) >= 12)
                        ball.Velocity = new Vector2(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
                    else if (Math.Abs(ballOrigin.Y - platformOrigin_com.Y) < 12)
                        ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);

                    //if platform is moving while collade, add extra speed to ball
                    if (inputManager.KeyDown(Keys.Left, Keys.A))
                        ball.Velocity = new Vector2(ball.Velocity.X - 5, ball.Velocity.Y);
                    else if (inputManager.KeyDown(Keys.Right, Keys.D))
                        ball.Velocity = new Vector2(ball.Velocity.X + 5, ball.Velocity.Y);
                }
            }

            base.Update(gameTime);
            ball.Update(gameTime);
            platform_com.Update(gameTime);
            platform_player.Update(gameTime);

        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            ball.Draw(spriteBatch);
            platform_player.Draw(spriteBatch);
            platform_com.Draw(spriteBatch);

            if (!start)
                spriteBatch.DrawString(font, "Press Space to Start..",
                    new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Space to Start..").X/2, 
                        ScreenManager.Instance.Dimensions.Y/2), 
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

        #endregion
    }
}
