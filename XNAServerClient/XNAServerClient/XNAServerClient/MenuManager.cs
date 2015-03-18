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
    public class MenuManager
    {
        List<string> menuItems, animationTypes, linkType, linkID;
        List<Texture2D> menuImages;
        List<Animation> tempAnimation;
        List<List<Animation>> animation;

        ContentManager content;
        FileManager fileManager;

        Vector2 position;
        Rectangle source;
        SpriteFont font;

        int axis, itemNumber;
        string align;

        #region Private Methods

        private void SetMenuItems()
        {
            for (int i = 0; i < menuItems.Count; i++)
            {
                if (menuImages.Count == i)
                    menuImages.Add(ScreenManager.Instance.NullImage);
            }
            for (int i = 0; i < menuImages.Count; i++)
            {
                if (menuItems.Count == i)
                    menuItems.Add("");
            }
        }

        private void SetAnimations()
        {
            Vector2 pos = Vector2.Zero;
            Vector2 dimensions = Vector2.Zero;
            
            if (align.Contains("Center"))
            {
                for (int i = 0; i < menuItems.Count; i++)
                {
                    dimensions.X += font.MeasureString(menuItems[i]).X + menuImages[i].Width;
                    dimensions.Y += font.MeasureString(menuItems[i]).Y + menuImages[i].Height;
                }

                if (axis == 1)
                {
                    pos.X = (ScreenManager.Instance.Dimensions.X - dimensions.X) / 2;
                }
                else if (axis == 2)
                {
                    pos.Y = (ScreenManager.Instance.Dimensions.Y - dimensions.Y) / 2;
                }
            }
            else 
            {
                    pos = position;
            }

            tempAnimation = new List<Animation>();

            for (int i = 0; i < menuImages.Count; i++)
            {
                dimensions = new Vector2(font.MeasureString(menuItems[i]).X + menuImages[i].Width, font.MeasureString(menuItems[i]).Y + menuImages[i].Height);
                
                if (axis == 1)
                    pos.Y = (ScreenManager.Instance.Dimensions.Y - dimensions.Y) / 2;
                else
                    pos.X = (ScreenManager.Instance.Dimensions.X - dimensions.X) / 2;

                for (int j = 0; j < animationTypes.Count; j++)
                {
                    switch (animationTypes[j])
                    {
                        case "Fade":
                            tempAnimation.Add(new MenuAnimation());
                            tempAnimation[tempAnimation.Count - 1].LoadContent(content, menuImages[i], menuItems[i], pos);
                            tempAnimation[tempAnimation.Count - 1].Font = font;
                            break;
                    }
                }

                if(tempAnimation.Count > 0)
                    animation.Add(tempAnimation);
                tempAnimation = new List<Animation>();

                if (axis == 1)
                {
                    pos.X += dimensions.X;
                }
                else 
                {
                    pos.Y += dimensions.Y;
                }
            }
        }

        #endregion

        public void LoadContent(ContentManager content, string id)
        {
            this.content = new ContentManager(content.ServiceProvider, "Content");
            menuItems = new List<string>();
            animationTypes = new List<string>();
            linkType = new List<string>();
            linkID = new List<string>();
            menuImages = new List<Texture2D>();
            animation = new List<List<Animation>>();
            itemNumber = 0;

            position = Vector2.Zero;

            fileManager = new FileManager();
            fileManager.LoadContent("Load/Menu.cme", id);

            for (int i = 0; i < fileManager.Attributes.Count; i++)
            {
                for (int j = 0; j < fileManager.Attributes[i].Count; j++)
                {
                    switch (fileManager.Attributes[i][j])
                    {
                        case "Font":
                            font = this.content.Load<SpriteFont>(fileManager.Contents[i][j]);
                            break;
                        case "Item":
                            menuItems.Add(fileManager.Contents[i][j]);
                            break;
                        case "Image":
                            menuImages.Add(this.content.Load<Texture2D>(fileManager.Contents[i][j]));
                            break;
                        case "Axis":
                            axis = int.Parse(fileManager.Contents[i][j]);
                            break;
                        case "Position":
                            string[] temp = fileManager.Contents[i][j].Split(' ');
                            position = new Vector2(float.Parse(temp[0]), float.Parse(temp[1]));
                            break;
                        case "Source":
                            temp = fileManager.Contents[i][j].Split(' ');
                            source = new Rectangle(int.Parse(temp[0]), int.Parse(temp[1]), int.Parse(temp[2]), int.Parse(temp[3]));
                            break;
                        case "Animation":
                            animationTypes.Add(fileManager.Contents[i][j]);
                            break;
                        case "Align":
                            align = fileManager.Contents[i][j];
                            break;
                        case "LinkType":
                            linkType.Add(fileManager.Contents[i][j]);
                            break;
                        case "LinkID":
                            linkID.Add(fileManager.Contents[i][j]);
                            break;
                    }
                }
            }
            SetMenuItems();
            SetAnimations();
        }

        public void UnloadContent()
        {
            content.Unload();
            fileManager = null;
            position = Vector2.Zero;
            menuItems.Clear();
            animation.Clear();
            menuImages.Clear();
            animationTypes.Clear();
            
        }

        public void Update(GameTime gameTime, InputManager inputManager)
        {
            if (axis == 1)
            {
                if (inputManager.KeyPressed(Keys.Right, Keys.D))
                    itemNumber++;
                else if (inputManager.KeyPressed(Keys.Left, Keys.A))
                    itemNumber--;
            }
            else
            {
                if (inputManager.KeyPressed(Keys.Down, Keys.S))
                    itemNumber++;
                else if (inputManager.KeyPressed(Keys.Up, Keys.W))
                    itemNumber--;
            }
            
            if(inputManager.KeyPressed(Keys.Enter, Keys.Space))
            {
                if (linkType[itemNumber] == "Screen")
                {
                    Type newClass = Type.GetType("XNAServerClient." + linkID[itemNumber]);
                    ScreenManager.Instance.AddScreen((GameScreen)Activator.CreateInstance(newClass), inputManager);
                }
                else if (linkType[itemNumber] == "Exit")
                {
                    Game1.myGameInstance.Exit();
                }
            }

            if (itemNumber < 0)
                itemNumber = 0;
            else if (itemNumber > menuItems.Count - 1)
                itemNumber = menuItems.Count - 1;

            for(int i = 0; i < animation.Count; i++)
            {
                for (int j = 0; j < animation[i].Count; j++)
                {
                    if (itemNumber == i)
                        animation[i][j].IsActive = true;
                    else
                        animation[i][j].IsActive = false;

                    animation[i][j].Update(gameTime);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            for (int i = 0; i < animation.Count; i++)
            {
                for (int j = 0; j < animation[i].Count; j++)
                {
                    animation[i][j].Draw(spriteBatch);
                }
            }
        }
    }
}
