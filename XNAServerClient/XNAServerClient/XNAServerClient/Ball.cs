using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;

namespace XNAServerClient
{
    public class Ball : Entity
    {
        Vector2 position, origin;
        Texture2D ballImage;
        Rectangle sourceRect;
        Rectangle originRect;
        float rotation, scale, alpha;

        Vector2 velocity;

        //piexl collision
        Color[] textureData;

        int hitGround;

        public Vector2 Velocity
        {
            get { return velocity; }
            set { velocity = value; }
        }

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

        public int HitGround
        {
            get { return hitGround; }
        }

        public int ImageHeight
        {
            get { return ballImage.Height; }
        }

        public int ImageWidth
        {
            get { return ballImage.Width; }
        }

        public float Alpha
        {
            set { alpha = value; }
        }

        public override void LoadContent(ContentManager Content, InputManager inputManager)
        {
            base.LoadContent(Content, inputManager);
            content = new ContentManager(Content.ServiceProvider, "Content");
            ballImage = content.Load<Texture2D>("balla");

            textureData = new Color[ballImage.Width * ballImage.Height];
            ballImage.GetData(textureData);

            if (ballImage != null)
            {
                originRect = new Rectangle(0, 0, ballImage.Width, ballImage.Height);
                sourceRect = new Rectangle(0, 0, ballImage.Width, ballImage.Height);
            }
            rotation = 0.0f;
            scale = 1.0f;
            position = new Vector2(ScreenManager.Instance.Dimensions.X / 2 - ballImage.Width / 2, 200);
            alpha = 0.0f;
            velocity = new Vector2(0, 0);

            hitGround = 0;
        }

        public override void UnloadContent()
        {
            base.UnloadContent();
            ballImage = null;
            position = Vector2.Zero;
            origin = Vector2.Zero;
            velocity = Vector2.Zero;
            sourceRect = Rectangle.Empty;
        }

        public override void Update(GameTime gamtTime)
        {
            base.Update(gamtTime);
            if (alpha < 1.0f)
                alpha += 0.05f;
            else
                alpha = 1.0f;

            //check screen bounds
            if (position.X <= 0)
                velocity = new Vector2(velocity.X * -1, velocity.Y);
            else if (position.X + ballImage.Width >= ScreenManager.Instance.Dimensions.X)
                velocity = new Vector2(Math.Abs(velocity.X) * -1, velocity.Y);
            if (position.Y <= 0)
                velocity = new Vector2(velocity.X, velocity.Y * -1);
            else if (position.Y + ballImage.Height >= ScreenManager.Instance.Dimensions.Y)
            {
                //hit ground, game end
                //do nothing, end game in play screen

                //if you don't want end game, comment above and comment out below
                velocity = new Vector2(velocity.X, velocity.Y * -1);
            }
            
            //move ball base on velocity
            position += velocity;

            //update rectangle position
            sourceRect.X = (int)position.X;
            sourceRect.Y = (int)position.Y;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            if (ballImage != null)
            {
                origin = new Vector2(originRect.Width / 2, originRect.Height / 2);
                spriteBatch.Draw(ballImage, position + origin, originRect, Color.White * alpha, rotation, origin, scale, SpriteEffects.None, 0.0f);
            }
        }
    }
}
