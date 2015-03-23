using System;
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

        Platform platform;
        Ball ball;

        //game state
        GameState gameState;

        SpriteFont font;

        //Networking Members
        NetworkSession session;
        //AvailableNetworkSession AvailablesSession;
        PacketReader packetReader;
        PacketWriter packetWriter;
        bool isServer;

        enum GameState { Menu, FindGame, WaitingGame, PlayGame }
        enum SessionProperty { GameMode, SkillLevel, ScoreToWin }
        enum GameMode { Practice, Timed, CaptureTheFlag }
        enum SkillLevel { Beginner, Intermediate, Advanced }
        enum PacketType { Enter, Leave, Data }

        string errorMessage;

        #endregion

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);

            //inital game state
            gameState = GameState.FindGame;

            //networking state
            isServer = false;

            //inital network
            packetReader = new PacketReader();
            packetWriter = new PacketWriter();

            //loading assets
            if (font == null)
                font = content.Load<SpriteFont>("Font1");

            platform = new Platform();
            platform.LoadContent(Content, inputManager);
            ball = new Ball();
            ball.LoadContent(Content, inputManager);

        }

        public override void UnloadContent()
        {
            base.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {
            //prevent ball moving before start
            if (gameState != GameState.PlayGame)
                ball.Velocity = new Vector2(0, 0);

            if (gameState == GameState.FindGame)
                JoinGame();
            else if (gameState == GameState.WaitingGame)
            { 
                //waiting server to match player and start game 
            }
            else if (gameState == GameState.PlayGame)
            {
                ///receive packet from server
                

                ///update local game

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
                        Vector2 ballOrigin = ball.Origin + ball.Position;
                        Vector2 platformOrigin = platform.Origin + platform.Position;

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
                    }
                }

                base.Update(gameTime);
                platform.Update(gameTime);
                ball.Update(gameTime);

                ///write data to server
                
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            ball.Draw(spriteBatch);
            platform.Draw(spriteBatch);


            //int displayScore = score - ball.HitGround * 5;
            //if (!start)
            //    spriteBatch.DrawString(font, "Press Space to Start..", 
            //        new Vector2(ScreenManager.Instance.Dimensions.X/2 - font.MeasureString("Press Space to Start..").X/2, ScreenManager.Instance.Dimensions.Y/2), Color.White);
            //if (start)
            //    spriteBatch.DrawString(font, "Score " + displayScore, new Vector2(10, 770), Color.White);

            //if (ending)
            //{
            //    spriteBatch.DrawString(font, "Press Space to Restart..",
            //        new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Space to Restart..").X / 2, ScreenManager.Instance.Dimensions.Y / 2), Color.White);
            //    spriteBatch.DrawString(font, "Press ESC to Title..",
            //        new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press ESC to Title..").X / 2, ScreenManager.Instance.Dimensions.Y / 2), Color.White);
            //}
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


        /* SignIn displays the Xbox360 Guide */
        static protected void SignIn()
        {
            if (!Guide.IsVisible)
                Guide.ShowSignIn(1, true);
        }

        /* join in a existing session */
        public void JoinGame()
        {
            int maximumLocalGamers = 2;
            try
            {
                AvailableNetworkSessionCollection availableSessions
                    = NetworkSession.Find(NetworkSessionType.SystemLink, maximumLocalGamers, null);

                if (availableSessions.Count == 0)
                {
                    errorMessage = "Server is not available.";
                    return;
                }

                //join the first session
                //in this case, we only have one session for testing
                session = NetworkSession.Join(availableSessions[0]);
                gameState = GameState.WaitingGame;
                //hook session event
                //HookSessionEvents();
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }
        }

        /*  */
        public void ReceivePacket()
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
                }
            }
        }


    }
}
