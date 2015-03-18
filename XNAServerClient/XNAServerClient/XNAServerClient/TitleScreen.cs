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
    public class TitleScreen : GameScreen
    {
        SpriteFont font;
        MenuManager menu;
        Texture2D background;

        //server code
        Char[] code = {'s', 'e', 'r', 'v', 'e', 'r'};
        int codeIndex = 0;

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);
            if (font == null)
                font = this.content.Load<SpriteFont>("Font1");
            menu = new MenuManager();
            menu.LoadContent(content, "Title");

            //background = content.Load<Texture2D>("Background/bg1");
        }

        public override void UnloadContent()
        {
            base.UnloadContent();
            menu.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {
            inputManager.Update();
            menu.Update(gameTime, inputManager);

            //check for server code
            //type "server" allow to start a server 
            if (inputManager.KeyPressed(Keys.S) && codeIndex == 0)
            {
                codeIndex++;
            }
            else if (inputManager.KeyPressed(Keys.E) && (codeIndex == 1 || codeIndex == 4) )
            {
                codeIndex++;
            }
            else if (inputManager.KeyPressed(Keys.R) && (codeIndex == 2 || codeIndex == 5) )
            {
                codeIndex++;
                //start a server
                if (codeIndex == 6)
                {
                    Type newClass = Type.GetType("XNAServerClient.GamePlayScreen");
                    ScreenManager.Instance.AddScreen((GameScreen)Activator.CreateInstance(newClass), inputManager);
                }

            }
            else if (inputManager.KeyPressed(Keys.V) && codeIndex == 3)
            {
                codeIndex++;
            }
            
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (background != null)
                spriteBatch.Draw(background, new Vector2(0,0), Color.White);
            //spriteBatch.DrawString(font, "Abusement Park Mini Game", new Vector2(170, 80), Color.White);
            menu.Draw(spriteBatch);
        }
    }
}
