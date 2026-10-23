using System;

namespace NaiwaPet;

public sealed class PetInteraction
{
    public bool Pressed { get; private set; }
    public bool Dragging { get; private set; }
    public bool Laughing { get; private set; }
    public double StartX { get; private set; }
    public double StartY { get; private set; }

    public void Press(double x, double y)
    {
        Pressed = true;
        Dragging = false;
        StartX = x;
        StartY = y;
    }

    public bool Move(double x, double y)
    {
        if (!Pressed || Dragging || double.Hypot(x - StartX, y - StartY) <= 4) return false;
        Dragging = true;
        Laughing = true;
        return true;
    }

    public bool Release()
    {
        if (!Pressed) return false;
        bool wasDrag = Dragging;
        Pressed = false;
        Dragging = false;
        Laughing = !wasDrag;
        return !wasDrag;
    }

    public void Laugh() => Laughing = true;
    public void FinishLaugh() => Laughing = false;
    public void Stop() { Pressed = false; Dragging = false; Laughing = false; }
}
