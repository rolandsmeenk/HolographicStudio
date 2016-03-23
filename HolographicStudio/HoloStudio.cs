using System;
using System.Collections.Generic;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.Toolkit.Input;
using System.IO;
using SharpDX;
using RoomAliveToolkit;
using HolographicStudio.Cameras;
using HolographicStudio.Tweakables;
using HolographicStudio.RoomAlive;

namespace HolographicStudio
{
    /// <summary>
    /// Simple game using SharpDX.Toolkit.
    /// </summary>
    public class HoloStudio : Game
    {
        private SpriteBatch _spriteBatch;
        private GraphicsDeviceManager _graphicsDeviceManager;
        private ProjectorCameraEnsemble _ensemble;
        private KeyboardManager _keyboardManager;
        private TweakableManager _tweakableManager;
        private List<RoomAliveGeometryRenderer> _geometryRenderers = new List<RoomAliveGeometryRenderer>();
        private OrbitCamera _orbitCamera;

        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float CenterZ { get; set; }
        public float Floor { get; set; }
        public float Ceiling { get; set; }
        public float Radius { get; set; }
        public bool Animate { get; set; }
        public bool HologramEnabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HoloStudio" /> class.
        /// </summary>
        public HoloStudio()
        {
            // Creates a graphics manager. This is mandatory.
            _graphicsDeviceManager = new GraphicsDeviceManager(this);

            // Setup the relative directory to the executable directory
            // for loading contents with the ContentManager
            Content.RootDirectory = "Content";

            // Initialize input keyboard system
            _keyboardManager = new KeyboardManager(this);
            Services.AddService(_keyboardManager);
            
            // Add service for tweaking
            _tweakableManager = new TweakableManager(this);
            Services.AddService(_tweakableManager);
            _tweakableManager.TextColor = Color.Yellow;

            CenterX = 0;
            CenterY = 0;
            CenterZ = 1;
            Radius = 1;

            _orbitCamera = new OrbitCamera();
            _orbitCamera.Radius = 1.0f;
            _orbitCamera.Speed = 2.0f;
            _orbitCamera.ZoomSpeed = 0.1f;
            Floor = -1;
            Ceiling = 2;
        }

        protected override void Initialize()
        {
            // Modify the title of the window
            Window.Title = "HolographicStudio";
            Window.AllowUserResizing = false;

            // Manually position to fullscreen so we can alt-tab to Visual Studio while debugging
            var window = Window.NativeWindow as System.Windows.Forms.Form;
            if (window != null)
            {
                window.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                window.Location = new System.Drawing.Point(0,0);
                window.Size = new System.Drawing.Size(1920, 1080);
            }

            // load ensemble.xml
            string path = HolographicStudio.Utils.Configuration.EnsembleConfigurationFile;
            string directory = Path.GetDirectoryName(path);
            try
            {
                _ensemble = RoomAliveToolkit.ProjectorCameraEnsemble.FromFile(path);

                // Create geometry renderers for each 3D camera
                foreach (var camera in _ensemble.cameras)
                {
                    string colorImagePath = Path.Combine(directory, string.Format("camera{0}", camera.name), "color.tiff");
                    string depthImagePath = Path.Combine(directory, string.Format("camera{0}", camera.name), "mean.tiff");
                    _geometryRenderers.Add(new RoomAliveGeometryRenderer(this, camera, colorImagePath, depthImagePath));
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("Could not find calibration file.");
            }

            _tweakableManager.AddTweakable(new FloatTweakable("Clip Radius", new PropertyInvoker<float>("Radius", this), 0.01f, 10f, 0.01f));
            _tweakableManager.AddTweakable(new FloatTweakable("Clip CenterX", new PropertyInvoker<float>("CenterX", this), -10f, 10f, 0.01f));
            _tweakableManager.AddTweakable(new FloatTweakable("Clip CenterY", new PropertyInvoker<float>("CenterY", this), -10f, 10f, 0.01f));
            _tweakableManager.AddTweakable(new FloatTweakable("Clip CenterZ", new PropertyInvoker<float>("CenterZ", this),  0f, 10f, 0.01f));
            _tweakableManager.AddTweakable(new FloatTweakable("Clip Floor", new PropertyInvoker<float>("Floor", this), -5f, 5f, 0.01f));
            _tweakableManager.AddTweakable(new FloatTweakable("Clip Ceiling", new PropertyInvoker<float>("Ceiling", this), -5f, 5f, 0.01f));
            _tweakableManager.AddTweakable(new BooleanTweakable("Apply Hologram Effect", new PropertyInvoker<bool>("HologramEnabled", this)));

            for (int i = 0; i < _ensemble.cameras.Count; i++)
            {
                _tweakableManager.AddTweakable(new BooleanTweakable(string.Format("LiveDepth{0}", i), new PropertyInvoker<bool>("LiveDepth", _geometryRenderers[i])));
                _tweakableManager.AddTweakable(new BooleanTweakable(string.Format("LiveColor{0}", i), new PropertyInvoker<bool>("LiveColor", _geometryRenderers[i])));
                _tweakableManager.AddTweakable(new BooleanTweakable(string.Format("FilterDepth{0}", i), new PropertyInvoker<bool>("FilterDepth", _geometryRenderers[i])));
                _tweakableManager.AddTweakable(new FloatTweakable(string.Format("SpatialSigma{0}", i), new PropertyInvoker<float>("SpatialSigma", _geometryRenderers[i]), 0.1f, 10f, 0.1f));
                _tweakableManager.AddTweakable(new FloatTweakable(string.Format("IntensitySigma{0}", i), new PropertyInvoker<float>("IntensitySigma", _geometryRenderers[i]), 1f, 1000f, 1f));
                _tweakableManager.AddTweakable(new FloatTweakable(string.Format("DepthThreshold{0}", i), new PropertyInvoker<float>("DepthThreshold", _geometryRenderers[i]), 0f, 1f, 0.01f));
            }

            _tweakableManager.AddTweakable(new BooleanTweakable("Animate", new PropertyInvoker<bool>("Animate", this)));

            base.Initialize();
        }


        protected override void LoadContent()
        {
            _spriteBatch = ToDisposeContent(new SpriteBatch(GraphicsDevice));

            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            _orbitCamera.SetAspectRatio((float)GraphicsDevice.Viewport.Width / (float)GraphicsDevice.Viewport.Height);

            // Get the current state of the keyboard
            var keyboardState = _keyboardManager.GetState();

            if (keyboardState.IsKeyPressed(Keys.Escape))
            {
                Exit();
            }

            var time = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _orbitCamera.Target = new SharpDX.Vector3(CenterX, CenterY, CenterZ);

            if (Animate)
            {
                _orbitCamera.Rotation = (float)(Math.PI * (0.2 *Math.Sin(0.23 * gameTime.TotalGameTime.TotalSeconds))) ;
                _orbitCamera.UpDown = (float)(Math.PI * (0.1 * Math.Sin(0.11 * gameTime.TotalGameTime.TotalSeconds)));
            }


            if (keyboardState.IsKeyDown(Keys.A))
            {
                _orbitCamera.Left((float)gameTime.ElapsedGameTime.TotalSeconds);
            }
            if (keyboardState.IsKeyDown(Keys.D))
            {
                _orbitCamera.Right((float)gameTime.ElapsedGameTime.TotalSeconds);
            }
            if (keyboardState.IsKeyDown(Keys.W))
            {
                _orbitCamera.Up((float)gameTime.ElapsedGameTime.TotalSeconds);
            }
            if (keyboardState.IsKeyDown(Keys.S))
            {
                _orbitCamera.Down((float)gameTime.ElapsedGameTime.TotalSeconds);
            }
            if (keyboardState.IsKeyDown(Keys.E))
            {
                _orbitCamera.ZoomIn((float)gameTime.ElapsedGameTime.TotalSeconds);
            }
            if (keyboardState.IsKeyDown(Keys.C))
            {
                _orbitCamera.ZoomOut((float)gameTime.ElapsedGameTime.TotalSeconds);
            }            

            // Update renderers to manipulator view
            foreach (var g in _geometryRenderers)
            {
                g.View = _orbitCamera.View;
                g.Projection = _orbitCamera.Projection;

                g.ClipZone = new SharpDX.Vector4(CenterX, CenterY, CenterZ, Radius);
                g.ClipFloor = Floor;
                g.ClipCeiling = Ceiling;
                g.Holographic = HologramEnabled;
            }
           
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // Use time in seconds directly
            var time = (float)gameTime.TotalGameTime.TotalSeconds;

            GraphicsDevice.Clear(Color.Black);

            // Invokes begindraw and draw on all drawable components
            base.Draw(gameTime);
        }
    }
}
