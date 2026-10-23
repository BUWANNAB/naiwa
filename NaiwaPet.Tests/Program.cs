using System;
using NaiwaPet;

var pet = new PetInteraction();
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
Check(!pet.Move(10, 10), "hover does not start a drag");
pet.Press(100, 100);
Check(!pet.Move(102, 102), "small mouse jitter stays a click");
Check(pet.Release() && pet.Laughing && !pet.Dragging, "click triggers one laugh");
pet.Stop(); pet.Press(-300, 500);
Check(pet.Move(-320, 520) && pet.Dragging && pet.Laughing, "drag supports negative monitor coordinates");
Check(!pet.Move(-350, 550), "continued drag does not restart audio");
Check(!pet.Release() && !pet.Laughing && !pet.Pressed, "release stops drag and laugh");
Check(!pet.Release() && !pet.Laughing, "duplicate release is harmless");
pet.Press(0, 0); pet.Move(20, 0); pet.Stop();
Check(!pet.Pressed && !pet.Dragging && !pet.Laughing, "cancel stops interaction");
pet.Press(50, 50); pet.Laugh(); pet.FinishLaugh();
Check(pet.Pressed && !pet.Laughing && pet.Move(70, 70), "natural audio completion preserves a held mouse gesture");
pet.Stop();
