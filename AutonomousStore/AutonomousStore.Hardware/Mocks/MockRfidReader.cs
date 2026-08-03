using AutonomousStore.Hardware.Interfaces;

namespace AutonomousStore.Hardware.Mocks;

public class MockRfidReader : IRfidReader
{
    private bool _isRunning;

    public event EventHandler<RfidTagReadEventArgs>? TagRead;

    public void Start() => _isRunning = true;

    public void Stop() => _isRunning = false;

    public void SimulateRead(string tag)
    {
        if (!_isRunning)
            return;

        if (string.IsNullOrWhiteSpace(tag))
            return;

        TagRead?.Invoke(this, new RfidTagReadEventArgs(tag));
    }
}