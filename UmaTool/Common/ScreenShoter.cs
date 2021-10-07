using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System.Numerics;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using System.Drawing;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.Media.Ocr;

namespace UmaTool.Common
{
    public struct ResultPath
    {
        public Exception ex;
        public string path;
    };

    // 参考文献: https://docs.microsoft.com/ja-jp/windows/uwp/audio-video-camera/screen-capture

    /// <summary>
    /// スクリーンプレビュー、スクリーンキャプチャ、スクリーンショットを行うクラス
    /// 
    /// 
    /// </summary>
    class ScreenShoter
    {
        // Capture API objects.
        private SizeInt32 _lastSize;
        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;

        // Non-API related members.
        private CanvasDevice _canvasDevice;
        private CompositionGraphicsDevice _compositionGraphicsDevice;
        private Compositor _compositor;
        private CompositionDrawingSurface _surface;
        private CanvasBitmap _currentFrame;

        private GraphicsCapturePicker picker = null;
        private GraphicsCaptureItem item = null;

        private SpriteVisual _visual = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="page"></param>
        /// <param name="position"></param>
        /// <param name="size"></param>
        public void Setup(Page page,Vector3 position, Vector2 size)
        {
            _canvasDevice = new CanvasDevice();

            _compositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(
                Window.Current.Compositor,
                _canvasDevice);

            _compositor = Window.Current.Compositor;

            _surface = _compositionGraphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(16, 16),
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);    // This is the only value that currently works with
                                                    // the composition APIs.

            _visual = _compositor.CreateSpriteVisual();
            _visual.Offset = position;
            _visual.Size = size;

            var brush = _compositor.CreateSurfaceBrush(_surface);
            brush.HorizontalAlignmentRatio = 1f;
            brush.VerticalAlignmentRatio = 1f;
            brush.Stretch = CompositionStretch.Uniform;
            //brush.CenterPoint = new Vector2(1000,1000);
            _visual.Brush = brush;
            ElementCompositionPreview.SetElementChildVisual(page, _visual);
        }

        /// <summary>
        /// 開いているウィンドウ名を取得するメソッド
        /// </summary>
        /// <returns>開いているウィンドウ名</returns>
        public string GetWindowName() {
            if (this.item == null)
            {
                return "";
            }
            else
            {
                return this.item.DisplayName;
            }
        }

        /// <summary>
        /// ウィンドウを選択するタスクを実行する
        /// </summary>
        /// <returns>(タスク)開いたウィンドウ名</returns>
        public async Task<string> PickWindow() {
            // The GraphicsCapturePicker follows the same pattern the
            // file pickers do.
            picker = new GraphicsCapturePicker();
            if (picker == null)
            {
                return "";
            }
            item = await picker.PickSingleItemAsync();

            if (item == null) return "";
            else return item.DisplayName;
        }

        /// <summary>
        /// 非同期のウィンドウプレビューを開始するメソッド
        /// setup、ウィンドウ指定が完了してる状態でなければならない
        /// </summary>
        public void StartCaptureAsync()
        {
            if (picker == null || item == null)
            {
                BaseCommonMethods.ToastSimpleMessage("ウィンドウが指定されていません", toastType: MessageType.Caution);
                return;
            }

            // The item may be null if the user dismissed the
            // control without making a selection or hit Cancel.
            if (item != null)
            {
                StartCaptureInternal(this.item);
            }
        }

        /// <summary>
        /// キャプチャの非同期タスクを入れ込み、投影する
        /// </summary>
        /// <param name="item"></param>
        private void StartCaptureInternal(GraphicsCaptureItem item)
        {
            // Stop the previous capture if we had one.
            StopCaptureLite();

            _item = item;
            _lastSize = _item.Size;

            _framePool = Direct3D11CaptureFramePool.Create(
               _canvasDevice, // D3D device
               DirectXPixelFormat.B8G8R8A8UIntNormalized, // Pixel format
               2, // Number of frames
               _item.Size); // Size of the buffers

            _framePool.FrameArrived += (s, a) =>
            {
                // The FrameArrived event is raised for every frame on the thread
                // that created the Direct3D11CaptureFramePool. This means we
                // don't have to do a null-check here, as we know we're the only
                // one dequeueing frames in our application.  

                // NOTE: Disposing the frame retires it and returns  
                // the buffer to the pool.

                using (var frame = _framePool.TryGetNextFrame())
                {
                    ProcessFrame(frame);
                }
            };

            _item.Closed += (s, a) =>
            {
                StopCapture();
            };

            _session = _framePool.CreateCaptureSession(_item);
            _session.IsCursorCaptureEnabled = false;
            _session.StartCapture();
        }

        /// <summary>
        /// キャプチャを強制終了するメソッド
        /// ただし、ウィンドウ指定した情報までは放棄しない
        /// </summary>
        public void StopCaptureLite()
        {
            this._session?.Dispose();
            this._framePool?.Dispose();
            this._item = null;
            this._session = null;
            this._framePool = null;
        }

        /// <summary>
        /// キャプチャを強制終了するメソッド
        /// </summary>
        public void StopCapture() {
            StopCaptureLite();
            this.item = null;
            this.picker = null;
        }

        private void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            // Resize and device-lost leverage the same function on the
            // Direct3D11CaptureFramePool. Refactoring it this way avoids
            // throwing in the catch block below (device creation could always
            // fail) along with ensuring that resize completes successfully and
            // isn’t vulnerable to device-lost.
            bool needsReset = false;
            bool recreateDevice = false;

            if ((frame.ContentSize.Width != _lastSize.Width) ||
                (frame.ContentSize.Height != _lastSize.Height))
            {
                needsReset = true;
                _lastSize = frame.ContentSize;
            }

            try
            {
                // Take the D3D11 surface and draw it into a  
                // Composition surface.

                // Convert our D3D11 surface into a Win2D object.
                CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(
                    _canvasDevice,
                    frame.Surface);

                _currentFrame = canvasBitmap;

                // Helper that handles the drawing for us.
                FillSurfaceWithBitmap(canvasBitmap);
            }

            // This is the device-lost convention for Win2D.
            catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
            {
                // We lost our graphics device. Recreate it and reset
                // our Direct3D11CaptureFramePool.  
                needsReset = true;
                recreateDevice = true;
            }

            if (needsReset)
            {
                ResetFramePool(frame.ContentSize, recreateDevice);
            }
        }

        private void FillSurfaceWithBitmap(CanvasBitmap canvasBitmap)
        {
            CanvasComposition.Resize(_surface, canvasBitmap.Size);

            using (var session = CanvasComposition.CreateDrawingSession(_surface))
            {
                session.Clear(Colors.Transparent);
                session.DrawImage(canvasBitmap);
            }
        }

        private void ResetFramePool(SizeInt32 size, bool recreateDevice)
        {
            do
            {
                try
                {
                    if (recreateDevice)
                    {
                        _canvasDevice = new CanvasDevice();
                    }

                    _framePool.Recreate(
                        _canvasDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        size);
                }
                // This is the device-lost convention for Win2D.
                catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
                {
                    _canvasDevice = null;
                    recreateDevice = true;
                }
            } while (_canvasDevice == null);
        }

        public bool isAvarableScreenShot() {
            return this._currentFrame != null;
        }

        /// <summary>
        /// スクリーンショットを撮影するタスク
        /// Exception型で返す
        /// nullなら成功、例外があればそのまま返す
        /// </summary>
        /// <param name="pathObject"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<ResultPath> Screenshot(StorageFolder pathObject, string fileName)
        {
            try
            {
                return new ResultPath() {
                    ex = null,
                    path = await SaveImageAsync(pathObject, fileName, _currentFrame)
                };
            }
            catch(Exception ex)
            {
                return new ResultPath()
                {
                    ex = ex,
                    path = ""
                };
            }
        }

        private async Task<string> SaveImageAsync(StorageFolder pathObject,string fileName, CanvasBitmap frame, Boolean isOverwrite = false)
        {
            StorageFile file;
            if (isOverwrite)
            {
                file = await pathObject.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            }
            else
            {
                file = await pathObject.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
            }

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await frame.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
            }

            return file.Path;

        }

        /// <summary>
        /// 現在表示中の画面をBitmap形式で返す
        /// </summary>
        /// <returns></returns>
        public Bitmap GetCurrentBitMap() {
            var ms = new MemoryStream(
                _currentFrame.GetPixelBytes(),
                (int)Math.Floor(_currentFrame.Bounds.Width),
                (int)Math.Floor(_currentFrame.Bounds.Height)
                );
            return new Bitmap(ms);
        }

        /// <summary>
        /// 現在表示中の画面を切り抜いてSoftwareBitmap形式で返す
        /// </summary>
        /// <param name="left">取得する範囲の左端</param>
        /// <param name="top">取得する範囲の上端</param>
        /// <param name="width">取得する範囲の幅</param>
        /// <param name="height">取得する範囲の高さ</param>
        /// <returns></returns>
        public SoftwareBitmap GetCurrentSoftwareBitMap(int left = -1, int top = -1,int width = -1, int height = -1 )
        {
            var fWidth = (int)Math.Floor(this.frameWidth);
            var fHeight = (int)Math.Floor(this.frameHeight);

            if (left < 0 && top< 0 && width < 0 && height < 0)
            {
                //全引数省略時
                return CommonMethods.BytesToSoftwareBMP(
                    _currentFrame.GetPixelBytes(),
                    fWidth,
                    fHeight
                    );
            }
            else
            {   
                // 省略されてなかった場合、値を加工する
                left = left < 0 ? 0 : left;
                top = top < 0 ? 0 : top;
                width = width < 0 ? fWidth - left : width;
                height = height < 0 ? fHeight - top : height;

                //例外が発生した場合
                if (width < 0) throw new ArgumentException("左端が画像の幅を超えています");
                if (height < 0) throw new ArgumentException("上端が画像の高さを超えています");
                if (left + width > fWidth) width = fWidth - left;
                if (top + height > fHeight) width = fHeight - top;

                //配列の取得
                var bytes = _currentFrame.GetPixelBytes();

                //配列の再生成(幅x高さxビット深)
                var croppedBytes = new Byte[width * height * 4];

                //再生成した配列にクリッピング後のデータを格納
                int x;
                for (int y = 0; y < height; y++) {
                    for (x = 0; x < width; x++)
                    {
                        //BGRAの順番に各8bit
                        croppedBytes[y * width * 4 + x * 4] = bytes[(top + y) * fWidth * 4 + (left + x) * 4];
                        croppedBytes[y * width * 4 + x * 4 + 1] = bytes[(top + y) * fWidth * 4 + (left + x) * 4 + 1];
                        croppedBytes[y * width * 4 + x * 4 + 2] = bytes[(top + y) * fWidth * 4 + (left + x) * 4 + 2];
                        croppedBytes[y * width * 4 + x * 4 + 3] = bytes[(top + y) * fWidth * 4 + (left + x) * 4 + 3];
                    }
                }

                return CommonMethods.BytesToSoftwareBMP(
                    croppedBytes,
                    width,
                    height
                    );
            }

        }

        /// <summary>
        /// キャプチャ対象のウィンドウの実体フレーム幅
        /// </summary>
        public double frameWidth
        {
            get { return this._currentFrame.Bounds.Width; }
        }

        /// <summary>
        /// キャプチャ対象のウィンドウの実体フレーム高
        /// </summary>
        public double frameHeight
        {
            get { return this._currentFrame.Bounds.Height; }
        }

    }
}
