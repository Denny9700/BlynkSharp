using System;

using BlynkLib;

namespace BlynkSharp.Test
{
  class Program
  {
    private static BlynkPin LedOnBoard = new BlynkPin(5, PinType.Digital);
    private static BlynkPin VirtualLed = new BlynkPin(5, PinType.Virtual);

    static void Main(string[] args)
    {
      BlynkClient blynk = new BlynkClient("18ab7e60abcb4de1a47c94fe624d7633");
      blynk.AddPin(VirtualLed);
      blynk.AddPin(LedOnBoard);

      blynk.ConnectionChange += Blynk_ConnectionChange;
      blynk.BadResponse += Blynk_BadResponse;

      blynk.VirtualPinDataReceived += Blynk_VirtualPinDataReceived;
      blynk.DigitalPinDataReceived += Blynk_DigitalPinDataReceived;

      blynk.StartService();
      Console.ReadKey();
    }

    private static void Blynk_DigitalPinDataReceived(object sender, DigitalPinDataReceivedEventArgs e)
    {
      if (e.BlynkPin.Value > 0)
        VirtualLed.On();
      else
        VirtualLed.Off();
    }

    private static void Blynk_VirtualPinDataReceived(object sender, VirtualPinDataReceivedEventArgs e)
    {
      if (e.BlynkPin.Value > 0)
        LedOnBoard.On();
      else
        LedOnBoard.Off();
    }

    private static void Blynk_BadResponse(object sender, BadResponseEventArgs e)
    {
      Console.WriteLine($"Error -> Status: {e.StatusCode}, Message: {e.Message}");
    }

    private static void Blynk_ConnectionChange(object sender, ConnectionChangeEventArgs e)
    {
      Console.WriteLine($"Connection changed: {e.ConnectionType.ToString()} -> {e.Status}");
    }
  }
}
