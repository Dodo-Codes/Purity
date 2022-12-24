﻿using System.IO.Compression;
using System.Xml;

namespace Purity.Tilemap
{
	public class Tilemap
	{
		public enum Alignment
		{
			UpLeft, Up, UpRight,
			Left, Center, Right,
			DownLeft, Down, DownRight
		};

		public uint TileTotalCount => TileCount.Item1 * TileCount.Item2;
		public (uint, uint) TileCount => ((uint)tiles.GetLength(0), (uint)tiles.GetLength(1));

		public (int[,], byte[,]) Camera { get; private set; }
		public (int, int) CameraPosition { get; set; }
		public (int, int) CameraSize { get; set; }

		public Tilemap((uint, uint) tileCount)
		{
			var (w, h) = tileCount;
			tiles = new int[w, h];
			colors = new byte[w, h];
			CameraSize = ((int)w, (int)h);
		}
		public Tilemap(int[,] tiles)
		{
			this.tiles = tiles;
			colors = new byte[tiles.GetLength(0), tiles.GetLength(1)];
			CameraSize = (tiles.GetLength(0), tiles.GetLength(1));
		}
		public Tilemap(byte[,] colors)
		{
			tiles = new int[colors.GetLength(0), colors.GetLength(1)];
			this.colors = colors;
			CameraSize = (tiles.GetLength(0), tiles.GetLength(1));
		}
		public Tilemap(int[,] tiles, byte[,] colors)
		{
			this.tiles = tiles;
			this.colors = colors;
			CameraSize = (tiles.GetLength(0), tiles.GetLength(1));
		}
		public Tilemap(string tmxPath, string layerName)
		{
			this.tiles = new int[0, 0];
			colors = new byte[0, 0];

			if(tmxPath == null)
				throw new ArgumentNullException(nameof(tmxPath));

			if(File.Exists(tmxPath) == false)
				throw new ArgumentException($"No tmx file was found at '{tmxPath}'.");

			var xml = new XmlDocument();
			xml.Load(tmxPath);

			var layers = xml.GetElementsByTagName("layer");
			var layer = default(XmlElement);
			var data = default(XmlNode);
			var layerFound = false;

			foreach(var element in layers)
			{
				layer = (XmlElement)element;
				data = layer.FirstChild;

				if(data == null || data.Attributes == null)
					continue;

				var name = layer.Attributes["name"]?.Value;
				if(name == layerName)
				{
					layerFound = true;
					break;
				}
			}

			if(layerFound == false)
				throw new Exception($"File at '{tmxPath}' does not contain the layer '{layerName}'.");

			if(layer == null || data == null)
			{
				Error();
				return;
			}

			var dataStr = data.InnerText.Trim();
			var attributes = data.Attributes;
			var encoding = attributes?["encoding"]?.Value;
			var compression = attributes?["compression"]?.Value;
			_ = int.TryParse(layer?.Attributes?["width"]?.InnerText, out var mapWidth);
			_ = int.TryParse(layer?.Attributes?["height"]?.InnerText, out var mapHeight);

			tiles = new int[mapWidth, mapHeight];
			colors = new byte[tiles.GetLength(0), tiles.GetLength(1)];
			CameraSize = (mapWidth, mapHeight);

			if(encoding == "csv")
				LoadFromCSV(dataStr);
			else if(encoding == "base64")
			{
				if(compression == null)
					LoadFromBase64Uncompressed(dataStr);
				else if(compression == "gzip")
					LoadFromBase64<GZipStream>(dataStr);
				else if(compression == "zlib")
					LoadFromBase64<ZLibStream>(dataStr);
				else
					throw new Exception($"Tile Layer Format encoding 'Base64' " +
						$"with compression '{compression}' is not supported.");
			}
			else
				throw new Exception($"Tile Layer Format encoding" +
					$"'{encoding}' is not supported.");

			for(int i = 0; i < colors.GetLength(0); i++)
				for(int j = 0; j < colors.GetLength(1); j++)
					colors[i, j] = 255;

			void Error() => throw new Exception(
				$"Could not parse file at '{tmxPath}', layer '{layerName}'.");
		}

		public void UpdateCamera()
		{
			var (w, h) = CameraSize;
			var (cx, cy) = CameraPosition;
			var tiles = new int[Math.Abs(w), Math.Abs(h)];
			var colors = new byte[Math.Abs(w), Math.Abs(h)];
			var xStep = w < 0 ? -1 : 1;
			var yStep = h < 0 ? -1 : 1;
			var i = 0;
			for(int x = cx; x != cx + w; x += xStep)
			{
				var j = 0;
				for(int y = cy; y != cy + h; y += yStep)
				{
					tiles[i, j] = GetTile((x, y));
					colors[i, j] = GetColor((x, y));
					j++;
				}
				i++;
			}
			Camera = (tiles, colors);
		}

		public int GetTile((int, int) position)
		{
			return IndicesAreValid(position) ? tiles[position.Item1, position.Item2] : default;
		}
		public byte GetColor((int, int) position)
		{
			return IndicesAreValid(position) ? colors[position.Item1, position.Item2] : default;
		}

		public void Fill(int tile, byte color)
		{
			for(uint y = 0; y < TileCount.Item2; y++)
				for(uint x = 0; x < TileCount.Item1; x++)
				{
					tiles[x, y] = tile;
					colors[x, y] = color;
				}
		}
		public void SetTile((int, int) position, int title, byte color)
		{
			if(IndicesAreValid(position) == false)
				return;

			var x = position.Item1;
			var y = position.Item2;
			tiles[x, y] = title;
			colors[x, y] = color;
		}
		public void SetSquare((int, int) position, (int, int) size, int tile, byte color)
		{
			var xStep = size.Item1 < 0 ? -1 : 1;
			var yStep = size.Item2 < 0 ? -1 : 1;
			for(int x = position.Item1; x != position.Item1 + size.Item1; x += xStep)
				for(int y = position.Item2; y != position.Item2 + size.Item2; y += yStep)
					SetTile((x, y), tile, color);
		}
		public void SetTextLine((int, int) position, string text, byte color)
		{
			var errorOffset = 0;
			for(int i = 0; i < text?.Length; i++)
			{
				var symbol = text[i];
				var index = SymbolToTile(symbol);

				if(index == default && symbol != ' ')
				{
					errorOffset++;
					continue;
				}

				SetTile((position.Item1 + i - errorOffset, position.Item2), index, color);
			}
		}
		public void SetTextBox((int, int) position, (int, int) size, byte color,
			bool isWordWrapping, Alignment alignment, params string[] lines)
		{
			if(lines == null || lines.Length == 0 ||
				size.Item1 <= 0 || size.Item2 <= 0)
				return;

			var x = position.Item1;
			var y = position.Item2;


			var lineList = new List<string>(lines);

			if(lineList == null || lineList.Count == 0)
				return;

			for(int i = 0; i < lineList.Count - 1; i++)
				lineList[i] = lineList[i] + '\n';

			for(int i = 0; i < lineList.Count; i++)
			{
				var line = lineList[i];

				for(int j = 0; j < line.Length; j++)
				{
					var isSymbolNewLine = line[j] == '\n' && j != line.Length - 1 && j > size.Item1;
					var isEndOfLine = j > size.Item1;

					if(j == line.Length - 1 && line[j] == '\n')
					{
						j--;
						line = line[0..^1];
						lineList[i] = line;
					}

					if(isEndOfLine ^ isSymbolNewLine)
					{
						var newLineIndex = isWordWrapping && isSymbolNewLine == false ?
							GetSafeNewLineIndex(line, (uint)j) : j;

						// end of line? can't word wrap, proceed to symbol wrap
						if(newLineIndex == 0)
						{
							lineList[i] = line[0..size.Item1];
							var newLineSymbol = line[size.Item1..line.Length];
							if(i == lineList.Count - 1)
							{
								lineList.Add(newLineSymbol);
								break;
							}
							lineList[i + 1] = $"{newLineSymbol} {lineList[i + 1]}";
							break;
						}

						lineList[i] = line[0..newLineIndex];

						var newLine = isWordWrapping ?
							line[(newLineIndex + 1)..^0] : line[j..^0];

						if(i == lineList.Count - 1)
						{
							lineList.Add(newLine);
							break;
						}

						var space = newLine.EndsWith('\n') ? "" : " ";
						lineList[i + 1] = $"{newLine}{space}{lineList[i + 1]}";
						break;
					}
				}
				if(i > size.Item2)
					break;
			}

			var yDiff = size.Item2 - lineList.Count;

			if(yDiff > 1)
			{
				if(alignment == Alignment.Left ||
					alignment == Alignment.Center ||
					alignment == Alignment.Right)
					for(int i = 0; i < yDiff / 2; i++)
						lineList.Insert(0, "");

				else if(alignment == Alignment.DownLeft ||
					alignment == Alignment.Down ||
					alignment == Alignment.DownRight)
					for(int i = 0; i < yDiff; i++)
						lineList.Insert(0, "");
			}

			for(int i = 0; i < lineList.Count; i++)
			{
				if(i >= size.Item2)
					return;

				var line = lineList[i].Replace('\n', ' ');

				if(isWordWrapping == false && i > size.Item1)
					NewLine();

				if(alignment == Alignment.UpRight ||
					alignment == Alignment.Right ||
					alignment == Alignment.DownRight)
					line = line.PadLeft(size.Item1);
				else if(alignment == Alignment.Up ||
					alignment == Alignment.Center ||
					alignment == Alignment.Down)
					line = PadLeftAndRight(line, size.Item1);

				SetTextLine((x, y), line, color);
				NewLine();
			}

			void NewLine()
			{
				x = position.Item1;
				y++;
			}
			int GetSafeNewLineIndex(string line, uint endLineIndex)
			{
				for(int i = (int)endLineIndex; i >= 0; i--)
					if(line[i] == ' ' && i <= size.Item1)
						return i;

				return default;
			}
		}

		public (float, float) PixelToPosition((int, int) pixel, (uint, uint) windowSize,
			bool isAccountingForCamera = true)
		{
			var x = Map(pixel.Item1, 0, windowSize.Item1, 0, TileCount.Item1);
			var y = Map(pixel.Item2, 0, windowSize.Item2, 0, TileCount.Item2);

			if(isAccountingForCamera)
			{
				x += CameraPosition.Item1;
				y += CameraPosition.Item2;
			}

			return (x, y);
		}

		public static int SymbolToTile(char symbol)
		{
			var index = default(int);
			if(symbol >= 'A' && symbol <= 'Z')
				index = symbol - 'A' + 78;
			else if(symbol >= 'a' && symbol <= 'z')
				index = symbol - 'a' + 104;
			else if(symbol >= '0' && symbol <= '9')
				index = symbol - '0' + 130;
			else if(map.ContainsKey(symbol))
				index = map[symbol];

			return index;
		}

		public static implicit operator Tilemap(int[,] tiles) => new(tiles);
		public static implicit operator int[,](Tilemap tilemap) => tilemap.tiles;
		public static implicit operator Tilemap(byte[,] colors) => new(colors);
		public static implicit operator byte[,](Tilemap tilemap) => tilemap.colors;

		#region Backend
		private static readonly Dictionary<char, int> map = new()
		{
			{ '░', 1 }, { '▒', 4 }, { '▓', 8 }, { '█', 11 },

			{ '⅛', 140 }, { '⅐', 141 }, { '⅙', 142 }, { '⅕', 143 }, { '¼', 144 },
			{ '⅓', 145 }, { '⅜', 146 }, { '⅖', 147 }, { '½', 148 }, { '⅗', 149 },
			{ '⅝', 150 }, { '⅔', 151 }, { '¾', 152 },  { '⅘', 153 },  { '⅚', 154 },  { '⅞', 155 },

			{ '₀', 156 }, { '₁', 157 }, { '₂', 158 }, { '₃', 159 }, { '₄', 160 },
			{ '₅', 161 }, { '₆', 162 }, { '₇', 163 }, { '₈', 164 }, { '₉', 165 },

			{ '⁰', 169 }, { '¹', 170 }, { '²', 171 }, { '³', 172 }, { '⁴', 173 },
			{ '⁵', 174 }, { '⁶', 175 }, { '⁷', 176 }, { '⁸', 177 }, { '⁹', 178 },

			{ '+', 182 }, { '-', 183 }, { '×', 184 }, { '―', 185 }, { '÷', 186 }, { '%', 187 },
			{ '=', 188 }, { '≠', 189 }, { '≈', 190 }, { '√', 191 }, { '∫', 193 }, { 'Σ', 194 },
			{ 'ε', 195 }, { 'γ', 196 }, { 'ϕ', 197 }, { 'π', 198 }, { 'δ', 199 }, { '∞', 200 },
			{ '≪', 204 }, { '≫', 205 }, { '≤', 206 }, { '≥', 207 }, { '<', 208 }, { '>', 209 },
			{ '(', 210 }, { ')', 211 }, { '[', 212 }, { ']', 213 }, { '{', 214 }, { '}', 215 },
			{ '⊥', 216 }, { '∥', 217 }, { '∠', 218 }, { '∟', 219 }, { '~', 220 }, { '°', 221 },
			{ '℃', 222 }, { '℉', 223 }, { '*', 224 }, { '^', 225 }, { '#', 226 }, { '№', 227 },
			{ '$', 228 }, { '€', 229 }, { '£', 230 }, { '¥', 231 }, { '¢', 232 }, { '¤', 233 },

			{ '!', 234 }, { '?', 235 }, { '.', 236 }, { ',', 237 }, { '…', 238 },
			{ ':', 239 }, { ';', 240 }, { '"', 241 }, { '\'', 242 }, { '`', 243 }, { '–', 244 },
			{ '_', 245 }, { '|', 246 }, { '/', 247 }, { '\\', 248 }, { '@', 249 }, { '&', 250 },
			{ '®', 251 }, { '℗', 252 }, { '©', 253 }, { '™', 254 },

			{ '→', 282 }, { '↓', 283 }, { '←', 284 }, { '↑', 285 },
			{ '⇨', 330 }, { '⇩', 331 }, { '⇦', 332 }, { '⇧', 333 },
			{ '➡', 334 }, { '⬇', 335 }, { '⬅', 336 }, { '⬆', 337 },
			{ '⭆', 356 }, { '⤋', 357 }, { '⭅', 358 }, { '⤊', 359 },
			{ '⇻', 360 }, { '⇟', 361 }, { '⇺', 362 }, { '⇞', 363 },

			{ '│', 260 }, { '─', 261 }, { '┌', 262 }, { '┐', 263 }, { '┘', 264 }, { '└', 265 },
			{ '├', 266 }, { '┤', 267 }, { '┴', 268 }, { '┬', 269 }, { '┼', 270 },
			{ '║', 286 }, { '═', 287 }, { '╔', 288 }, { '╗', 289 }, { '╝', 290 }, { '╚', 291 },
			{ '╠', 292 }, { '╣', 293 }, { '╩', 294 }, { '╦', 295 }, { '╬', 296 },

			{ '♩', 409 }, { '♪', 410 }, { '♫', 411 }, { '♬', 412 }, { '♭', 413 }, { '♮', 414 },
			{ '♯', 415 },

			{ '★', 385 }, { '☆', 386 }, { '✓', 390 }, { '⏎', 391 },

			{ '●', 475 }, { '○', 478 }, { '■', 469 }, { '□', 472 }, { '▲', 480 }, { '△', 482 },

			{ '♟', 456 }, { '♜', 457 }, { '♞', 458 }, { '♝', 459 }, { '♛', 460 }, { '♚', 461 },
			{ '♙', 462 }, { '♖', 463 }, { '♘', 464 }, { '♗', 465 }, { '♕', 466 }, { '♔', 467 },
			{ '♠', 448 }, { '♥', 449 }, { '♣', 450 }, { '♦', 451 },
			{ '♤', 452 }, { '♡', 453 }, { '♧', 454 }, { '♢', 455 },
		};
		private readonly int[,] tiles;
		private readonly byte[,] colors;

		private bool IndicesAreValid((int, int) indices)
		{
			return indices.Item1 >= 0 && indices.Item2 >= 0 &&
				indices.Item1 < tiles.GetLength(0) && indices.Item2 < tiles.GetLength(1);
		}
		private static string PadLeftAndRight(string text, int length)
		{
			var spaces = length - text.Length;
			var padLeft = spaces / 2 + text.Length;
			return text.PadLeft(padLeft).PadRight(length);

		}
		private static float Map(float number, float a1, float a2, float b1, float b2)
		{
			var value = (number - a1) / (a2 - a1) * (b2 - b1) + b1;
			return float.IsNaN(value) || float.IsInfinity(value) ? b1 : value;
		}
		private (int, int) IndexToCoords(int index)
		{
			var w = tiles.GetLength(0);
			var h = tiles.GetLength(1);
			index = index < 0 ? 0 : index;
			index = index > w * h - 1 ? w * h - 1 : index;

			return (index % w, index / w);
		}

		private void LoadFromCSV(string dataStr)
		{
			var values = dataStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
			for(int i = 0; i < values.Length; i++)
			{
				var value = int.Parse(values[i].Trim());
				var (x, y) = IndexToCoords(i);
				tiles[x, y] = value - 1;
			}
		}
		private void LoadFromBase64Uncompressed(string dataStr)
		{
			var bytes = Convert.FromBase64String(dataStr);
			LoadFromByteArray(bytes);
		}
		private void LoadFromBase64<T>(string dataStr) where T : Stream
		{
			var buffer = Convert.FromBase64String(dataStr);
			using var msi = new MemoryStream(buffer);
			using var mso = new MemoryStream();

			using var compStream = Activator.CreateInstance(
				typeof(T), msi, CompressionMode.Decompress) as T;

			if(compStream == null)
				return;

			CopyTo(compStream, mso);
			var bytes = mso.ToArray();
			LoadFromByteArray(bytes);
		}
		private void LoadFromByteArray(byte[] bytes)
		{
			var size = bytes.Length / sizeof(int);
			for(var i = 0; i < size; i++)
			{
				var (x, y) = IndexToCoords(i);
				var value = BitConverter.ToInt32(bytes, i * sizeof(int));
				tiles[x, y] = value - 1;
			}
		}

		private static void CopyTo(Stream src, Stream dest)
		{
			var bytes = new byte[4096];

			var i = 0;
			while((i = src.Read(bytes, 0, bytes.Length)) != 0)
				dest.Write(bytes, 0, i);
		}
		#endregion
	}
}