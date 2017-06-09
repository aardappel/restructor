using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using real = System.Double;

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
}
