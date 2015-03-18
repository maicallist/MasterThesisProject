using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace XNAServerClient
{
    public class CreditScreen : GameScreen
    {
        SpriteFont font;
        int counter;

        public override void LoadContent(Microsoft.Xna.Framework.Content.ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);
            if (font == null)
                font = content.Load<SpriteFont>("Font1");
            counter = 100;
        }

        public override void UnloadContent()
        {
            font = null;
            counter = 0;
            base.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            counter--;

            if (counter <= 0)
            {
                Type newClass = Type.GetType("IndividualGame.TitleScreen");
                ScreenManager.Instance.AddScreen((GameScreen)Activator.CreateInstance(newClass), inputManager);
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            Vector2 textDimension = new Vector2(font.MeasureString("Game Development : Chen Gao").X, font.MeasureString("Chen Gao").Y);
            Vector2 textDimension2 = new Vector2(font.MeasureString("Flinders University").X, font.MeasureString("Flinders University").Y);
            int width = (int)ScreenManager.Instance.Dimensions.X;
            int height = (int)ScreenManager.Instance.Dimensions.Y;
            spriteBatch.DrawString(font, "Game Development : Chen Gao", new Vector2((width - (int)textDimension.X)/2, (height - (int)textDimension.Y)/2), Color.White);
            spriteBatch.DrawString(font, "Flinders University", new Vector2((width - (int)textDimension2.X) / 2, (height - (int)textDimension2.Y) / 2 + textDimension.Y + 10), Color.White);
        }
    }
}
