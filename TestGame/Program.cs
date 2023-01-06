﻿using Pure.Graphics;
using Pure.Input;
using Pure.Modding;
using Pure.Tilemap;
using Pure.UserInterface;
using Pure.Utilities;

namespace TestGame
{
	public class Program
	{
		public enum Song
		{
			CoolVibe
		}

		static void Main()
		{
			var bg = new Tilemap((48, 27));
			var layer = new Tilemap((48, 27));
			var over = new Tilemap((48, 27));
			var inputBox = new InputLine((0, 0), (10, 1), "Hello");
			var inputBox2 = new InputLine((10, 15), (10, 1), "Test");

			var script = new Script("function main() end");
			var value = script.Call("main");

			while(Window.IsExisting)
			{
				var mousePos = Window.MousePosition;
				var hov = layer.PositionFrom(mousePos, Window.Size);

				Mouse.Update();
				Keyboard.Update();
				Time.Update();

				var input = new Input()
				{
					Position = hov,
					IsPressed = Mouse.IsPressed(Pure.Input.Button.Left),
					TypedSymbols = Keyboard.TypedSymbols,
					IsPressedBackspace = Keyboard.IsPressed(Key.Backspace),
					IsPressedLeft = Keyboard.IsPressed(Key.ArrowLeft),
					IsPressedRight = Keyboard.IsPressed(Key.ArrowRight),
				};

				UserInterface.UpdateInput(input);

				bg.Fill(0, 0);
				layer.Fill(0, 0);
				over.Fill(0, 0);

				Window.DrawEnable(true);

				MyCoolInputBoxUpdate(bg, layer, over, inputBox);
				MyCoolInputBoxUpdate(bg, layer, over, inputBox2);

				bg.UpdateCamera();
				layer.UpdateCamera();
				over.UpdateCamera();

				Window.DrawLayer(bg.Camera, bg.Camera, (8, 8), (0, 0));
				Window.DrawLayer(layer.Camera, layer.Camera, (8, 8), (0, 0));
				Window.DrawLayer(over.Camera, over.Camera, (8, 8), (0, 0));

				Window.DrawLine((1, 1), hov, Color.Gray);

				Window.DrawEnable(false);
			}
		}
		static void MyCoolInputBoxUpdate(Tilemap bg, Tilemap layer, Tilemap over, InputLine inputBox)
		{
			inputBox.Update((b) =>
			{
				var pos = inputBox.Position;
				var cursorPos = (pos.Item1 + inputBox.IndexCursor, pos.Item2);
				var selectedPos = (pos.Item1 + inputBox.IndexSelection, pos.Item2);
				var size = cursorPos.Item1 - selectedPos.Item1;

				if(size < 0)
					selectedPos.Item1--;

				bg.SetSquare(inputBox.Position, inputBox.Size, 10, Color.Gray);
				bg.SetSquare(selectedPos, (size, 1), Tile.SHADE_OPAQUE, Color.Blue);
				layer.SetTextLine(inputBox.Position, inputBox.Text, Color.Red);

				if(inputBox.IsFocused)
					over.SetTile(cursorPos, Tile.SHAPE_TRIANGLE_BIG_HOLLOW + 3, Color.White);
			});
		}
	}
}