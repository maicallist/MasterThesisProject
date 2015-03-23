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
    class ServerScreen : GameScreen
    {
        #region Variables
        Platform Player_1;
        Platform Player_2;

        Ball ball;

        //game state
        GameState gameState;

        //after both gamer requested start
        //game will start
        bool P1StartRequest;
        bool P2StartRequest;

        SpriteFont font;

        //Networking Members
        NetworkSession session;

        ///<summary>
        ///below 2 members are used for real game, not for this test platform
        ///</summary>>
        //AvailableNetworkSessionCollection availableSessions;
        //int sessionIndex;
        AvailableNetworkSession AvailablesSession;
        PacketReader packetReader;
        PacketWriter packetWriter;
        bool isServer;

        enum GameState { Menu, FindGame, WaitingGame, PlayGame }
        enum SessionProperty { GameMode, SkillLevel, ScoreToWin }
        enum GameMode { Practice, Timed, CaptureTheFlag }
        enum SkillLevel { Beginner, Intermediate, Advanced }
        enum PacketType { Enter, Leave, Data }

        #endregion

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);

            //inital game state
            gameState = GameState.Menu;

            //networking state
            isServer = true;
            P1StartRequest = false;
            P2StartRequest = false;

           //inital network
            packetReader = new PacketReader();
            packetWriter = new PacketWriter();

            //loading assets
            if (font == null)
                font = content.Load<SpriteFont>("Font1");
            Player_1 = new Platform();
            Player_1.LoadContent(Content, inputManager);
            Player_2 = new Platform();
            Player_2.LoadContent(Content, inputManager);
            ball = new Ball();
            ball.LoadContent(Content, inputManager);
        }

        public override void UnloadContent()
        {
            base.UnloadContent();

            Player_1.UnloadContent();
            Player_2.UnloadContent();
            ball.UnloadContent();

            //release network
            if (session != null)
                session.Dispose();
        }

        public override void Update(GameTime gameTime)
        {
            //prevent ball moving before start
            if (gameState != GameState.PlayGame)
                ball.Velocity = new Vector2(0, 0);

            //create a game session and wait for players
            if (gameState != GameState.Menu && session == null)
            {
                HostGame();
                gameState = GameState.WaitingGame;
            }
            else if (gameState == GameState.WaitingGame)
            { 
                //if 2 players are ready, start game

            }
            else if (gameState == GameState.PlayGame)
            {
                ///update game logic
                Rectangle platformRect = Player_1.Rectangle;
                Color[] platformColor = Player_1.ColorData;
                Rectangle ballRect = ball.Rectangle;
                Color[] ballColor = ball.ColorData;

                //ball collade with P1
                if (ballRect.Intersects(platformRect))
                {
                    //check pixel collision
                    if (UpdateCollision(ballRect, ballColor, platformRect, platformColor))
                    {
                        //if ball center is higher than Player_1, ball's velocity Y is negative 
                        //if ball center is lower than Player_1, ball's velocity Y is positive
                        Vector2 ballOrigin = new Vector2(ball.Origin.X + ballRect.X, ball.Origin.Y + ballRect.Y);
                        Vector2 platformOrigin = new Vector2(Player_1.Origin.X + platformRect.X, Player_1.Origin.Y + platformRect.Y);

                        if (ballOrigin.X >= Player_1.Position.X && ballOrigin.X <= Player_1.Position.X + Player_1.Dimension.X)
                            ball.Velocity = new Vector2(ball.Velocity.X, ball.Velocity.Y * -1);
                        else
                            ball.Velocity = new Vector2(ball.Velocity.X * -1, ball.Velocity.Y);

                        //if Player_1 is moving while collade, add extra speed to ball
                        //if (inputManager.KeyDown(Keys.Left, Keys.A))
                        //    ball.Velocity = new Vector2(ball.Velocity.X - 5, ball.Velocity.Y);
                        //else if (inputManager.KeyDown(Keys.Right, Keys.D))
                        //    ball.Velocity = new Vector2(ball.Velocity.X + 5, ball.Velocity.Y);
                    }
                }

                base.Update(gameTime);
                Player_1.Update(gameTime);
                ball.Update(gameTime);
            
                ///write data to client
                
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            //draw objects
            ball.Draw(spriteBatch);
            Player_1.Draw(spriteBatch);


            //int displayScore = score - ball.HitGround * 5;

            //if (!start)
            //    spriteBatch.DrawString(font, "Press Space to Start..",
            //        new Vector2(ScreenManager.Instance.Dimensions.X / 2 - font.MeasureString("Press Space to Start..").X / 2, ScreenManager.Instance.Dimensions.Y / 2), Color.White);
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

        // check each pixel on two texture, looking for overlap
        private bool UpdateCollision(Rectangle rect1, Color[] colorData1, Rectangle rect2, Color[] colorData2)
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

        //create new network session
        protected void HostGame()
        {
            //require Singed In 
            //Skipped

            NetworkSessionProperties sessionProperties = new NetworkSessionProperties();

            // Game mode
            sessionProperties[(int)SessionProperty.GameMode] 
                = (int)GameMode.Practice; 
            //player match level
            sessionProperties[(int)SessionProperty.SkillLevel]
                = (int)SkillLevel.Beginner; 
            // Score to win
            sessionProperties[(int)SessionProperty.ScoreToWin]
                = 100;

            int maximumGamers = 2;
            int maximumLocalPlayers = 1;
            
            //creating session
            //SystemLink = connect xbox360 or pc over a local subnet
            session = NetworkSession.Create(NetworkSessionType.SystemLink, maximumLocalPlayers, maximumGamers);
            
            session.AllowHostMigration = false;
            session.AllowJoinInProgress = false;
            gameState = GameState.FindGame;

        }
    }
}
