namespace AutonomousStore.Hardware.Interfaces;

public interface IRfidReader
{
    event EventHandler<RfidTagReadEventArgs>? TagRead;

    void Start();
    void Stop();
}

public class RfidTagReadEventArgs : EventArgs
{
    public string Tag { get; }

    public RfidTagReadEventArgs(string tag)
    {
        Tag = tag;
    }
}