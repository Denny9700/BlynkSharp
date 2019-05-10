using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

namespace BlynkLib
{
  public class BlynkClient
  {
    private Thread _blinkThread;
    private RestClient blynkClient;

    private Stopwatch checkWatch;
    private Stopwatch blynkWatch;

    private List<BlynkPin> pins;

    public delegate void BadResponseHandler(object sender, BadResponseEventArgs e);
    public delegate void ConnectionChangeHandler(object sender, ConnectionChangeEventArgs e);
    public delegate void DigitalPinDataReceivedHandler(object sender, DigitalPinDataReceivedEventArgs e);
    public delegate void VirtualPinDataReceivedHandler(object sender, VirtualPinDataReceivedEventArgs e);

    public event BadResponseHandler BadResponse;
    public event ConnectionChangeHandler ConnectionChange;
    public event DigitalPinDataReceivedHandler DigitalPinDataReceived;
    public event VirtualPinDataReceivedHandler VirtualPinDataReceived;

    protected virtual void OnBadResponseEvent(BadResponseEventArgs args)
    {
      if(BadResponse != null)
        BadResponse.Invoke(this, args);
    }

    protected virtual void OnConnectionChangeEvent(ConnectionChangeEventArgs args)
    {
      if(ConnectionChange != null)
        ConnectionChange.Invoke(this, args);
    }

    protected virtual void OnDigitalPinDataReceivedEvent(DigitalPinDataReceivedEventArgs args)
    {
      if(DigitalPinDataReceived != null)
        DigitalPinDataReceived.Invoke(this, args);
    }

    protected virtual void OnVirtualPinDataReceivedEvent(VirtualPinDataReceivedEventArgs args)
    {
      if(VirtualPinDataReceived != null)
        VirtualPinDataReceived.Invoke(this, args);
    }

    #region Private Var
    private string _blynkUri;
    private string _authToken;
    private bool _hardwareConnected;
    private bool _appConnected;
    #endregion

    #region Properties
    public string BlynkUri
    {
      get
      {
        return this._blynkUri;
      }

      internal set
      {
        this._blynkUri = value;
      }
    }

    public string AuthToken
    {
      get
      {
        return this._authToken;
      }

      internal set
      {
        this._authToken = value;
      }
    }
    #endregion

    #region Constants
    public static string MainBlynkUri = "http://blynk-cloud.com";

    public static int CheckTimer = 15 * 1000;
    public static int BlynkTimer = 2 * 1000;
    #endregion

    public BlynkClient(string token)
    {
      this.BlynkUri = MainBlynkUri;
      this._blinkThread = new Thread(new ThreadStart(blynkThreadDelegate));

      this.AuthToken = token;

      this.checkWatch = new Stopwatch();
      this.blynkWatch = new Stopwatch();

      this.pins = new List<BlynkPin>();
      this.blynkClient = new RestClient(this.BlynkUri);
    }

    public BlynkClient(string uri, string token)
      : this(token)
    {
      this.BlynkUri = uri;
    }

    public void StartService()
    {
      if (this._blinkThread == null || this._blinkThread.IsAlive)
        return;

      this._blinkThread.Start();

      this.checkWatch.Start();
      this.blynkWatch.Start();
    }

    public void StopService()
    {
      if (!this._blinkThread.IsAlive)
        return;

      this._blinkThread.Abort();

      this.checkWatch.Stop();
      this.blynkWatch.Stop();
    }

    public void AddPin(BlynkPin pin)
    {
      if (this.pins != null && !this.pins.Contains(pin))
      {
        pin.Client = this;
        this.pins.Add(pin);
      }
    }

    public void AddPin(byte pin, PinType type)
    {
      if(pin < 0)
        return;

      this.AddPin(new BlynkPin(this, pin, type));
    }

    public void AddPin(byte pin, int value, PinType type)
    {
      if(pin < 0)
        return;

      this.AddPin(new BlynkPin(this, pin, value, type));
    }

    public void RemovePin(BlynkPin pin)
    {
      if (this.pins != null && this.pins.Contains(pin))
        this.pins.Remove(pin);
    }

    public void RemovePin(byte pin, PinType type)
    {
      if(pin < 0)
        return;

      this.RemovePin(new BlynkPin(this, pin, type));
    }

    public bool IsHardwareConnected()
    {
      this.blynkClient = new RestClient(string.Format("{0}/{1}/isHardwareConnected", MainBlynkUri, this.AuthToken));
      RestResponse response = blynkClient.Get();

      if (response.StatusCode != HttpStatusCode.OK)
      {
        OnBadResponseEvent(new BadResponseEventArgs
        {
          StatusCode = (int)response.StatusCode,
          Message = response.Content
        });

        return false;
      }

      return bool.Parse(response.Content);
    }

    public bool IsAppConnected()
    {
      this.blynkClient = new RestClient(string.Format("{0}/{1}/isAppConnected", MainBlynkUri, this.AuthToken));
      RestResponse response = this.blynkClient.Get();

      if (response.StatusCode != HttpStatusCode.OK)
      {
        OnBadResponseEvent(new BadResponseEventArgs
        {
          StatusCode = (int)response.StatusCode,
          Message = response.Content
        });

        return false;
      }

      return bool.Parse(response.Content);
    }

    public ProjectStructure GetProjectStructure()
    {
      this.blynkClient = new RestClient(string.Format("{0}/{1}/project", MainBlynkUri, this.AuthToken));
      RestResponse response = this.blynkClient.Get();

      if (response.StatusCode != HttpStatusCode.OK)
      {
        OnBadResponseEvent(new BadResponseEventArgs
        {
          StatusCode = (int)response.StatusCode,
          Message = response.Content
        });

        return null;
      }

      return JsonConvert.DeserializeObject<ProjectStructure>(response.Content);
    }

    public bool WritePin(BlynkPin pin)
    {
      string pinType = pin.PinType == PinType.Digital ? "D" : "V";

      this.blynkClient = new RestClient(string.Format("{0}/{1}/update/{2}{3}?value={4}", MainBlynkUri, this.AuthToken, pinType, pin.Pin, pin.Value));
      RestResponse response = this.blynkClient.Get();

      if (response.StatusCode != HttpStatusCode.OK)
      {
        OnBadResponseEvent(new BadResponseEventArgs
        {
          StatusCode = (int)response.StatusCode,
          Message = response.Content
        });

        return false;
      }
      return true;
    }

    public bool WriteDigitalPin(byte pin)
    {
      if(pin < 0)
        return false;

      return this.WriteDigitalPin(pin, 0);
    }

    public bool WriteDigitalPin(byte pin, int value)
    {
      if(pin < 0)
        return false;

      return this.WritePin(new BlynkPin(this, pin, value, PinType.Digital));
    }

    public bool WriteVirtualPin(byte pin)
    {
      if(pin < 0)
        return false;

      return this.WriteVirtualPin(pin, 0);
    }

    public bool WriteVirtualPin(byte pin, int value)
    {
      if(pin < 0)
        return false;

      return this.WritePin(new BlynkPin(this, pin, value, PinType.Virtual));
    }

    public BlynkPin ReadPin(byte pin, PinType type)
    {
      if(pin < 0)
        return null;

      string pinType = type == PinType.Digital ? "D" : "V";

      this.blynkClient = new RestClient(string.Format("{0}/{1}/get/{2}{3}", MainBlynkUri, this.AuthToken, pinType, pin));
      RestResponse response = this.blynkClient.Get();

      if (response.StatusCode != HttpStatusCode.OK)
      {
        OnBadResponseEvent(new BadResponseEventArgs
        {
          StatusCode = (int)response.StatusCode,
          Message = response.Content
        });

        return null;
      }

      return new BlynkPin(this, pin, int.Parse(JsonConvert.DeserializeObject<string[]>(response.Content)[0]), type);
    }

    public BlynkPin ReadDigitalPin(byte pin)
    {
      if(pin < 0)
        return null;

      return this.ReadPin(pin, PinType.Digital);
    }

    public BlynkPin ReadVirtualPin(byte pin)
    {
      if(pin < 0)
        return null;

      return this.ReadPin(pin, PinType.Virtual);
    }

    public bool Notify(NotificationStructure notification)
    {
      if (notification.body.Length > 255)
        return false;

      this.blynkClient = new RestClient(string.Format("{0}/{1}/notify", MainBlynkUri, this.AuthToken));
      RestResponse response = this.blynkClient.Post(string.Format("{0}", JsonConvert.SerializeObject(notification)));

      if (response.StatusCode != HttpStatusCode.OK)
      {
        OnBadResponseEvent(new BadResponseEventArgs
        {
          StatusCode = (int)response.StatusCode,
          Message = response.Content
        });

        return false;
      }
      return true;
    }

    public bool Notify(string body)
    {
      if(body.Length > 255)
        return false;

      return this.Notify(new NotificationStructure(body));
    }

    private void blynkThreadDelegate()
    {
      while (true)
      {
        if (this.checkWatch.ElapsedMilliseconds >= CheckTimer)
        {
          bool hwConnected = this.IsHardwareConnected();
          if (this._hardwareConnected != hwConnected)
          {
            this._hardwareConnected = hwConnected;
            OnConnectionChangeEvent(new ConnectionChangeEventArgs
            {
              ConnectionType = ConnectionType.Hardware,
              Status = hwConnected
            });
          }

          bool appConnected = this.IsAppConnected();
          if (this._appConnected != appConnected)
          {
            this._appConnected = appConnected;
            OnConnectionChangeEvent(new ConnectionChangeEventArgs
            {
              ConnectionType = ConnectionType.App,
              Status = appConnected
            });
          }

          this.checkWatch.Reset();
          this.checkWatch.Start();
        }

        if (this.blynkWatch.ElapsedMilliseconds >= BlynkTimer)
        {
          for (int i = 0; i < this.pins.Count; i++)
          {
            var currentPin = this.pins[i];
            switch (currentPin.PinType)
            {
              case PinType.Virtual:
                var virtualPin = this.ReadVirtualPin(currentPin.Pin);
                if(virtualPin == null)
                  continue;

                if (virtualPin.Value != currentPin.Value)
                {
                  currentPin.Value = virtualPin.Value;
                  OnVirtualPinDataReceivedEvent(new VirtualPinDataReceivedEventArgs
                  {
                    BlynkPin = currentPin
                  });
                }
                break;
              case PinType.Digital:
                var digitalPin = this.ReadDigitalPin(currentPin.Pin);
                if(digitalPin == null)
                  continue;

                if (digitalPin.Value != currentPin.Value)
                {
                  currentPin.Value = digitalPin.Value;
                  OnDigitalPinDataReceivedEvent(new DigitalPinDataReceivedEventArgs
                  {
                    BlynkPin = currentPin
                  });
                }
                break;
            }
          }

          this.blynkWatch.Reset();
          this.blynkWatch.Start();
        }
        Thread.Sleep(100);
      }
    }
  }

  #region EventArgs
  public class BadResponseEventArgs : EventArgs
  {
    public int StatusCode { get; set; }
    public string Message { get; set; }

    public BadResponseEventArgs()
      : base()
    { }

    public BadResponseEventArgs(int statusCode)
      : base()
    {
      this.StatusCode = statusCode;
    }

    public BadResponseEventArgs(int statusCode, string message)
      : this(statusCode)
    {
      this.Message = message;
    }
  }

  public class ConnectionChangeEventArgs : EventArgs
  {
    public ConnectionType ConnectionType { get; set; }
    public bool Status { get; set; }

    public ConnectionChangeEventArgs()
      : base()
    { }

    public ConnectionChangeEventArgs(ConnectionType type, bool status)
      : this()
    {
      this.ConnectionType = type;
      this.Status = status;
    }
  }

  public class DigitalPinDataReceivedEventArgs : EventArgs
  {
    public BlynkPin BlynkPin { get; set; }

    public DigitalPinDataReceivedEventArgs()
      : base()
    { }

    public DigitalPinDataReceivedEventArgs(BlynkPin pin)
      : this()
    {
      this.BlynkPin = pin;
    }
  }

  public class VirtualPinDataReceivedEventArgs : EventArgs
  {
    public BlynkPin BlynkPin { get; set; }

    public VirtualPinDataReceivedEventArgs()
      : base()
    { }

    public VirtualPinDataReceivedEventArgs(BlynkPin pin)
      : this()
    {
      this.BlynkPin = pin;
    }
  }
  #endregion

  #region Artifact

  #region Project Structure
  public class ProjectStructure
  {
    public int id { get; set; }
    public int parentId { get; set; }
    public bool isPreview { get; set; }
    public string name { get; set; }
    public long createdAt { get; set; }
    public long updatedAt { get; set; }
    public Widget[] widgets { get; set; }
    public Device[] devices { get; set; }
    public string theme { get; set; }
    public bool keepScreenOn { get; set; }
    public bool isAppConnectedOn { get; set; }
    public bool isNotificationsOff { get; set; }
    public bool isShared { get; set; }
    public bool isActive { get; set; }
    public bool widgetBackgroundOn { get; set; }
    public int color { get; set; }
    public bool isDefaultColor { get; set; }
  }

  public class Widget
  {
    public string type { get; set; }
    public int id { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int color { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public int tabId { get; set; }
    public bool isDefaultColor { get; set; }
    public int deviceId { get; set; }
    public string pinType { get; set; }
    public int pin { get; set; }
    public bool pwmMode { get; set; }
    public bool rangeMappingOn { get; set; }
    public float min { get; set; }
    public float max { get; set; }
    public string value { get; set; }
    public bool pushMode { get; set; }
    public Onbuttonstate onButtonState { get; set; }
    public Offbuttonstate offButtonState { get; set; }
    public string fontSize { get; set; }
    public string edge { get; set; }
    public string buttonStyle { get; set; }
    public bool lockSize { get; set; }
    public string label { get; set; }
    public Androidtokens androidTokens { get; set; }
    public bool notifyWhenOffline { get; set; }
    public int notifyWhenOfflineIgnorePeriod { get; set; }
    public string priority { get; set; }
  }

  public class Onbuttonstate
  {
    public int textColor { get; set; }
    public int backgroundColor { get; set; }
  }

  public class Offbuttonstate
  {
    public int textColor { get; set; }
    public int backgroundColor { get; set; }
  }

  public class Androidtokens
  {
    public string _56545a83dbf9aa7a { get; set; }
  }

  public class Device
  {
    public int id { get; set; }
    public string name { get; set; }
    public string boardType { get; set; }
    public string vendor { get; set; }
    public string connectionType { get; set; }
    public bool isUserIcon { get; set; }
  }
  #endregion
  #region Notification Structure
  public struct NotificationStructure
  {
    public string body { get; set; }

    public NotificationStructure(string body)
    {
      this.body = body;
    }
  }
  #endregion

  #endregion

  #region BlynkPin
  public class BlynkPin
  {
    public BlynkClient Client { get; set; }

    public byte Pin { get; set; }
    public int Value { get; set; }
    public PinType PinType { get; internal set; }

    public BlynkPin(byte pin, PinType type)
    {
      this.Pin = pin;
      this.PinType = type;
    }

    public BlynkPin(BlynkClient blynk, PinType type)
    {
      this.Client = blynk;
    }

    public BlynkPin(BlynkClient blynk, byte pin, PinType type)
      : this(blynk, type)
    {
      this.Pin = pin;
    }

    public BlynkPin(BlynkClient blynk, byte pin, int value, PinType type)
      : this(blynk, pin, type)
    {
      this.Value = value;
    }

    public void On()
    {
      this.Value = 255;
      this.Client.WritePin(this);
    }

    public void Off()
    {
      this.Value = 0;
      this.Client.WritePin(this);
    }
  }
  #endregion

  #region RestRequest

  #region Enums
  internal enum HttpStatusCode
  {
    Continue = 100,
    SwitchingProtocols = 101,
    OK = 200,
    Created = 201,
    Accepted = 202,
    NonAuthoritativeInformation = 203,
    NoContent = 204,
    ResetContent = 205,
    PartialContent = 206,
    MultipleChoices = 300,
    Ambiguous = 300,
    MovedPermanently = 301,
    Moved = 301,
    Found = 302,
    Redirect = 302,
    SeeOther = 303,
    RedirectMethod = 303,
    NotModified = 304,
    UseProxy = 305,
    Unused = 306,
    TemporaryRedirect = 307,
    RedirectKeepVerb = 307,
    BadRequest = 400,
    Unauthorized = 401,
    PaymentRequired = 402,
    Forbidden = 403,
    NotFound = 404,
    MethodNotAllowed = 405,
    NotAcceptable = 406,
    ProxyAuthenticationRequired = 407,
    RequestTimeout = 408,
    Conflict = 409,
    Gone = 410,
    LengthRequired = 411,
    PreconditionFailed = 412,
    RequestEntityTooLarge = 413,
    RequestUriTooLong = 414,
    UnsupportedMediaType = 415,
    RequestedRangeNotSatisfiable = 416,
    ExpectationFailed = 417,
    UpgradeRequired = 426,
    InternalServerError = 500,
    NotImplemented = 501,
    BadGateway = 502,
    ServiceUnavailable = 503,
    GatewayTimeout = 504,
    HttpVersionNotSupported = 505
  }
  #endregion

  #region RestClient
  internal class RestClient
  {
    private HttpWebRequest webRequest;

    #region Private var
    private string _uri;
    #endregion

    #region Public var
    public string Uri
    {
      get
      {
        return this._uri;
      }

      set
      {
        this._uri = value;
      }
    }
    #endregion

    public RestClient(string uri)
    {
      this.Uri = uri;
    }

    public RestResponse Get()
    {
      this.webRequest = (HttpWebRequest)HttpWebRequest.Create(this.Uri);
      this.webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
      this.webRequest.ContentType = "application/json";

      try
      {
        using (HttpWebResponse response = (HttpWebResponse)this.webRequest.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream))
        {
          return new RestResponse
          {
            StatusCode = (HttpStatusCode)response.StatusCode,
            Content = reader.ReadToEnd()
          };
        }
      }
      catch (WebException e)
      {
        var response = (HttpWebResponse)e.Response;
        using(StreamReader reader = new StreamReader(response.GetResponseStream()))
        {
          return new RestResponse
          {
            StatusCode = (HttpStatusCode)response.StatusCode,
            Content = reader.ReadToEnd()
          };
        }
      }
    }

    public RestResponse Post(string data)
    {
      byte[] dataBytes = Encoding.UTF8.GetBytes(data);

      this.webRequest = (HttpWebRequest)HttpWebRequest.Create(this.Uri);
      this.webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
      this.webRequest.ContentLength = dataBytes.Length;
      this.webRequest.ContentType = "application/json";
      this.webRequest.Method = "POST";

      using (Stream requestBody = this.webRequest.GetRequestStream())
      {
        requestBody.Write(dataBytes, 0, dataBytes.Length);
      }

      try
      {
        using (HttpWebResponse response = (HttpWebResponse)this.webRequest.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream))
        {
          return new RestResponse
          {
            StatusCode = (HttpStatusCode)response.StatusCode,
            Content = reader.ReadToEnd()
          };
        }
      } catch(WebException e)
      {
        var response = (HttpWebResponse)e.Response;
        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
        {
          return new RestResponse
          {
            StatusCode = (HttpStatusCode)response.StatusCode,
            Content = reader.ReadToEnd()
          };
        }
      }
    }
  }
  #endregion

  #region RestResponse
  internal class RestResponse
  {
    public HttpStatusCode StatusCode { get; internal set; }
    public string Content { get; internal set; }

    public RestResponse()
    { }

    public RestResponse(HttpStatusCode code)
    {
      this.StatusCode = code;
    }

    public RestResponse(HttpStatusCode code, string content)
      : this(code)
    {
      this.Content = content;
    }
  }
  #endregion

  #endregion

  #region Enums
  public enum ConnectionType : byte
  {
    App,
    Hardware
  }

  public enum PinType : byte
  {
    Analog,
    Digital,
    Virtual
  }
  #endregion
}
