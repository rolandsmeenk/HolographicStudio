
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using SharpDX.WIC;
using System.Threading.Tasks;
using HolographicStudio.Utils;

namespace HolographicStudio.RoomAlive
{
    /// <summary>
    /// Renders the RoomAlive geometry coming from a Kinect server
    /// </summary>
    public class RoomAliveGeometryRenderer : GameSystem
    {
        private SpriteBatch _spriteBatch;

        bool firstIsUpdating = false;
        // CPU writeable color image
        protected SharpDX.Toolkit.Graphics.Texture _tex2DColorImage1;
        protected SharpDX.Toolkit.Graphics.Texture _tex2DColorImage2;
        // CPU writeable depth image
        protected SharpDX.Toolkit.Graphics.Texture _uintDepthImage1;
        protected SharpDX.Toolkit.Graphics.Texture _uintDepthImage2;
        public ushort[] depthShortBuffer = new ushort[RoomAliveToolkit.Kinect2Calibration.depthImageWidth * RoomAliveToolkit.Kinect2Calibration.depthImageHeight];

        // Final float depth
        protected RenderTarget2D _floatDepthImageFinal;
        // Float depth image for pingpong render passes
        protected RenderTarget2D _floatDepthImageSubpass;

        protected GeometricPrimitive<VertexPosition> _geometry;
        protected Effect _roomAliveEffect;
        protected Effect _fromUintEffect;
        protected Effect _gaussianEffect;

        protected SharpDX.Toolkit.Graphics.SamplerState _colorSamplerState;
        protected RoomAliveToolkit.ProjectorCameraEnsemble.Camera _camera;

        private string _colorImageFilePath;
        private string _depthImageFilePath;

        /// World matrix is the position of the camera wrt the first Kinect camera. First Kinect will therefore be Identity.
        protected Matrix _world;

        public Matrix View { get; set; }
        public Matrix Projection { get; set; }
        public ViewportF TargetViewport { get; set; }

        /// <summary>
        /// Offset matrix to place world coordinate at differenct center than first Kinect and align with floor. Usefull for shaders that use real world coordinates.
        /// </summary>
        public Matrix Offset { get; set; }

        public bool LiveDepth { get; set; }
        public bool LiveColor { get; set; }
        public bool Wireframe { get; set; }
        public bool FilterDepth { get; set; }
        public float SpatialSigma { get; set; }
        public float IntensitySigma { get; set; }
        public float DepthThreshold {get;set; }

        public Vector4 ClipZone { get; set; }
        public float ClipFloor { get; set; }
        public float ClipCeiling { get; set; }
        public bool Holographic { get; set; }

        private int ConnectionRetries = 5;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game"></param>
        public RoomAliveGeometryRenderer(Game game, RoomAliveToolkit.ProjectorCameraEnsemble.Camera camera, string colorImageFilePath, string depthImageFilePath) :
            base(game)
        {
            _camera = camera;
            _colorImageFilePath = colorImageFilePath;
            _depthImageFilePath = depthImageFilePath;

            // this game system has something to draw - enable drawing by default
            // this can be disabled to make objects drawn by this system disappear
            Visible = true;

            // this game system has logic that needs to be updated - enable update by default
            // this can be disabled to simulate a "pause" in logic update
            Enabled = true;

            // add the system itself to the systems list, so that it will get initialized and processed properly
            // this can be done after game initialization - the Game class supports adding and removing of game systems dynamically
            game.GameSystems.Add(this);

            LiveDepth = true;
            LiveColor = true;
            FilterDepth = true;
            SpatialSigma = 2f;
            IntensitySigma = 20f;

            View = Matrix.Identity;
            Projection = Matrix.Identity;
            ClipZone = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
            ClipFloor = -5f;
            ClipCeiling = 5f;
            DepthThreshold = 0.1f;
        }

        private VertexPosition[] _vertexArray;
        private int[] _indexArray;
        private SharpDX.WIC.ImagingFactory _imagingFactory;
        private Rectangle _depthRectangle;

        protected override void LoadContent()
        {
            _spriteBatch = ToDisposeContent(new SpriteBatch(GraphicsDevice));
            _depthRectangle = new Rectangle(0, 0, RoomAliveToolkit.Kinect2Calibration.depthImageWidth, RoomAliveToolkit.Kinect2Calibration.depthImageHeight);

            int depthWidth = RoomAliveToolkit.Kinect2Calibration.depthImageWidth;
            int depthHeight = RoomAliveToolkit.Kinect2Calibration.depthImageHeight;

            _roomAliveEffect = Content.Load<Effect>("Holographic");
            _fromUintEffect = Content.Load<Effect>("FromUint");
            _gaussianEffect = Content.Load<Effect>("BilateralFilter");

            _colorSamplerState = ToDisposeContent(SharpDX.Toolkit.Graphics.SamplerState.New(this.GraphicsDevice, new SamplerStateDescription()
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Border,
                AddressV = TextureAddressMode.Border,
                AddressW = TextureAddressMode.Border,
                BorderColor = new SharpDX.Color4(0.5f, 0.5f, 0.5f, 1.0f),
                ComparisonFunction = Comparison.Never,
                MaximumAnisotropy = 16,
                MipLodBias = 0,
                MinimumLod = -float.MaxValue,
                MaximumLod = float.MaxValue
            }));

            List<VertexPosition> vertices = new List<VertexPosition>();
            List<int> indices = new List<int>();

            // vertex buffer
            if (_camera.calibration != null)
            {
                var table = _camera.calibration.ComputeDepthFrameToCameraSpaceTable();
                for (int i = 0; i < depthHeight; i++)
                {
                    for (int j = 0; j < depthWidth; j++)
                    {
                        var point = table[RoomAliveToolkit.Kinect2Calibration.depthImageWidth * i + j];

                        vertices.Add(new VertexPosition(new SharpDX.Vector4(point.X, point.Y, j, i)));
                    }
                }


                for (int i = 0; i < depthHeight - 1; i++)
                {
                    for (int j = 0; j < depthWidth - 1; j++)
                    {
                        int baseIndex = depthWidth * i + j;
                        indices.Add(baseIndex);
                        indices.Add(baseIndex + depthWidth + 1);
                        indices.Add(baseIndex + 1);

                        indices.Add(baseIndex);
                        indices.Add(baseIndex + depthWidth);
                        indices.Add(baseIndex + depthWidth + 1);
                    }
                }            

                _vertexArray = vertices.ToArray();
                _indexArray = indices.ToArray();

                // build the plane geometry of the specified size and subdivision segments
                _geometry = ToDisposeContent(new GeometricPrimitive<VertexPosition>(GraphicsDevice, _vertexArray, _indexArray, true));
            }
            else
            {
                Console.WriteLine("Camera '{0}' is missing calibration", _camera.name);
                Visible = false;
                Enabled = false;
            }

            if (!string.IsNullOrEmpty(_colorImageFilePath))
            {
                _tex2DColorImage1 = SharpDX.Toolkit.Graphics.Texture2D.Load(GraphicsDevice, _colorImageFilePath, TextureFlags.ShaderResource, ResourceUsage.Dynamic);
                _tex2DColorImage2 = SharpDX.Toolkit.Graphics.Texture2D.Load(GraphicsDevice, _colorImageFilePath, TextureFlags.ShaderResource, ResourceUsage.Dynamic);
            }

            _imagingFactory = new SharpDX.WIC.ImagingFactory();
            if (!string.IsNullOrEmpty(_depthImageFilePath))
            {
                var depthImage = new RoomAliveToolkit.ShortImage(RoomAliveToolkit.Kinect2Calibration.depthImageWidth, RoomAliveToolkit.Kinect2Calibration.depthImageHeight);
                RoomAliveToolkit.ProjectorCameraEnsemble.LoadFromTiff(_imagingFactory, depthImage, _depthImageFilePath);

                UpdateDepthImage(this.GraphicsDevice, depthImage.DataIntPtr);
            }

            var floatDepthImageTextureDesc = new Texture2DDescription()
            {
                Width = depthWidth,
                Height = depthHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.R32_Float,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
            };

            _floatDepthImageFinal = ToDisposeContent(RenderTarget2D.New(GraphicsDevice, floatDepthImageTextureDesc));
            _floatDepthImageSubpass = ToDisposeContent(RenderTarget2D.New(GraphicsDevice, floatDepthImageTextureDesc));

            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();
        }

        private void CheckDepthTexture()
        {
            if (depthImageTexture == null)
            {
                var depthImageTextureDesc = new Texture2DDescription()
                {
                    Width = RoomAliveToolkit.Kinect2Calibration.depthImageWidth,
                    Height = RoomAliveToolkit.Kinect2Calibration.depthImageHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = SharpDX.DXGI.Format.R16_UInt,
                    SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                    Usage = ResourceUsage.Dynamic,
                    BindFlags = BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.Write,
                };
                depthImageTexture = new SharpDX.Direct3D11.Texture2D(GraphicsDevice, depthImageTextureDesc);

                // Create toolkit texture that wraps the DX Texture
                _uintDepthImage1 = SharpDX.Toolkit.Graphics.Texture2D.New(GraphicsDevice, depthImageTexture);
                _uintDepthImage2 = SharpDX.Toolkit.Graphics.Texture2D.New(GraphicsDevice, depthImageTexture);
            }
        }

        public SharpDX.Direct3D11.Texture2D depthImageTexture;
        public void UpdateDepthImage(DeviceContext deviceContext, IntPtr depthImage)
        {
            CheckDepthTexture();

            DataStream dataStream;
            deviceContext.MapSubresource(depthImageTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out dataStream);
            dataStream.WriteRange(depthImage, RoomAliveToolkit.Kinect2Calibration.depthImageWidth * RoomAliveToolkit.Kinect2Calibration.depthImageHeight * 2);
            deviceContext.UnmapSubresource(depthImageTexture, 0);
            _depthImageChanged = true;
        }

        public void UpdateDepthImage(DeviceContext deviceContext, byte[] depthImage)
        {
            CheckDepthTexture();

            DataStream dataStream;
            deviceContext.MapSubresource(depthImageTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out dataStream);
            dataStream.WriteRange<byte>(depthImage, 0, RoomAliveToolkit.Kinect2Calibration.depthImageWidth * RoomAliveToolkit.Kinect2Calibration.depthImageHeight * 2);
            deviceContext.UnmapSubresource(depthImageTexture, 0);
            _depthImageChanged = true;
        }

        private void CheckColorTexture()
        {
            if (_tex2DColorImage1 == null)
            {
                Texture2DDescription description = new Texture2DDescription()
                {
                    Width = RoomAliveToolkit.Kinect2Calibration.colorImageWidth,
                    Height = RoomAliveToolkit.Kinect2Calibration.colorImageHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    Usage = ResourceUsage.Dynamic,
                    BindFlags = BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    SampleDescription = new SharpDX.DXGI.SampleDescription(count: 1, quality: 0)
                };

                _tex2DColorImage1 = SharpDX.Toolkit.Graphics.Texture2D.New(GraphicsDevice, description);
                _tex2DColorImage2 = SharpDX.Toolkit.Graphics.Texture2D.New(GraphicsDevice, description);
            }
        }

        public void UpdateColorImage(DeviceContext deviceContext, byte[] colorImage)
        {
            CheckColorTexture();

            DataStream dataStream;
            if (firstIsUpdating)
            {
                deviceContext.MapSubresource(_tex2DColorImage1, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out dataStream);
                dataStream.WriteRange<byte>(colorImage, 0, RoomAliveToolkit.Kinect2Calibration.colorImageWidth * RoomAliveToolkit.Kinect2Calibration.colorImageHeight * 4);
                deviceContext.UnmapSubresource(_tex2DColorImage1, 0);
            }
            else
            {
                deviceContext.MapSubresource(_tex2DColorImage2, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out dataStream);
                dataStream.WriteRange<byte>(colorImage, 0, RoomAliveToolkit.Kinect2Calibration.colorImageWidth * RoomAliveToolkit.Kinect2Calibration.colorImageHeight * 4);
                deviceContext.UnmapSubresource(_tex2DColorImage2, 0);
            }
        }



        bool updating = false;
        byte[] nextColorData = new byte[4 * RoomAliveToolkit.Kinect2Calibration.colorImageWidth * RoomAliveToolkit.Kinect2Calibration.colorImageHeight];
        public override async void Update(GameTime gameTime)
        {
            if (!updating)
            {
                updating = true;
                if (LiveDepth)
                {
                    await UpdateDepthTexture();
                }

                if (LiveColor)
                { 
                    await UpdateColorTexture();
                }
                updating = false;
                firstIsUpdating = !firstIsUpdating;
            }

            base.Update(gameTime);
        }

        private bool _depthImageChanged = false;
        private int _connectionFailCount = 0;

        private async Task UpdateDepthTexture()
        {
            if (_connectionFailCount > ConnectionRetries)
            {
                LiveDepth = false;
                return;
            }

            try
            {
                var nextDepthData = await _camera.Client.LatestDepthImageAsync();
                UpdateDepthImage(GraphicsDevice, nextDepthData);
            }
            catch (System.ServiceModel.EndpointNotFoundException ex)
            {
                _connectionFailCount++;
                Console.WriteLine("Could not connect to Kinect for live depth. Start Kinect server. Tried: {0}", _connectionFailCount);

                await Task.Delay(_connectionFailCount * 2);
            }
            catch (System.ServiceModel.CommunicationException)
            {
                Console.WriteLine("Connection to Kinect for live depth was lost. Restart Kinect server and the application.");
                LiveDepth = false;
            }
        }

        private async Task UpdateColorTexture()
        {
            try
            {
                var encodedColorData = await _camera.Client.LatestJPEGImageAsync();

                // decode JPEG
                var memoryStream = new MemoryStream(encodedColorData);

                var stream = new WICStream(_imagingFactory, memoryStream);
                // decodes to 24 bit BGR
                var decoder = new SharpDX.WIC.BitmapDecoder(_imagingFactory, stream, SharpDX.WIC.DecodeOptions.CacheOnLoad);
                var bitmapFrameDecode = decoder.GetFrame(0);

                // convert to 32 bpp
                var formatConverter = new FormatConverter(_imagingFactory);
                formatConverter.Initialize(bitmapFrameDecode, SharpDX.WIC.PixelFormat.Format32bppBGR);
                formatConverter.CopyPixels(nextColorData, RoomAliveToolkit.Kinect2Calibration.colorImageWidth * 4);

                UpdateColorImage(GraphicsDevice, nextColorData);
                memoryStream.Close();
                memoryStream.Dispose();
                stream.Dispose();
                decoder.Dispose();
                formatConverter.Dispose();
                bitmapFrameDecode.Dispose();
            }
            catch (System.ServiceModel.EndpointNotFoundException ex)
            {
                // TODO Message
                LiveColor = false;
                Console.WriteLine("Could not connect to Kinect for live color. Start Kinect server.");
            }
            catch (System.ServiceModel.CommunicationException)
            {
                Console.WriteLine("Connection to Kinect server for live color was lost. Restart Kinect server and the application.");
                LiveDepth = false;
            }
        }

        protected virtual void PrepareEffectParameters()
        {
            _world = new SharpDX.Matrix();
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    _world[i, j] = (float)_camera.pose[j, i];

            // view and projection matrix are post-multiply
            var worldViewProjection = _world * View * Projection;

            Matrix depthToColorTransform = new Matrix();
            for (int i = 0, col = 0; col < 4; col++)
            {
                for (int row = 0; row < 4; row++)
                {
                    depthToColorTransform[i] = (float)_camera.calibration.depthToColorTransform[col, row];
                    i++;
                }
            }

            _roomAliveEffect.Parameters["depthToColor"].SetValue(depthToColorTransform);
            _roomAliveEffect.Parameters["f"].SetValue(new Vector2((float)_camera.calibration.colorCameraMatrix[0, 0], (float)_camera.calibration.colorCameraMatrix[1, 1]));
            _roomAliveEffect.Parameters["c"].SetValue(new Vector2((float)_camera.calibration.colorCameraMatrix[0, 2], (float)_camera.calibration.colorCameraMatrix[1, 2]));
            _roomAliveEffect.Parameters["k1"].SetValue((float)_camera.calibration.colorLensDistortion[0]);
            _roomAliveEffect.Parameters["k2"].SetValue((float)_camera.calibration.colorLensDistortion[1]);
            _roomAliveEffect.Parameters["clipzone"].SetValue(ClipZone);
            _roomAliveEffect.Parameters["clipfloor"].SetValue(ClipFloor);
            _roomAliveEffect.Parameters["clipceiling"].SetValue(ClipCeiling);
            _roomAliveEffect.Parameters["depthThreshold"].SetValue(DepthThreshold);
            _roomAliveEffect.Parameters["hologramEnabled"].SetValue(Holographic);
            _roomAliveEffect.DefaultParameters.WorldViewProjectionParameter.SetValue(worldViewProjection);
            _roomAliveEffect.DefaultParameters.WorldParameter.SetValue(_world);

            if (_uintDepthImage1 != null && _uintDepthImage2 != null && _floatDepthImageFinal != null)
            {
                _roomAliveEffect.Parameters["depthTexture"].SetResource(_floatDepthImageFinal);
            }

            if (_tex2DColorImage1 != null && _tex2DColorImage2 != null)
            {
                _roomAliveEffect.Parameters["colorSampler"].SetResource(_colorSamplerState);
                if (firstIsUpdating)
                    _roomAliveEffect.Parameters["colorTexture"].SetResource(_tex2DColorImage2);
                else
                    _roomAliveEffect.Parameters["colorTexture"].SetResource(_tex2DColorImage1);
            }
        }

        public override bool BeginDraw()
        {
            // Filter depth image in 3 passes, convert to unsigned int
            if (_depthImageChanged)
            {
                // Convert Uint texture to float texture
                GraphicsDevice.SetRenderTargets(GraphicsDevice.DepthStencilBuffer, _floatDepthImageFinal);
                // NOTE: This clearing the rendertarget first solves a BUG on the target rendering machine, where depth of previous images was accumulated
                GraphicsDevice.Clear(_floatDepthImageFinal, Color.Black);
                _spriteBatch.Begin(SpriteSortMode.Deferred, _fromUintEffect);
                if (firstIsUpdating)
                    _spriteBatch.Draw(_uintDepthImage2, _depthRectangle, Color.White);
                else
                    _spriteBatch.Draw(_uintDepthImage1, _depthRectangle, Color.White);
                _spriteBatch.End();

                if (FilterDepth)
                {
                    // Filter depth image first pass
                    GraphicsDevice.SetRenderTargets(GraphicsDevice.DepthStencilBuffer, _floatDepthImageSubpass);
                    GraphicsDevice.Clear(_floatDepthImageSubpass, Color.Black);
                    _gaussianEffect.Parameters["spatialSigma"].SetValue(1f / SpatialSigma);
                    _gaussianEffect.Parameters["intensitySigma"].SetValue(1f / IntensitySigma);
                    _spriteBatch.Begin(SpriteSortMode.Deferred, _gaussianEffect);
                    _spriteBatch.Draw(_floatDepthImageFinal, _depthRectangle, Color.White);
                    _spriteBatch.End();

                    // Second pass back into the final depth image
                    GraphicsDevice.SetRenderTargets(GraphicsDevice.DepthStencilBuffer, _floatDepthImageFinal);
                    GraphicsDevice.Clear(_floatDepthImageFinal, Color.Black);
                    _spriteBatch.Begin(SpriteSortMode.Deferred, _gaussianEffect);
                    _spriteBatch.Draw(_floatDepthImageSubpass, _depthRectangle, Color.White);
                    _spriteBatch.End();
                }

                _depthImageChanged = false;
            }
            GraphicsDevice.SetRenderTargets(GraphicsDevice.DepthStencilBuffer, GraphicsDevice.BackBuffer);

            return base.BeginDraw();
        }

        public override void Draw(GameTime gameTime)
        {
            // If no TargetViewPort specified GraphicsDevice.AutoViewportFromRenderTargets has already set the correct viewport based on the size of the rendertarget
            if (TargetViewport.Width > 0 && TargetViewport.Height > 0)
            {
                GraphicsDevice.SetViewport(TargetViewport);
            }

            if (Holographic)
                GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.Additive);
            else
                GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.Default);

            GraphicsDevice.SetDepthStencilState(GraphicsDevice.DepthStencilStates.Default);
            GraphicsDevice.SetRasterizerState(GraphicsDevice.RasterizerStates.CullFront);

            PrepareEffectParameters();

            _geometry.Draw(_roomAliveEffect);

            base.Draw(gameTime);
        }
    }
}
