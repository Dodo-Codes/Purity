using SFML.Graphics; // textures and vertices etc.
using SFML.System; // vectors etc.
using SFML.Window; // window etc.

namespace Pure.Window
{
	/// <summary>
	/// The OS and tile mouse cursors.
	/// </summary>
	public enum Cursor
	{
		TileArrow, TileArrowNoTail, TileHand, TileText, TileCrosshair, TileNo, TileResizeHorizontal, TileResizeVertical,
		TileResizeDiagonal1, TileResizeDiagonal2, TileMove, TileWait1, TileWait2, TileWait3,

		SystemArrow, SystemArrowWait, SystemWait, SystemText, SystemHand, SystemResizeHorinzontal, SystemResizeVertical,
		SystemResizeDiagonal2, SystemResizeDiagonal1, SystemMove, SystemCrosshair, SystemHelp, SystemNo,

		None
	}

	/// <summary>
	/// Provides a simple way to create and interact with an OS window.
	/// </summary>
	public static class Window
	{
		/// <summary>
		/// Whether the OS window exists. This is <see langword="true"/> even when it
		/// is minimized or <see cref="IsHidden"/>.
		/// </summary>
		public static bool IsExisting
		{
			get => window != null && window.IsOpen;
			set
			{
				if(value == false)
					window.Close();
			}
		}
		/// <summary>
		/// The title on the title bar of the OS window.
		/// </summary>
		public static string Title
		{
			get => title;
			set { title = value; window.SetTitle(title); }
		}
		/// <summary>
		/// The size of the OS window.
		/// </summary>
		public static (uint, uint) Size
		{
			get => (window.Size.X, window.Size.Y);
			set => window.Size = new(value.Item1, value.Item2);
		}
		/// <summary>
		/// Whether the OS window acts as a background process.
		/// </summary>
		public static bool IsHidden
		{
			get => isHidden;
			set { isHidden = value; window.SetVisible(value == false); }
		}

		/// <summary>
		/// The mouse cursor position relative to the OS window.
		/// </summary>
		public static (int, int) MousePosition
		{
			get { var pos = Mouse.GetPosition(window); return (pos.X, pos.Y); }
			set
			{
				var pos = new Vector2i(value.Item1, value.Item2);
				Mouse.SetPosition(pos, window);
			}
		}
		/// <summary>
		/// The mouse cursor type used by the OS window and <see cref="TryDrawMouseCursor"/>.
		/// </summary>
		public static Cursor MouseCursor
		{
			get => cursor;
			set
			{
				cursor = value;

				if(value != Cursor.None && (int)value > (int)Cursor.TileWait3)
				{
					var sfmlEnum = (SFML.Window.Cursor.CursorType)((int)value - (int)Cursor.SystemArrow);
					sysCursor.Dispose();
					sysCursor = new(sfmlEnum);

					window.SetMouseCursor(sysCursor);
				}
			}
		}
		/// <summary>
		/// The mouse cursor color used by <see cref="TryDrawMouseCursor"/>.
		/// </summary>
		public static byte MouseColor { get; set; } = 255;
		/// <summary>
		/// Whether the mouse cursor is restricted of leaving the OS window.
		/// </summary>
		public static bool MouseIsRestriced
		{
			get => isMouseGrabbed;
			set { isMouseGrabbed = value; window.SetMouseCursorGrabbed(value); }
		}

		static Window()
		{
			//var str = DefaultGraphics.PNGToBase64String("graphics.png");

			sysCursor = new SFML.Window.Cursor(SFML.Window.Cursor.CursorType.Arrow);
			graphics["default"] = DefaultGraphics.CreateTexture();

			var desktopW = VideoMode.DesktopMode.Width;
			var desktopH = VideoMode.DesktopMode.Height;
			title = "";

			var width = (uint)RoundToMultipleOfTwo((int)(desktopW * 0.6f));
			var height = (uint)RoundToMultipleOfTwo((int)(desktopH * 0.6f));

			window = new(new VideoMode(width, height), title);
			window.Closed += (s, e) => window.Close();
			window.Resized += (s, e) => UpdateWindowAndView();
			window.DispatchEvents();
			window.Clear();
			window.Display();
			UpdateWindowAndView();
		}

		/// <summary>
		/// If the drawing <paramref name="isEnabled"/>:<br></br>
		/// - The OS window setups for drawing<br></br>
		/// Otherwise:<br></br>
		/// - The OS window displays everything drawn<br></br><br></br>
		/// Or in other words: drawing onto the OS window should start with enabling the draw
		/// and end with disabling it.
		/// </summary>
		public static void DrawEnable(bool isEnabled)
		{
			if(isEnabled)
			{
				window.DispatchEvents();
				window.Clear();
				return;
			}

			TryDrawMouseCursor();
			window.Display();
		}

		/// <summary>
		/// Draws a tilemap onto the OS window. Its graphics image is loaded from a
		/// <paramref name="path"/> (default graphics if <see langword="null"/>) using a <paramref name="tileSize"/> and a
		/// <paramref name="tileMargin"/>, then it is cached for future draws. The tilemap's
		/// contents are decided by <paramref name="tiles"/> and <paramref name="colors"/>.
		/// </summary>
		public static void DrawTilemap(int[,] tiles, byte[,] colors, (uint, uint) tileSize,
			(uint, uint) tileMargin = default, string? path = default)
		{
			if(tiles == null || colors == null || tiles.Length != colors.Length)
				return;

			path ??= "default";

			TryLoadGraphics(path);
			var verts = GetTilemapVertices(tiles, colors, tileSize, tileMargin, path);
			window.Draw(verts, PrimitiveType.Quads, new(graphics[path]));
		}
		/// <summary>
		/// Draws a sprite onto the OS window. Its graphics are decided by a <paramref name="tile"/>
		/// from the last <see cref="DrawTilemap"/> call and a <paramref name="color"/>. The sprite's
		/// <paramref name="position"/> is also relative to the previously drawn tilemap.
		/// </summary>
		public static void DrawSprite((float, float) position, int tile, byte color)
		{
			if(prevDrawTilemapGfxPath == null)
				return;

			var verts = GetSpriteVertices(position, tile, color);
			window.Draw(verts, PrimitiveType.Quads, new(graphics[prevDrawTilemapGfxPath]));
		}
		/// <summary>
		/// Draws single pixel points with <paramref name="color"/> onto the OS window.
		/// Their <paramref name="positions"/> are relative to the previously drawn tilemap.
		/// </summary>
		public static void DrawPoints(byte color, params (float, float)[] positions)
		{
			if(positions == null || positions.Length == 0)
				return;

			var verts = GetPointsVertices(color, positions);
			window.Draw(verts, PrimitiveType.Quads);
		}
		/// <summary>
		/// Draws a rectangle with <paramref name="color"/> onto the OS window.
		/// Its <paramref name="position"/> and <paramref name="size"/> are relative
		/// to the previously drawn tilemap.
		/// </summary>
		public static void DrawRectangle((float, float) position, (float, float) size, byte color)
		{
			var verts = GetRectangleVertices(position, size, color);
			window.Draw(verts, PrimitiveType.Quads);
		}
		/// <summary>
		/// Draws a line between <paramref name="pointA"/> and <paramref name="pointB"/> with
		/// <paramref name="color"/> onto the OS window.
		/// Its points are relative to the previously drawn tilemap.
		/// </summary>
		public static void DrawLine((float, float) pointA, (float, float) pointB, byte color)
		{
			var verts = GetLineVertices(pointA, pointB, color);
			window.Draw(verts, PrimitiveType.Quads);
		}

		#region Backend
		private const int LINE_MAX_ITERATIONS = 10000;

		private static Cursor cursor;
		private static SFML.Window.Cursor sysCursor;
		private static bool isHidden, isMouseGrabbed;
		private static string title;
		private static string? prevDrawTilemapGfxPath;
		private static (uint, uint) prevDrawTilemapTileSz;
		private static (float, float) prevDrawTilemapCellSz;
		private static (uint, uint) prevDrawTilemapCellCount;

		private static readonly Dictionary<Cursor, (float, float)> cursorOffsets = new()
		{
			{ Cursor.TileArrow, (0, 0) }, { Cursor.TileArrowNoTail, (0, 0) }, { Cursor.TileHand, (0.2f, 0f) },
			{ Cursor.TileText, (0.3f, 0.4f) }, { Cursor.TileCrosshair, (0.3f, 0.3f) }, { Cursor.TileNo, (0.4f, 0.4f) },
			{ Cursor.TileResizeHorizontal, (0.4f, 0.3f) }, { Cursor.TileResizeVertical, (0.3f, 0.4f) },
			{ Cursor.TileResizeDiagonal1, (0.4f, 0.4f) }, { Cursor.TileResizeDiagonal2, (0.4f, 0.4f) },
			{ Cursor.TileMove, (0.4f, 0.4f) }, { Cursor.TileWait1, (0.4f, 0.4f) }, { Cursor.TileWait2, (0.4f, 0.4f) },
			{ Cursor.TileWait3, (0.4f, 0.4f) },
		};
		private static readonly Dictionary<string, Texture> graphics = new();
		private static readonly RenderWindow window;

		private static void TryLoadGraphics(string path)
		{
			if(graphics.ContainsKey(path))
				return;

			graphics[path] = new(path);
		}
		private static void TryDrawMouseCursor()
		{
			var cursor = (int)MouseCursor;

			window.SetMouseCursorVisible(IsHovering() == false);

			if(cursor > (int)Cursor.TileWait3)
				return;

			var (x, y) = PositionFrom(MousePosition);
			var (offX, offY) = cursorOffsets[MouseCursor];
			DrawSprite((x - offX, y - offY), 494 + cursor, MouseColor);
		}
		private static void UpdateWindowAndView()
		{
			var view = window.GetView();
			var (w, h) = (RoundToMultipleOfTwo((int)Size.Item1), RoundToMultipleOfTwo((int)Size.Item2));
			view.Size = new(w, h);
			view.Center = new(RoundToMultipleOfTwo((int)(Size.Item1 / 2f)), RoundToMultipleOfTwo((int)(Size.Item2 / 2f)));
			window.SetView(view);
			window.Size = new((uint)w, (uint)h);
		}

		private static Vertex[] GetRectangleVertices((float, float) position, (float, float) size, byte color)
		{
			if(prevDrawTilemapGfxPath == null)
				return Array.Empty<Vertex>();

			var verts = new Vertex[4];
			var cellCount = prevDrawTilemapCellCount;
			var (cellWidth, cellHeight) = prevDrawTilemapCellSz;
			var (tileWidth, tileHeight) = prevDrawTilemapTileSz;

			var (w, h) = size;
			var x = Map(position.Item1, 0, cellCount.Item1, 0, window.Size.X);
			var y = Map(position.Item2, 0, cellCount.Item2, 0, window.Size.Y);
			var c = ByteToColor(color);
			var (gridX, gridY) = ToGrid((x, y), (cellWidth / tileWidth, cellHeight / tileHeight));
			var tl = new Vector2f(gridX, gridY);
			var br = new Vector2f(gridX + cellWidth * w, gridY + cellHeight * h);

			verts[0] = new(new(tl.X, tl.Y), c);
			verts[1] = new(new(br.X, tl.Y), c);
			verts[2] = new(new(br.X, br.Y), c);
			verts[3] = new(new(tl.X, br.Y), c);
			return verts;
		}
		private static Vertex[] GetLineVertices((float, float) a, (float, float) b, byte color)
		{
			var (tileW, tileH) = prevDrawTilemapTileSz;
			var (x0, y0) = a;
			var (x1, y1) = b;
			var dx = MathF.Abs(x1 - x0);
			var dy = -MathF.Abs(y1 - y0);
			var (stepX, stepY) = (1f / tileW * 0.999f, 1f / tileH * 0.999f);
			var sx = x0 < x1 ? stepX : -stepY;
			var sy = y0 < y1 ? stepX : -stepY;
			var err = dx + dy;
			var points = new List<(float, float)>();
			float e2;

			for(int i = 0; i < LINE_MAX_ITERATIONS; i++)
			{
				points.Add((x0, y0));

				if(IsWithin(x0, x1, stepX) && IsWithin(y0, y1, stepY))
					break;

				e2 = 2f * err;

				if(e2 > dy)
				{
					err += dy;
					x0 += sx;
				}
				if(e2 < dx)
				{
					err += dx;
					y0 += sy;
				}
			}
			return GetPointsVertices(color, points.ToArray());
		}
		private static Vertex[] GetSpriteVertices((float, float) position, int cell, byte color)
		{
			if(prevDrawTilemapGfxPath == null)
				return Array.Empty<Vertex>();

			var verts = new Vertex[4];
			var (cellWidth, cellHeight) = prevDrawTilemapCellSz;
			var cellCount = prevDrawTilemapCellCount;
			var (tileWidth, tileHeight) = prevDrawTilemapTileSz;
			var texture = graphics[prevDrawTilemapGfxPath];

			var tileCount = (texture.Size.X / tileWidth, texture.Size.Y / tileHeight);
			var texCoords = IndexToCoords(cell, tileCount);
			var tx = new Vector2f(texCoords.Item1 * tileWidth, texCoords.Item2 * tileHeight);
			var x = Map(position.Item1, 0, cellCount.Item1, 0, window.Size.X);
			var y = Map(position.Item2, 0, cellCount.Item2, 0, window.Size.Y);
			var c = ByteToColor(color);
			var grid = ToGrid((x, y), (cellWidth / tileWidth, cellHeight / tileHeight));
			var tl = new Vector2f(grid.Item1, grid.Item2);
			var br = new Vector2f(grid.Item1 + cellWidth, grid.Item2 + cellHeight);

			verts[0] = new(new(tl.X, tl.Y), c, tx);
			verts[1] = new(new(br.X, tl.Y), c, tx + new Vector2f(tileWidth, 0));
			verts[2] = new(new(br.X, br.Y), c, tx + new Vector2f(tileWidth, tileHeight));
			verts[3] = new(new(tl.X, br.Y), c, tx + new Vector2f(0, tileHeight));
			return verts;
		}
		private static Vertex[] GetTilemapVertices(int[,] tiles, byte[,] colors,
			(uint, uint) tileSz, (uint, uint) tileOff, string path)
		{
			if(tiles == null || window == null)
				return Array.Empty<Vertex>();

			var cellWidth = (float)window.Size.X / tiles.GetLength(0);
			var cellHeight = (float)window.Size.Y / tiles.GetLength(1);
			var texture = graphics[path];
			var tileCount = (texture.Size.X / tileSz.Item1, texture.Size.Y / tileSz.Item2);
			var verts = new Vertex[tiles.Length * 4];

			// this cache is used for a potential sprite draw
			prevDrawTilemapGfxPath = path;
			prevDrawTilemapCellSz = (cellWidth, cellHeight);
			prevDrawTilemapTileSz = tileSz;
			prevDrawTilemapCellCount = ((uint)tiles.GetLength(0), (uint)tiles.GetLength(1));

			for(uint y = 0; y < tiles.GetLength(1); y++)
				for(uint x = 0; x < tiles.GetLength(0); x++)
				{
					var cell = tiles[x, y];
					var color = ByteToColor(colors[x, y]);
					var i = GetIndex(x, y, (uint)tiles.GetLength(0)) * 4;
					var tl = new Vector2f(x * cellWidth, y * cellHeight);
					var tr = new Vector2f((x + 1) * cellWidth, y * cellHeight);
					var br = new Vector2f((x + 1) * cellWidth, (y + 1) * cellHeight);
					var bl = new Vector2f(x * cellWidth, (y + 1) * cellHeight);

					var w = tileSz.Item1;
					var h = tileSz.Item2;
					var texCoords = IndexToCoords(cell, tileCount);
					var tx = new Vector2f(
						texCoords.Item1 * (w + tileOff.Item1),
						texCoords.Item2 * (h + tileOff.Item2));
					var texTr = new Vector2f(tx.X + w, tx.Y);
					var texBr = new Vector2f(tx.X + w, tx.Y + h);
					var texBl = new Vector2f(tx.X, tx.Y + h);

					verts[i + 0] = new(tl, color, tx);
					verts[i + 1] = new(tr, color, texTr);
					verts[i + 2] = new(br, color, texBr);
					verts[i + 3] = new(bl, color, texBl);
				}
			return verts;
		}
		private static Vertex[] GetPointsVertices(byte color, (float, float)[] positions)
		{
			var verts = new Vertex[positions.Length * 4];
			var tileSz = prevDrawTilemapTileSz;
			var cellWidth = prevDrawTilemapCellSz.Item1 / tileSz.Item1;
			var cellHeight = prevDrawTilemapCellSz.Item2 / tileSz.Item2;
			var cellCount = prevDrawTilemapCellCount;

			for(int i = 0; i < positions.Length; i++)
			{
				var x = Map(positions[i].Item1, 0, cellCount.Item1, 0, window.Size.X);
				var y = Map(positions[i].Item2, 0, cellCount.Item2, 0, window.Size.Y);
				var c = ByteToColor(color);
				var grid = ToGrid((x, y), (cellWidth, cellHeight));
				var tl = new Vector2f(grid.Item1, grid.Item2);
				var br = new Vector2f(grid.Item1 + cellWidth, grid.Item2 + cellHeight);

				var index = i * 4;
				verts[index + 0] = new(new(tl.X, tl.Y), c);
				verts[index + 1] = new(new(br.X, tl.Y), c);
				verts[index + 2] = new(new(br.X, br.Y), c);
				verts[index + 3] = new(new(tl.X, br.Y), c);
			}
			return verts;
		}

		private static (int, int) IndexToCoords(int index, (uint, uint) fieldSize)
		{
			index = index < 0 ? 0 : index;
			index = index > fieldSize.Item1 * fieldSize.Item2 - 1 ?
				(int)(fieldSize.Item1 * fieldSize.Item2 - 1) : index;

			return (index % (int)fieldSize.Item1, index / (int)fieldSize.Item1);
		}
		private static uint GetIndex(uint x, uint y, uint width)
		{
			return y * width + x;
		}
		private static Color ByteToColor(byte color)
		{
			var binary = Convert.ToString(color, 2).PadLeft(8, '0');
			var r = binary[0..3];
			var g = binary[3..6];
			var b = binary[6..8];
			var red = (byte)(Convert.ToByte(r, 2) * byte.MaxValue / 7);
			var green = (byte)(Convert.ToByte(g, 2) * byte.MaxValue / 7);
			var blue = (byte)(Convert.ToByte(b, 2) * byte.MaxValue / 3);
			return new(red, green, blue);
		}
		private static (float, float) ToGrid((float, float) pos, (float, float) gridSize)
		{
			if(gridSize == default)
				return pos;

			var X = pos.Item1;
			var Y = pos.Item2;

			// this prevents -0 cells
			var x = X - (X < 0 ? gridSize.Item1 : 0);
			var y = Y - (Y < 0 ? gridSize.Item2 : 0);

			x -= X % gridSize.Item1;
			y -= Y % gridSize.Item2;
			return new(x, y);
		}
		private static float Map(float number, float a1, float a2, float b1, float b2)
		{
			var value = (number - a1) / (a2 - a1) * (b2 - b1) + b1;
			return float.IsNaN(value) || float.IsInfinity(value) ? b1 : value;
		}
		private static bool IsBetween(float number, float rangeA, float rangeB)
		{
			if(rangeA > rangeB)
				(rangeA, rangeB) = (rangeB, rangeA);

			var l = rangeA <= number;
			var u = rangeB >= number;
			return l && u;
		}
		private static bool IsWithin(float number, float targetNumber, float range)
		{
			return IsBetween(number, targetNumber - range, targetNumber + range);
		}
		private static int RoundToMultipleOfTwo(int n)
		{
			var rem = n % 2;
			var result = n - rem;
			if(rem >= 1)
				result += 2;
			return result;
		}
		private static (float, float) PositionFrom((int, int) screenPixel)
		{
			var x = Map(screenPixel.Item1, 0, Size.Item1, 0, prevDrawTilemapCellCount.Item1);
			var y = Map(screenPixel.Item2, 0, Size.Item2, 0, prevDrawTilemapCellCount.Item2);

			return (x, y);
		}
		private static bool IsHovering()
		{
			var pos = Mouse.GetPosition(window);
			return pos.X > 0 && pos.X < window.Size.X && pos.Y > 0 && pos.Y < window.Size.Y;
		}
		#endregion
	}
}
