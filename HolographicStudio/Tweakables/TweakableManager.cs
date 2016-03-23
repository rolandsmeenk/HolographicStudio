using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.Toolkit.Input;
using System;
using System.Collections.Generic;

namespace HolographicStudio.Tweakables
{
    /// <summary>
    /// GameSystem for 
    /// </summary>
    public class TweakableManager : GameSystem, ITweakableService
    {
        private SpriteBatch spriteBatch;
        private SpriteFont arial16Font;
        private int _currentTweakable = -1;

        private List<Tweakable> _tweakables = new List<Tweakable>();
        private KeyboardManager keyboard;

        public Color TextColor { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game"></param>
        public TweakableManager(Game game) :
            base(game)
        {
            // Initially invisible, but can be toggled with [T]
            Visible = false;

            // this game system has logic that needs to be updated - enable update by default
            // this can be disabled to simulate a "pause" in logic update
            Enabled = true;

            // add the system itself to the systems list, so that it will get initialized and processed properly
            // this can be done after game initialization - the Game class supports adding and removing of game systems dynamically
            game.GameSystems.Add(this);

            DrawOrder = 99999;
            keyboard = game.Services.GetService<KeyboardManager>();
            TextColor = Color.Magenta;
        }

        public void AddTweakable(Tweakable tweakable)
        {
            if (_tweakables.Find(t => t.Name == tweakable.Name) != null)
            {
                throw new InvalidOperationException(string.Format("Tweakable with name '{0}' already exists", tweakable.Name));
            }

            _tweakables.Add(tweakable);
            if (_currentTweakable == -1)
            {
                _currentTweakable = 0;
            }
        }

        public override async void Update(GameTime gameTime)
        {
            KeyboardState keyboardState = keyboard.GetState();

            // Switch visibility
            if (keyboardState.IsKeyPressed(Keys.T))
            {
                Visible = !Visible;
            }

            if (Visible)
            {
                if (keyboardState.IsKeyPressed(Keys.Down))
                {
                    if (keyboardState.IsKeyDown(Keys.Shift))
                    {
                        for (int i = 0; i < 10; i++)
                            Down();
                    }
                    else
                    {
                        Down();
                    }
                }

                if (keyboardState.IsKeyPressed(Keys.Up))
                {
                    if (keyboardState.IsKeyDown(Keys.Shift))
                    {
                        for (int i = 0; i < 10; i++)
                            Up();
                    }
                    else
                    {
                        Up();
                    }
                }

                if (keyboardState.IsKeyPressed(Keys.Left))
                {
                    Previous();
                }

                if (keyboardState.IsKeyPressed(Keys.Right))
                {
                    Next();
                }
            }


            base.Update(gameTime);
        }

        private void Down()
        {
            if (_currentTweakable >= 0 && !_tweakables[_currentTweakable].ReadOnly)
            {
                _tweakables[_currentTweakable].Down();
            }
        }

        private void Up()
        {
            if (_currentTweakable >= 0 && !_tweakables[_currentTweakable].ReadOnly)
            {
                _tweakables[_currentTweakable].Up();
            }
        }

        private void Next()
        {
            if (_currentTweakable >= 0)
            {
                if (!_tweakables[_currentTweakable].Next())
                {
                    _currentTweakable++;
                    if (_currentTweakable > _tweakables.Count - 1)
                    {
                        _currentTweakable = 0;
                    }
                }
            }
        }

        private void Previous()
        {
            if (_currentTweakable >= 0)
            {
                if (!_tweakables[_currentTweakable].Previous())
                {
                    _currentTweakable--;
                    if (_currentTweakable < 0)
                    {
                        _currentTweakable = _tweakables.Count - 1;
                    }
                }
            }
        }

        protected override void LoadContent()
        {
            // Instantiate a SpriteBatch
            spriteBatch = ToDisposeContent(new SpriteBatch(GraphicsDevice));

            // Loads a sprite font
            // The [Arial16.xml] file is defined with the build action [ToolkitFont] in the project
            arial16Font = Content.Load<SpriteFont>("Arial24");

            base.LoadContent();
        }

        public override void EndDraw()
        {
            Vector2 textPosition = new Vector2(32, Game.GraphicsDevice.Viewport.Height - 3.6f * arial16Font.LineSpacing);

            if ((_currentTweakable >= 0) && (_currentTweakable < _tweakables.Count))
            {
                string tweakableString = string.Format("{0}/{1} {2} : {3}", (_currentTweakable + 1),
                                                                            _tweakables.Count,
                                                                            _tweakables[_currentTweakable].Name,
                                                                            _tweakables[_currentTweakable].ValueAsString());

                spriteBatch.Begin();
                spriteBatch.DrawString(arial16Font,
                    tweakableString,
                    textPosition,
                    TextColor,
                    0f,
                    Vector2.Zero,
                    1.0f,
                    SpriteEffects.None,
                    0);

                spriteBatch.End();
            }
        }

        public bool IsVisible()
        {
            return Visible;
        }
    }  
}
