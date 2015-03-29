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
    public class Platform : Entity
    {
        Vector2 position, origin, velocity;
        Texture2D platformImage;
        Rectangle sourceRect;
        float rotation, scale, alpha;
        Vector2 dimension;

        float moveSpeed;
        bool controlByPlayer;

        Color[] textureData;

        public Rectangle Rectangle
        {
            get { return sourceRect; }
        }

        public Color[] ColorData
        {
            get { return textureData; }
        }

        public Vector2 Origin
        {
            get { return origin; }
        }

        public Vector2 Position
        {
            get { return position; }
            set { position = value; }
        }

        public Vector2 Dimension 
        {
            get { return dimension; }
        }

        public bool ControlByPlayer
        {
            set { controlByPlayer = value; }
        }

        public Vector2 Velocity
        {
            get { return velocity; }
            set { velocity = value; }
        }

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);
            content = new ContentManager(Content.ServiceProvider, "Content");
            platformImage = content.Load<Texture2D>("platform");

            textureData = new Color[platformImage.Width * platformImage.Height];
            platformImage.GetData(textureData);

            if (platformImage != null)
                sourceRect = new Rectangle(0, 0, platformImage.Width, platformImage.Height);
            rotation = 0.0f;
            scale = 1.0f;
            alpha = 0.0f;
            position = new Vector2(ScreenManager.Instance.Dimensions.X / 2 - platformImage.Width / 2, 
                ScreenManager.Instance.Dimensions.Y - 20 - platformImage.Height);

            controlByPlayer = true;
            moveSpeed = 10f;
            dimension = new Vector2(platformImage.Width, platformImage.Height);
        }

        public override void UnloadContent()
        {
            base.UnloadContent();
            platformImage = null;
            position = Vector2.Zero;
            origin = Vector2.Zero;
            sourceRect = Rectangle.Empty;
            dimension = Vector2.Zero;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            inputManager.Update();
            //animation loading
            if (alpha < 1.0f)
                alpha += 0.05f;
            else
                alpha = 1.0f;

            if (inputManager.KeyDown(Keys.Left, Keys.A) && controlByPlayer)
            {
                position = new Vector2(position.X - moveSpeed, position.Y);
                velocity = new Vector2(-moveSpeed, 0);
            }
            else if (inputManager.KeyDown(Keys.Right, Keys.D) && controlByPlayer)
            {
                position = new Vector2(position.X + moveSpeed, position.Y);
                velocity = new Vector2(moveSpeed, 0);
            }
            else if (controlByPlayer)
            { 
                velocity = new Vector2(0, 0); 
            }


            if (position.X < 0)
                position = new Vector2(0, position.Y);
            if (position.X + dimension.X > ScreenManager.Instance.Dimensions.X)
                position = new Vector2(ScreenManager.Instance.Dimensions.X - dimension.X, position.Y);

            //update rectangle position
            sourceRect.X = (int)position.X;
            sourceRect.Y = (int)position.Y;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (platformImage != null)
            {
                origin = new Vector2(sourceRect.Width / 2, sourceRect.Height / 2);
                spriteBatch.Draw(platformImage, position + origin, sourceRect, Color.White * alpha, rotation, origin, scale, SpriteEffects.None, 0.0f);
            }
        }
    }
}
