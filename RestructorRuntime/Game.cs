// Note: this file uses XNA for minimal game rendering.
// It is disabled by default because most people won't have it installed, and MS doesn't support
// it anymore.
// Enable it by uncommenting the line below, and adding the XNA assembly references to the project.

//#define USE_XNA

using System;
using real = System.Double;

#if USE_XNA

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class Game : Microsoft.Xna.Framework.Game
{
    GraphicsDeviceManager graphics;
    public SpriteBatch sb;
    public Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    Action<real> update;
    Action<real> draw;

    public Game(string title, Action<real> _update, Action<real> _draw)
    {
        update = _update;
        draw = _draw;
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = title;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        sb = new SpriteBatch(GraphicsDevice);
    }

    protected override void UnloadContent()
    {
        foreach (var t in textures) t.Value.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyState = Keyboard.GetState();
        // TODO: closes restructor as well, for testing only
        if (keyState.IsKeyDown(Keys.Escape)) Exit();

        MouseState ms = Mouse.GetState();
        //if (ms.LeftButton)

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        sb.Begin();
        draw((real)gameTime.TotalGameTime.TotalSeconds);
        sb.End();

        base.Draw(gameTime);
    }

    public void RenderSprite(string name, real x, real y, real scale, real rot)
    {
        Texture2D sprite;
        if (!textures.TryGetValue(name, out sprite))
        {
            sprite = Texture2D.FromStream(GraphicsDevice,
                new System.IO.StreamReader("..\\..\\Game\\" + name).BaseStream);
            if (sprite == null) return;
            textures[name] = sprite;
        }
        sb.Draw(sprite, new Vector2((float)x, (float)y), null, Color.White, (float)rot,
            new Vector2(sprite.Width / 2, sprite.Height / 2), (float)scale, SpriteEffects.None, 0);
    }
}

#else

using System.Windows;

public class Game
{
    public Game(string title, Action<real> _update, Action<real> _draw) { }

    public void Run()
    {
        MessageBox.Show(
            "This would show a rotating spaceship if you had XNA installed, see top of Game.cs");
    }

    public void RenderSprite(string name, real x, real y, real scale, real rot) { }
}

#endif
