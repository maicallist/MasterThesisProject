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
    public class Entity
    {
        protected ContentManager content;
        protected InputManager inputManager;
        public FadeAnimation fade;

        public virtual void LoadContent(ContentManager Content, InputManager inputManager) 
        {
            content = new ContentManager(Content.ServiceProvider, "Content");
            this.inputManager = inputManager;
        }

        public virtual void LoadContent(ContentManager Content, InputManager inputManager, string name, Vector2 position)
        {
            content = new ContentManager(Content.ServiceProvider, "Content");
            this.inputManager = inputManager;
        }

        public virtual void UnloadContent() 
        {
            content.Unload();
        }

        public virtual void Update(GameTime gamtTime) { }
        public virtual void Draw(SpriteBatch spriteBatch) { }
    }
}
