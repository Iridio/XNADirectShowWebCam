using DirectShowLib;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace WindowsGame1
{
  public class VideoCapture : ISampleGrabberCB, IDisposable
  {
    public VideoCapture(GraphicsDevice GraphicsDevice)
    {
      this.GraphicsDevice = GraphicsDevice;
      Initialize();
    }

    public Texture2D Frame
    {
      get
      {
        if (frame.GraphicsDevice.Textures[0] == frame)
          frame.GraphicsDevice.Textures[0] = null;
        lock (lockObj)
        {
          frame.SetData<byte>(FrameRGBA);
        }
        return frame;
      }
    }

    protected ICaptureGraphBuilder2 CaptureGraphBuilder;
    protected Texture2D frame;
    protected byte[] FrameBGR;
    protected byte[] FrameRGBA;
    protected bool FrameReady;
    protected IGraphBuilder GraphBuilder;
    protected GraphicsDevice GraphicsDevice;
    protected IMediaControl MediaControl;
    protected ISampleGrabber SampleGrabber;
    protected Thread UpdateThread;
    protected int Width = 640;
    protected int Height = 480;
    protected int DEVICE_ID = 0;
    bool isRunning;
    static readonly object lockObj = new object();

    protected void Initialize()
    {
      FrameReady = false;
      frame = new Texture2D(GraphicsDevice, Width, Height, false, SurfaceFormat.Color);
      FrameBGR = new byte[(Width * Height) * 3];
      FrameRGBA = new byte[(Width * Height) * 4];
      GraphBuilder = (IGraphBuilder)new FilterGraph();
      CaptureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
      MediaControl = (IMediaControl)GraphBuilder;
      CaptureGraphBuilder.SetFiltergraph(GraphBuilder);
      object VideoInputObject = null;
      IBaseFilter VideoInput = null;
      IEnumMoniker classEnum;
      ICreateDevEnum devEnum = (ICreateDevEnum)new CreateDevEnum();
      devEnum.CreateClassEnumerator(FilterCategory.VideoInputDevice, out classEnum, 0);
      Marshal.ReleaseComObject(devEnum);
      if (classEnum != null)
      {
        IMoniker[] moniker = new IMoniker[1];
        if (classEnum.Next(moniker.Length, moniker, IntPtr.Zero) == DEVICE_ID)
        {
          Guid iid = typeof(IBaseFilter).GUID;
          moniker[0].BindToObject(null, null, ref iid, out VideoInputObject);
        }
        Marshal.ReleaseComObject(moniker[0]);
        Marshal.ReleaseComObject(classEnum);
        VideoInput = (IBaseFilter)VideoInputObject;
      }
      if (VideoInput != null)
      {
        isRunning = true;
        SampleGrabber = new SampleGrabber() as ISampleGrabber;
        GraphBuilder.AddFilter((IBaseFilter)SampleGrabber, "Render");
        AMMediaType Type = new AMMediaType() { majorType = MediaType.Video, subType = MediaSubType.RGB24, formatType = FormatType.VideoInfo };
        SampleGrabber.SetMediaType(Type);
        GraphBuilder.AddFilter(VideoInput, "Camera");
        SampleGrabber.SetBufferSamples(false);
        SampleGrabber.SetOneShot(false);
        SampleGrabber.GetConnectedMediaType(new AMMediaType());
        SampleGrabber.SetCallback((ISampleGrabberCB)this, 1);
        CaptureGraphBuilder.RenderStream(PinCategory.Preview, MediaType.Video, VideoInput, null, SampleGrabber as IBaseFilter);
        UpdateThread = new Thread(UpdateBuffer);
        UpdateThread.Start();
        MediaControl.Run();
        Marshal.ReleaseComObject(VideoInput);
      }
    }

    public void Dispose()
    {
      isRunning = false;
      Thread.Sleep(100); //My pc sometime require more time to process the cam buffer. With this I don't end up in Deadlock city
      if (MediaControl != null)
        MediaControl.StopWhenReady();
      Marshal.ReleaseComObject(MediaControl);
      Marshal.ReleaseComObject(GraphBuilder);
      Marshal.ReleaseComObject(CaptureGraphBuilder);
      CaptureGraphBuilder = null;
      GraphBuilder = null;
      MediaControl = null;
      frame.Dispose();
      frame = null;
      Marshal.ReleaseComObject(SampleGrabber);
      SampleGrabber = null;
    }

    protected void UpdateBuffer()
    {
      int samplePosRGBA = 0;
      int samplePosRGB24 = 0;
      while (isRunning)
      {
        lock (lockObj)
        {
          for (int y = 0, y2 = Height - 1; y < Height; y++, y2--)
          {
            for (int x = 0; x < Width; x++)
            {
              samplePosRGBA = (((y2 * Width) + x) * 4);
              samplePosRGB24 = ((y * Width) + (Width - x - 1)) * 3;
              FrameRGBA[samplePosRGBA + 0] = FrameBGR[samplePosRGB24 + 2];
              FrameRGBA[samplePosRGBA + 1] = FrameBGR[samplePosRGB24 + 1];
              FrameRGBA[samplePosRGBA + 2] = FrameBGR[samplePosRGB24 + 0];
              FrameRGBA[samplePosRGBA + 3] = (byte)255;
            }
          }
        }
        FrameReady = false;
        while (!FrameReady) Thread.Sleep(20);

      }
    }

    public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
    {
      lock (lockObj)
      {
        Marshal.Copy(pBuffer, FrameBGR, 0, BufferLen);
      }
      FrameReady = true;
      return 0;
    }

    public int SampleCB(double SampleTime, IMediaSample pSample)
    {
      return 0;
    }
  }
}