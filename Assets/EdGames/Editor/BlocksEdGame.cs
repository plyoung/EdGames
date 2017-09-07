//#define DEBUG_OPTIONS
//#define DEBUG_RENDER

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace EdGames
{
	public class BlocksEdGame : EditorWindow
	{
		private const int GridWidth = 10;
		private const int GridHeight = 20;
		private const int CellSize = 20;
		private const float BlockMoveMax = CellSize * 0.1f;
		private const float SoftDropSpeed = 300f;
		private const float SpeedInc = 1f;

		private static readonly GUIContent GC_Help = new GUIContent("?");
		private static readonly GUIContent GC_Reset = new GUIContent("Reset");
		private static readonly GUIContent GC_Label = new GUIContent("Blocks Game");
		private static readonly GUIContent[] GC_HelpLines =
			{
				new GUIContent("<- -> arrow keys to move"),
				new GUIContent("up arrow key to rotate"),
				new GUIContent("down arrow key to drop faster"),
			};

		private static readonly Color[] BlockColours = 
			{
				new Color(1.0f, 1.0f, 0.0f, 1f),
				new Color(1.0f, 0.0f, 0.0f, 1f),
				new Color(0.0f, 1.0f, 1.0f, 1f),
				new Color(1.0f, 0.5f, 0.0f, 1f),
				new Color(0.0f, 0.0f, 1.0f, 1f),
				new Color(1.0f, 0.0f, 1.0f, 1f),
				new Color(0.0f, 1.0f, 0.0f, 1f)
			};

		private static readonly int[][,] BlockShapes = 
			{ 
				new int[,] { { 1, 1 },
							 { 1, 1 } },

				new int[,] { { 0, 0, 0, 0 },
							 { 0, 0, 0, 0 },
							 { 1, 1, 1, 1 },
							 { 0, 0, 0, 0 } },

				new int[,] { { 0, 0, 0 },
							 { 1, 1, 1 },
							 { 0, 1, 0 } },

				new int[,] { { 0, 0, 0 },
							 { 1, 1, 1 },
							 { 1, 0, 0 } },

				new int[,] { { 0, 0, 0 },
							 { 1, 1, 1 },
							 { 0, 0, 1 } },

				new int[,] { { 0, 0, 0 },
							 { 0, 1, 1 },
							 { 1, 1, 0 } },


				new int[,] { { 0, 0, 0 },
							 { 1, 1, 0 },
							 { 0, 1, 1 } }
			};

		public class Clump
		{
			public int y;
			public int h;
			public int[,] cells;

			public Clump(int height)
			{
				y = 0;
				h = height;
				cells = new int[GridWidth, height];
				for (int x = 0; x < GridWidth; x++)
				{
					for (int y = 0; y < height; y++)
					{
						cells[x, y] = -1;
					}
				}
			}
		}

		private Texture2D[] textures;
		private GUIStyle backStyle;

		private bool doRepaint = false;
		private Rect boardRect;
		private Rect cellRect;
		private Rect labelRect;
		private Rect helpButtonRect;
		private Rect resetButtonRect;

		[System.NonSerialized] private int blockType = -1;
		private int[] grid = new int[GridWidth * GridHeight];
		private int[,] block;
		private int blockWH;
		private Vector2 blockPos;
		private double time;
		private float speed;
		private bool gameActive;
		private int score;
		private bool softDrop = false;

		// ------------------------------------------------------------------------------------------------------------------

		[MenuItem("Window/Games/Blocks Game")]
		private static void OpenBlocksEdGame()
		{
			GetWindow<BlocksEdGame>("Blocks Game");
		}

		private void Awake()
		{
			boardRect = new Rect(5, CellSize * 2 + 5, CellSize * GridWidth + 2, CellSize * GridHeight + 2);
			cellRect = new Rect(0, 0, CellSize, CellSize);
			labelRect = new Rect(5, 10, boardRect.width, 20);
			helpButtonRect = new Rect(boardRect.xMax - 20, 13, 20, 15);
			resetButtonRect = new Rect(helpButtonRect.x - 50, 13, 50, 15);
			minSize = new Vector2(boardRect.width + 10, boardRect.height + (CellSize * 2) + 10);
			GenerateAssets(); 
			ResetGame();
		}

		private void OnDestroy()
		{
			DestroyAssets();
		}

		private void OnFocus()
		{
			// need to reset this so deltaTime don't have huge jump
			// cause game is "paused" while window is not focused
			time = EditorApplication.timeSinceStartup;
		}

		private void OnGUI()
		{
			Event ev = Event.current;

			if (blockType < 0)
			{
				ResetGame();
			}

			if (gameActive && focusedWindow == this)
			{
				doRepaint = true;
				DoUpdate(ev);
			}

			DoDraw(ev);

			if (doRepaint)
			{
				doRepaint = false;
				Repaint();
			}
		}

		// ------------------------------------------------------------------------------------------------------------------

		private void DoUpdate(Event ev)
		{
			// handle input
			if (ev.type == EventType.KeyDown)
			{
				switch (ev.keyCode)
				{
					case KeyCode.LeftArrow: MoveBlock(-1); ev.Use(); break;
					case KeyCode.RightArrow: MoveBlock(+1); ev.Use(); break;
					case KeyCode.UpArrow: RotateBlock(); ev.Use(); break;
					case KeyCode.DownArrow: softDrop = true; ev.Use(); break;
#if DEBUG_OPTIONS
					case KeyCode.Space: DebugChangeBlock(); ev.Use(); break;
					case KeyCode.KeypadPlus: DebugChangeSpeed(10); ev.Use(); break;
					case KeyCode.KeypadMinus: DebugChangeSpeed(-10); ev.Use(); break;
#endif
				}
			}
			else if (ev.type == EventType.KeyUp && ev.keyCode == KeyCode.DownArrow)
			{
				softDrop = false;
			}

			// move block down
			AdvanceBlock();

			// check if should snap in yet
			if (ShouldSnapIn())
			{
				if (SnapBlock()) // game over if this returns true
				{
					gameActive = false;
				}
				else
				{
					CheckLines();
					SelectBlock();
				}
			}
		}

		private void DebugChangeSpeed(int s)
		{
			speed += s;
		}

		private void DebugChangeBlock()
		{
			time = EditorApplication.timeSinceStartup;
			blockType++; if (blockType == BlockShapes.Length) blockType = 0;
			block = BlockShapes[blockType];
			blockWH = (int)Mathf.Sqrt(block.Length);
			blockPos = new Vector2(boardRect.x + 1 + (CellSize * (5 - Mathf.CeilToInt(blockWH / 2f))), boardRect.y - (CellSize * (blockWH - 1)));
		}

		private void ResetGame()
		{
			time = EditorApplication.timeSinceStartup;
			gameActive = true;
			softDrop = false;
			score = 0;
			speed = 10f;
			for (int i = 0; i < grid.Length; i++) grid[i] = -1;
			SelectBlock();
		}

		private void SelectBlock()
		{
			softDrop = false;
			time = EditorApplication.timeSinceStartup;
			blockType = Random.Range(0, BlockShapes.Length);
			block = BlockShapes[blockType];
			blockWH = (int)Mathf.Sqrt(block.Length);
			blockPos = new Vector2(boardRect.x + 1 + (CellSize * (5 - Mathf.CeilToInt(blockWH / 2f))), boardRect.y - (CellSize * (blockWH - 1)));
		}

		private void RotateBlock()
		{
			if (blockType == 0) return; // no need to rotate this one

			Vector2 oldPos = blockPos;
			int[,] oldShape = block;

			block = EdGameUtil.RotateMatrix(block, blockWH);

			// check if rotated while next to a wall and if block is now outside of board
			int res = 0;
			if ((res = IsOutsideEdge()) != 0)
			{
				blockPos.x += (res * CellSize);
				if (blockType == 1)
				{   // this one might have two cells outside of border and need another kick
					if (blockPos.x < boardRect.x || blockPos.x + blockWH * CellSize > boardRect.xMax)
					{
						blockPos.x += (res * CellSize);
					}
				}
			}

			// check if over any filled cells now and revert if so
			if (IsOverFilledCells(false))
			{
				blockPos = oldPos;
				block = oldShape;
			}
		}

		private void MoveBlock(int dir)
		{
			Vector2 oldPos = blockPos;
			blockPos.x += CellSize * dir;
			if (IsOutsideEdge() != 0 || IsOverFilledCells(false))
			{
				blockPos = oldPos;
			}
		}

		private void AdvanceBlock()
		{
			float deltatime = (float)(EditorApplication.timeSinceStartup - time);
			time = EditorApplication.timeSinceStartup;

			Vector2 oldPos = blockPos;
			if (softDrop)
			{
				blockPos.y += Mathf.Min((speed > SoftDropSpeed ? speed : SoftDropSpeed) * deltatime, BlockMoveMax);
			}
			else
			{
				blockPos.y += Mathf.Min(speed * deltatime, BlockMoveMax);
			}

			if (IsOverFilledCells(false))
			{
				blockPos = oldPos;
			}
		}

		private int IsOutsideEdge()
		{   // res: 0: none, -1: right, +1: left (the values can be used in multiplication for kickback position calculation)
			// only check if any part of matrix is outside
			if (blockPos.x < boardRect.x || blockPos.x + blockWH * CellSize > boardRect.xMax)
			{
				if (blockType == 0)
				{
					// Block 0 is fully filled 2x2 matrix so no need for further checks
					// does not matter if I send -1 or +1 since this can't be rotated
					// and that value is only needed when determine wall kick
					return 1;
				}

				// now check against the filled cells since part of matrix may be outside if not filled
				Vector2 p = Vector2.zero;
				for (int y = 0; y < blockWH; y++)
				{
					for (int x = 0; x < blockWH; x++)
					{
						if (block[y, x] == 1)
						{
							p.x = x * CellSize + blockPos.x;
							p.y = y * CellSize + blockPos.y;
							if (p.x < boardRect.x || p.x + CellSize > boardRect.xMax)
							{
								return (blockPos.x < boardRect.center.x ? +1 : -1);
							}
						}
					}
				}
			}
			return 0;
		}

		private bool ReachedBottom()
		{   // check if the Block reached bottom and should snap in
			// only check if any part of matrix is at bottom
			if (blockPos.y + blockWH * CellSize > boardRect.yMax)
			{
				if (blockType == 0) return true; // no further checks needed for this one

				// now check against the filled cells since part of matrix may be outside if not filled
				Vector2 p = Vector2.zero;
				for (int y = 0; y < blockWH; y++)
				{
					for (int x = 0; x < blockWH; x++)
					{
						if (block[y, x] == 1)
						{
							p.x = x * CellSize + blockPos.x;
							p.y = y * CellSize + blockPos.y;
							if (p.y + CellSize > boardRect.yMax)
							{
								return true;
							}
						}
					}
				}
			}

			return false;
		}

		private bool IsOverFilledCells(bool below)
		{
			int ox, oy;
			EdGameUtil.CalcCellPos(boardRect, blockPos, CellSize, below, out ox, out oy);
			if (below) oy++;
			for (int x = 0; x < blockWH; x++)
			{
				for (int y = 0; y < blockWH; y++)
				{
					if (block[y, x] == 1)
					{
						// idx can be smaller than 0 for new blocks, which starts above board and idx could
						// be bigger than grid.Length because of empty cells reaching below board
						int idx = (x + ox) + ((y + oy) * GridWidth);
						if (idx >= 0 && idx < grid.Length && grid[idx] >= 0) return true;
					}
				}
			}

			return false;
		}

		private bool ShouldSnapIn()
		{
			return (ReachedBottom() || IsOverFilledCells(true));
		}

		private bool SnapBlock()
		{
			int ox, oy;
			EdGameUtil.CalcCellPos(boardRect, blockPos, CellSize, true, out ox, out oy);
			for (int x = 0; x < blockWH; x++)
			{
				for (int y = 0; y < blockWH; y++)
				{
					if (block[y, x] == 1)
					{
						int idx = (x + ox) + ((y + oy) * GridWidth);
						if (idx < 0) return true; // if idx invalid then reached top and game over
						grid[idx] = blockType;
					}
				}
			}
			return false;
		}

		private void CheckLines()
		{
			// start from bottom row and find filled rows to remove
			int linesCount = 0;
			int firstFoundRow = -1;
			for (int y = GridHeight - 1; y >= 0; y--)
			{
				bool foundLine = true;
				for (int x = 0; x < GridWidth; x++)
				{
					if (grid[x + y * GridWidth] < 0)
					{
						foundLine = false;
						break;
					}
				}

				if (foundLine)
				{
					if (firstFoundRow < 0) firstFoundRow = y;
					linesCount++;
					for (int x = 0; x < GridWidth; x++) grid[x + y * GridWidth] = -1; // clear the line
				}
			}

			if (linesCount > 0)
			{
				// calculate score and new speed
				int addScore = linesCount + (linesCount > 3 ? 1 : 0);
				score += addScore;
				speed += addScore * SpeedInc;

				// let clumps of blocks fall down to fill space below
				FallLinesAbove(firstFoundRow);

				// run check again since new lines might have formed
				CheckLines();
			}
		}

		private void FallLinesAbove(int startY)
		{
			List<Clump> clumps = new List<Clump>();

			// find a filled cell
			for (int y = startY; y >= 0; y--)
			{
				for (int x = 0; x < GridWidth; x++)
				{
					int idx = x + y * GridWidth;
					if (grid[idx] >= 0)
					{
						Clump clump = GetClump(x, y);
						clumps.Add(clump);
					}
				}
			}

			// drop the clumps
			// first add them all back to the grid since the cells where cleared while collecting clumps
			for (int i = clumps.Count - 1; i >= 0; i--)
			{
				Clump c = clumps[i];
				for (int y = 0; y < c.h; y++)
				{
					for (int x = 0; x < GridWidth; x++)
					{
						grid[x + y * GridWidth] = c.cells[x, y];
					}
				}
			}

			// now try to move each clump down a line until none can move
			bool moved = true;
			while (moved)
			{
				moved = false;
				foreach(Clump c in clumps)
				{
					// remove the cells of this clump from the grid so it don't "collide with itself"
					for (int y = 0; y < c.h; y++)
					{
						for (int x = 0; x < GridWidth; x++)
						{
							if (c.cells[x, y] >= 0) grid[x + (y + c.y) * GridWidth] = -1;
						}
					}

					// now try to drop the clump down as far possible
					bool collided = false;
					while (!collided)
					{
						c.y++;

						// check if any cells will be over another
						for (int y = 0; y < c.h; y++)
						{
							for (int x = 0; x < GridWidth; x++)
							{
								if (c.cells[x, y] >= 0)
								{
									int idx = x + (y + c.y) * GridWidth;
									if (idx >= grid.Length || grid[idx] >= 0)
									{
										collided = true;
										break;
									}
								}
							}
							if (collided) break;
						}

						if (collided) c.y--; // revert
						else moved = true;
					}

					// put it back in the grid
					for (int y = 0; y < c.h; y++)
					{
						for (int x = 0; x < GridWidth; x++)
						{
							if (c.cells[x, y] >= 0) grid[x + (y + c.y) * GridWidth] = c.cells[x, y];
						}
					}

				}
			}
		}

		private Clump GetClump(int sx, int sy)
		{
			Clump clump = new Clump(sy + 1);
			AddCellToClump(clump, sx, sy, sy);
			return clump;
		}

		private void AddCellToClump(Clump clump, int sx, int sy, int maxY)
		{
			int idx = sx + sy * GridWidth;
			clump.cells[sx, sy] = grid[idx];
			grid[idx] = -1;

			// check if connected below
			if (sy < maxY - 1 && grid[sx + (sy + 1) * GridWidth] >= 0)
			{
				AddCellToClump(clump, sx, sy + 1, maxY);
			}

			// check if connected left
			if (sx > 0 && grid[(sx - 1) + sy * GridWidth] >= 0)
			{
				AddCellToClump(clump, sx - 1, sy, maxY);
			}

			// check if connected above
			if (sy > 0 && grid[sx + (sy - 1) * GridWidth] >= 0)
			{
				AddCellToClump(clump, sx, sy - 1, maxY);
			}

			// check if connected right
			if (sx < GridWidth - 1 && grid[(sx + 1) + sy * GridWidth] >= 0)
			{
				AddCellToClump(clump, sx + 1, sy, maxY);
			}
		}

		// ------------------------------------------------------------------------------------------------------------------

		private void DoDraw(Event ev)
		{
			// UI
			GUI.Label(labelRect, string.Format("Score: {0}", score), EditorStyles.largeLabel);

			if (GUI.Button(resetButtonRect, GC_Reset, EditorStyles.miniButtonLeft))
			{
				ResetGame();
			}

			if (GUI.Button(helpButtonRect, GC_Help, EditorStyles.miniButtonRight))
			{
				EdGameHelpWindow.ShowWindow(GC_Label, GC_HelpLines);
			}

			if (ev.type == EventType.Repaint)
			{
				DrawBoard();
				DrawBlock();
			}
		}

		private void DrawBoard()
		{
			backStyle.Draw(boardRect, false, false, false, false);

			cellRect.x = boardRect.x + 1;
			cellRect.y = boardRect.y + 1;
			for (int i = 0; i < grid.Length; i++)
			{
#if DEBUG_RENDER
				GUI.color = new Color(1f, 1f, 1f, 0.1f);
				GUI.DrawTexture(cellRect, textures[1]);
#endif

				if (grid[i] >= 0)
				{
					GUI.color = BlockColours[grid[i]];
					GUI.DrawTexture(cellRect, textures[1]);
				}

				cellRect.x += CellSize;
				if ((i+1) % (float)GridWidth == 0)
				{
					cellRect.x = boardRect.x + 1;
					cellRect.y += CellSize;
				}
			}

			GUI.color = Color.white;
		}

		private void DrawBlock()
		{
			GUI.color = BlockColours[blockType];
			cellRect.x = blockPos.x;
			cellRect.y = blockPos.y;
			for (int y = 0; y < blockWH; y++)
			{
				cellRect.y = y * CellSize + blockPos.y;
				for (int x = 0; x < blockWH; x++)
				{
					cellRect.x = x * CellSize + blockPos.x;
#if DEBUG_RENDER
					GUI.color = BlockColours[blockType];
					if (block[y, x] == 0) GUI.color = new Color(BlockColours[blockType].r, BlockColours[blockType].g, BlockColours[blockType].b, 0.2f);
					GUI.DrawTexture(cellRect, textures[1]);
#else
					if (block[y, x] == 1) GUI.DrawTexture(cellRect, textures[1]);
#endif
				}
			}
			GUI.color = Color.white;

#if DEBUG_RENDER
			int a, b; CalcCellPos(blockPos, true, out a, out b);
			GUI.Label(new Rect(boardRect.x, boardRect.yMax + 2, 300, 20), string.Format("{0} => {1},{2}", blockPos, a, b));
#endif
		}

		// ------------------------------------------------------------------------------------------------------------------

		private void GenerateAssets()
		{
			// generate all needed assets so that I do not need anything else but this script in the project folder
			textures = new Texture2D[2];

			Color32 cBlack = new Color32(0, 0, 0, 255);
			Color32 cWhite = new Color32(255, 255, 255, 255);
			Color32 cGrey = new Color32(128, 128, 128, 255);

			// create the background
			textures[0] = new Texture2D(4, 4, TextureFormat.RGBA32, false);
			EdGameUtil.TextureFill(textures[0], cBlack);
			EdGameUtil.TextureRect(textures[0], cWhite, 0, 0, 3, 3);

			// create the cell/block texture
			textures[1] = new Texture2D(CellSize, CellSize, TextureFormat.RGBA32, false);
			EdGameUtil.TextureFill(textures[1], cWhite);
			EdGameUtil.TextureFillRect(textures[1], cGrey, CellSize / 2 - 2, CellSize / 2 - 2, 4, 4);
			EdGameUtil.TextureRect(textures[1], cGrey, 0, 0, CellSize-1, CellSize-1);

			// apply common texture settings
			foreach (Texture2D t in textures)
			{
				t.wrapMode = TextureWrapMode.Clamp;
				t.filterMode = FilterMode.Point;
				t.hideFlags = HideFlags.HideAndDontSave;
			}

			// create the styles
			backStyle = new GUIStyle() { border = new RectOffset(1, 1, 1, 1), normal = { background = textures[0] } };
		}

		private void DestroyAssets()
		{
			foreach (Texture2D t in textures)
			{
				if (t != null) DestroyImmediate(t);
			}
		}

		// ------------------------------------------------------------------------------------------------------------------
	}
}